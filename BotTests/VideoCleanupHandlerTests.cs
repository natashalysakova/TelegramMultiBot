using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Telegram.Bot;
using TelegramMultiBot.Database;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Interfaces;
using VideoDownloader;
using VideoDownloader.Client;

namespace BotTests;

[TestClass]
public class VideoCleanupHandlerTests
{
    [TestMethod]
    public async Task UnreachableMeTubeLogsWarningAndStillCleansDatabase()
    {
        using var db = CreateDatabase();
        await AddOldEntry(db);
        var logger = new RecordingLogger<VideoCleanupHandler>();
        var handler = CreateCleanup(db, logger, (_, _) =>
            throw new HttpRequestException(HttpRequestError.NameResolutionError, "name resolution failed"));

        await handler.CleanupOld(CancellationToken.None);

        Assert.AreEqual(0, await db.VideoDownloads.CountAsync());
        Assert.IsTrue(logger.Entries.Any(e => e.Level == LogLevel.Warning &&
            e.Message.Contains("VideoDownloader.MeTubeUrl") && e.Message.Contains("name resolution failed")));
        Assert.IsFalse(logger.Entries.Any(e => e.Level == LogLevel.Error));
    }

    [TestMethod]
    public async Task TimeoutContinuesDatabaseCleanup()
    {
        using var db = CreateDatabase();
        await AddOldEntry(db);
        var logger = new RecordingLogger<VideoCleanupHandler>();
        var handler = CreateCleanup(db, logger, (_, _) => throw new TaskCanceledException("timeout"));

        await handler.CleanupOld(CancellationToken.None);

        Assert.AreEqual(0, await db.VideoDownloads.CountAsync());
        Assert.IsTrue(logger.Entries.Any(e => e.Level == LogLevel.Warning && e.Message.Contains("Timed out")));
    }

    [TestMethod]
    public async Task CancellationStopsRequestAndDoesNotDeleteDatabaseRecords()
    {
        using var db = CreateDatabase();
        await AddOldEntry(db);
        var logger = new RecordingLogger<VideoCleanupHandler>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = CreateCleanup(db, logger, async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var cancellation = new CancellationTokenSource();

        var cleanup = handler.CleanupOld(cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () => await cleanup);

        Assert.AreEqual(1, await db.VideoDownloads.CountAsync());
        Assert.HasCount(0, logger.Entries);
    }

    [TestMethod]
    public async Task HttpErrorAndInvalidJsonAreLoggedAndDatabaseCleanupContinues()
    {
        foreach (var response in new[]
        {
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("invalid json") }
        })
        {
            using var db = CreateDatabase();
            await AddOldEntry(db);
            var logger = new RecordingLogger<VideoCleanupHandler>();
            var handler = CreateCleanup(db, logger, (_, _) => Task.FromResult(response));

            await handler.CleanupOld(CancellationToken.None);

            Assert.AreEqual(0, await db.VideoDownloads.CountAsync());
            Assert.IsTrue(logger.Entries.Any(e => e.Level == LogLevel.Error && e.Exception is not null));
        }
    }

    [TestMethod]
    public async Task UnavailableServiceRecoversAndDeletesOnlyOldHistory()
    {
        using var db = CreateDatabase();
        var logger = new RecordingLogger<VideoCleanupHandler>();
        var calls = 0;
        string? deleteBody = null;
        var handler = CreateCleanup(db, logger, async (request, token) =>
        {
            calls++;
            if (calls == 1)
                throw new HttpRequestException("connection failed");
            if (request.RequestUri!.AbsolutePath == "/history")
                return JsonResponse("""{"done":[{"id":"old","url":"https://old","status":"finished","title":"old","timestamp":1},{"id":"new","url":"https://new","status":"finished","title":"new","timestamp":4102444800}]}""");
            deleteBody = await request.Content!.ReadAsStringAsync(token);
            return JsonResponse("""{"status":"ok","msg":""}""");
        });

        await handler.CleanupOld(CancellationToken.None);
        await handler.CleanupOld(CancellationToken.None);

        Assert.AreEqual(3, calls);
        Assert.IsNotNull(deleteBody);
        StringAssert.Contains(deleteBody, "https://old");
        Assert.IsFalse(deleteBody.Contains("https://new"));
        Assert.IsFalse(logger.Entries.Any(e => e.Level == LogLevel.Error));
    }

