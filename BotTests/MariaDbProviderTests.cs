using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using TelegramMultiBot.Database;
using TelegramMultiBot.Database.Models;

namespace BotTests;

[TestClass]
public class MariaDbProviderTests
{
    private static readonly MariaDbServerVersion ServerVersion = new(new Version(11, 6, 0));

    [TestMethod]
    public void ContextUsesPomeloAndRetainsHistoricalMigrations()
    {
        using var context = CreateContext("Server=localhost;Database=telegrammultibot_test_unused;User ID=test");

        Assert.AreEqual("Pomelo.EntityFrameworkCore.MySql", context.Database.ProviderName);
        var migrations = context.Database.GetMigrations().ToArray();
        Assert.AreEqual(56, migrations.Length);
        Assert.AreEqual("20240218151941_Init", migrations[0]);
        Assert.AreEqual("20260414164003_AddUnqueIndex", migrations[^1]);
        Assert.IsFalse(context.Database.HasPendingModelChanges(), "The model differs from the committed snapshot.");
    }

    [TestMethod]
    public void RepresentativeQueriesTranslateForMariaDb()
    {
        using var context = CreateContext("Server=localhost;Database=telegrammultibot_test_unused;User ID=test");
        var groupId = Guid.NewGuid();

        var groupQuery = context.ElectricityGroups
            .Where(x => x.Id == groupId && EF.Functions.Like(x.GroupName, "%тест%"))
            .ToQueryString();
        var historyQuery = context.Assistants
            .Include(x => x.ChatHistory)
            .Where(x => x.IsActive)
            .ToQueryString();

        StringAssert.Contains(groupQuery, "LIKE");
        StringAssert.Contains(groupQuery, "ElectricityGroups");
        StringAssert.Contains(historyQuery, "JOIN");
        StringAssert.Contains(historyQuery, "ChatHistory");
    }

    [TestMethod]
    [TestCategory("MariaDbIntegration")]
    public async Task ExistingMariaDbSchemaSupportsPomeloOperations()
    {
        var connectionString = Environment.GetEnvironmentVariable("TELEGRAM_MULTIBOT_TEST_DB");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Inconclusive("Set TELEGRAM_MULTIBOT_TEST_DB to a dedicated MariaDB 11.6 test database.");
            return;
        }

        var connectionSettings = new MySqlConnectionStringBuilder(connectionString);
        Assert.IsTrue(
            connectionSettings.Database.StartsWith("telegrammultibot_test_", StringComparison.Ordinal),
            "The database name must start with telegrammultibot_test_.");

        using var context = CreateContext(connectionString);
        await context.Database.OpenConnectionAsync();
        var connection = context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT VERSION()";
        var serverVersion = Convert.ToString(await command.ExecuteScalarAsync());
        Assert.IsTrue(
            serverVersion is not null && serverVersion.Contains("MariaDB", StringComparison.OrdinalIgnoreCase)
                && serverVersion.StartsWith("11.6.", StringComparison.Ordinal),
            $"Expected MariaDB 11.6 but connected to {serverVersion}.");
        command.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE()";
        var tableCount = Convert.ToInt64(await command.ExecuteScalarAsync());
        if (tableCount == 0)
        {
            await context.Database.MigrateAsync();
        }

        var migrations = context.Database.GetMigrations().ToArray();
        CollectionAssert.AreEqual(migrations, (await context.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.IsFalse((await context.Database.GetPendingMigrationsAsync()).Any());
        await context.Database.MigrateAsync();

        await using var transaction = await context.Database.BeginTransactionAsync();
        context.Seed();
        Assert.IsTrue(await context.ElectricityLocations.AnyAsync(x => x.Region == "kem"));
        Assert.IsTrue(await context.ElectricityLocations.AnyAsync(x => x.Region == "krem"));
        var group = new ElectricityGroup
        {
            Id = Guid.NewGuid(),
            LocationRegion = "test_" + Guid.NewGuid().ToString("N"),
            GroupCode = "group_1",
            GroupName = "Тестова група",
            DataSnapshot = "тест"
        };
        var assistant = new AssistantSubscriber
        {
            ChatId = 987654321,
            IsActive = true
        };
        assistant.ChatHistory =
        [
            new ChatHistory
            {
                Assistant = assistant,
                MessageId = 1,
                Author = "Тест",
                Text = "Привіт",
                SendTime = new DateTime(2026, 1, 2, 3, 4, 5)
            }
        ];
        context.ElectricityGroups.Add(group);
        context.Assistants.Add(assistant);
        await context.SaveChangesAsync();
        Assert.IsTrue(assistant.Id > 0);
        Assert.IsTrue(assistant.ChatHistory.Single().Id > 0);

        var groupId = group.Id;
        var assistantId = assistant.Id;
        context.ChangeTracker.Clear();
        var savedGroup = await context.ElectricityGroups.SingleAsync(x => x.Id == groupId);
        Assert.AreEqual("Тестова група", savedGroup.GroupName);
        var savedAssistant = await context.Assistants.Include(x => x.ChatHistory).SingleAsync(x => x.Id == assistantId);
        var message = savedAssistant.ChatHistory!.Single();
        Assert.AreEqual("Привіт", message.Text);
        Assert.AreEqual(new DateTime(2026, 1, 2, 3, 4, 5), message.SendTime);
        await transaction.RollbackAsync();
    }

    private static BoberDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<BoberDbContext>()
            .UseLazyLoadingProxies()
            .UseMySql(connectionString, ServerVersion)
            .Options;
        return new BoberDbContext(options);
    }
}
