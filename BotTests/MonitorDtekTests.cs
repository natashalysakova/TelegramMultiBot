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

[TestClass]
public class  ImageGenerationTests
{
    [TestMethod]
    [DataRow("https://www.dtek-kem.com.ua/ua/shutdowns", "kem.img.png")]
    [DataRow("https://www.dtek-krem.com.ua/ua/shutdowns", "krem.img.png")]
    public async Task Image_RealScheduleSingleGroupImageReady(string url, string fileName)
    {
        var parser = new ScheduleParser();

        var schedule = await parser.Parse(url);

        var image = await ScheduleImageGenerator.GenerateRealScheduleSingleGroupImage(schedule.ElementAt(6));

        File.WriteAllBytes(fileName, image);
    }

    [TestMethod]
    [DataRow("https://www.dtek-kem.com.ua/ua/shutdowns", "kem.all.png")]
    [DataRow("https://www.dtek-krem.com.ua/ua/shutdowns", "krem.all.png")]
    public async Task Image_GenerateAllGroupsRealScheduleImageReady(string url, string fileName)
    {
        var parser = new ScheduleParser();

        var schedule = await parser.Parse(url);

        var image = await ScheduleImageGenerator.GenerateAllGroupsRealSchedule(schedule);

        File.WriteAllBytes(fileName, image);
    }

    [TestMethod]
    [DataRow("https://www.dtek-kem.com.ua/ua/shutdowns", "kem.single.png")]
    [DataRow("https://www.dtek-krem.com.ua/ua/shutdowns", "krem.single.png")]
    public async Task Image_GeneratePlannedScheduleSingleGroupImageReady(string url, string fileName)
    {
        var parser = new ScheduleParser();

        var schedule = await parser.Parse(url);

        var image = await ScheduleImageGenerator.GeneratePlannedScheduleSingleGroup(schedule.First());

        File.WriteAllBytes(fileName, image);
    }

}