    [TestMethod]
    public async Task RejectedDeleteIsLoggedAndDatabaseCleanupContinues()
    {
        using var db = CreateDatabase();
        await AddOldEntry(db);
        var logger = new RecordingLogger<VideoCleanupHandler>();
        var handler = CreateCleanup(db, logger, (request, _) => Task.FromResult(
            request.RequestUri!.AbsolutePath == "/history"
                ? JsonResponse("""{"done":[{"id":"old","url":"https://old","status":"finished","title":"old","timestamp":1}]}""")
                : JsonResponse("""{"status":"error","msg":"delete rejected"}""")));

        await handler.CleanupOld(CancellationToken.None);

        Assert.AreEqual(0, await db.VideoDownloads.CountAsync());
        Assert.IsTrue(logger.Entries.Any(e => e.Level == LogLevel.Error && e.Message.Contains("delete rejected")));
    }

    [TestMethod]
    public async Task DeleteHttpFailureIdentifiesOperationAndKeepsRecentEntries()
    {
        using var db = CreateDatabase();
        await AddOldEntry(db);
        db.VideoDownloads.Add(new VideoDownload
        {
            Id = Guid.NewGuid(),
            VideoUrl = "https://example.com/recent",
            RequestedBy = "test",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var logger = new RecordingLogger<VideoCleanupHandler>();
        var handler = CreateCleanup(db, logger, (request, _) => Task.FromResult(
            request.RequestUri!.AbsolutePath == "/history"
                ? JsonResponse("""{"done":[{"id":"old","url":"https://old","status":"finished","title":"old","timestamp":1}]}""")
                : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        await handler.CleanupOld(CancellationToken.None);

        Assert.AreEqual("https://example.com/recent", (await db.VideoDownloads.SingleAsync()).VideoUrl);
        Assert.IsTrue(logger.Entries.Any(e => e.Level == LogLevel.Error && e.Message.Contains("delete old downloads")));
    }

    [TestMethod]
    public async Task ProcessingKeepsPendingJobWhenMeTubeIsUnreachable()
    {
        using var db = CreateDatabase();
        await AddOldEntry(db);
        var logger = new RecordingLogger<VideoProcessHandler>();
        var client = CreateClient((_, _) => throw new HttpRequestException("name resolution failed"));
        var handler = new VideoProcessHandler(logger, new TelegramBotClient("123456:example-token"), client, db);

        await handler.ProcessDownloads(CancellationToken.None);

        Assert.AreEqual(VideoDownloadStatus.Pending, (await db.VideoDownloads.SingleAsync()).Status);
        Assert.IsTrue(logger.Entries.Any(e => e.Level == LogLevel.Warning && e.Message.Contains("MeTubeUrl")));
    }

    [TestMethod]
    public async Task ProcessingPropagatesShutdownCancellation()
    {
        using var db = CreateDatabase();
        await AddOldEntry(db);
        var logger = new RecordingLogger<VideoProcessHandler>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = CreateClient(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var handler = new VideoProcessHandler(logger, new TelegramBotClient("123456:example-token"), client, db);
        using var cancellation = new CancellationTokenSource();

        var processing = handler.ProcessDownloads(cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () => await processing);

        Assert.AreEqual(VideoDownloadStatus.Pending, (await db.VideoDownloads.SingleAsync()).Status);
        Assert.HasCount(0, logger.Entries);
    }

    private static BoberDbContext CreateDatabase() =>
        new(new DbContextOptionsBuilder<BoberDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task AddOldEntry(BoberDbContext db)
    {
        db.VideoDownloads.Add(new VideoDownload
        {
            Id = Guid.NewGuid(),
            VideoUrl = "https://example.com/video",
            RequestedBy = "test",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-15)
        });
        await db.SaveChangesAsync();
    }

    private static VideoCleanupHandler CreateCleanup(
        BoberDbContext db,
        ILogger<VideoCleanupHandler> logger,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
    {
        return new VideoCleanupHandler(logger, CreateClient(send), db);
    }

    private static MeTubeClient CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
    {
        var settings = new Mock<ISqlConfiguationService>();
        settings.Setup(x => x.VideoDownloaderSettings).Returns(new VideoDownloaderSettings { MeTubeUrl = "http://metube:8081" });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient(new TestHttpHandler(send)));
        return new MeTubeClient(settings.Object, NullLogger<MeTubeClient>.Instance, factory.Object);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class TestHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }
}
