using AngleSharp.Html;
using DtekParsers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TelegramMultiBot.Database;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.ImageCompare;

namespace BotTests;

[TestClass]
public class MonitorDtekTests
{

    [TestMethod]
    public async Task PageLoaded()
    {
        var parser = new ScheduleParser();

        var html = await parser.GetHtmlFromUrl("https://www.dtek-krem.com.ua/ua/shutdowns");

        Assert.IsNotNull(html);
        Assert.IsTrue(html.Contains("DisconSchedule.fact"));
        Assert.IsTrue(html.Contains("DisconSchedule.preset"));
    }

    [TestMethod]
    public async Task PageParsed()
    {
        var parser = new ScheduleParser();

        var schedule = await parser.Parse("https://www.dtek-krem.com.ua/ua/shutdowns");

        Assert.IsNotNull(schedule);
        Assert.IsTrue(schedule.Count == 12);
        foreach (var group in schedule)
        {
            Assert.IsTrue(group.RealSchedule.Count == 2);
            Assert.IsTrue(group.PlannedSchedule.Count == 7);
        }
    }

    [TestMethod]
    public async Task RealScheduleSingleGroupImageReady()
    {
        var parser = new ScheduleParser();

        var schedule = await parser.Parse("https://www.dtek-krem.com.ua/ua/shutdowns");

        var image = await ScheduleImageGenerator.GenerateRealScheduleSingleGroupImage(schedule.ElementAt(1));

        File.WriteAllBytes("img.png", image);
    }

    private static MonitorService CreateService()
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<MonitorService>();
        var options = new DbContextOptionsBuilder<BoberDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDatabase")
            .Options;
        var context = new BoberDbContext(options);
        var database = new MonitorDataService(context);

        return new MonitorService(logger, database);
    }
}

