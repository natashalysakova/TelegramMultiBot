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
        Assert.IsTrue(schedule.Groups.Count == 12);
        Assert.IsTrue(schedule.TimeZones.Count == 24);
        Assert.IsTrue(schedule.PlannedSchedule.Count == 7);
        foreach (var item in schedule.PlannedSchedule)
        {
            Assert.IsTrue(item.Statuses.Count == 12);
        }
        Assert.IsTrue(schedule.RealSchedule.Count == 2);
        foreach (var item in schedule.RealSchedule)
        {
            Assert.IsTrue(item.Statuses.Count == 12);
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
    [DataRow("https://www.dtek-kem.com.ua/ua/shutdowns", "kem.real")]
    [DataRow("https://www.dtek-krem.com.ua/ua/shutdowns", "krem.real")]
    public async Task Image_RealScheduleSingleGroupImageReady(string url, string folder)
    {
        var parser = new ScheduleParser();

        var schedule = await parser.Parse(url);

        var image = await ScheduleImageGenerator.GenerateRealScheduleSingleGroupImages(schedule);

        SaveImages(folder, image.Select(x=>x.ImageData));

        Assert.AreEqual(12, image.Count());
    }

    private static void SaveImages(string folder, IEnumerable<byte[]> image)
    {
        if(Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }

        Directory.CreateDirectory(folder);

        for (int i = 0; i < image.Count(); i++)
        {
            File.WriteAllBytes(Path.Combine(folder, $"{i}.png"), image.ElementAt(i));
        }
    }

    [TestMethod]
    [DataRow("https://www.dtek-kem.com.ua/ua/shutdowns", "kem.all")]
    [DataRow("https://www.dtek-krem.com.ua/ua/shutdowns", "krem.all")]
    public async Task Image_GenerateAllGroupsRealScheduleImageReady(string url, string folder)
    {
        var parser = new ScheduleParser();

        var schedule = await parser.Parse(url);

        var image = await ScheduleImageGenerator.GenerateAllGroupsRealSchedule(schedule);

        SaveImages(folder, image.Select(x => x.ImageData));

        Assert.AreEqual(2, image.Count());
    }

    [TestMethod]
    [DataRow("https://www.dtek-kem.com.ua/ua/shutdowns", "kem.planned")]
    [DataRow("https://www.dtek-krem.com.ua/ua/shutdowns", "krem.planned")]
    public async Task Image_GeneratePlannedScheduleSingleGroupImageReady(string url, string folder)
    {
        var parser = new ScheduleParser();

        var schedule = await parser.Parse(url);

        var image = await ScheduleImageGenerator.GeneratePlannedScheduleSingleGroupImages(schedule);

        SaveImages(folder, image.Select(x => x.ImageData));

        Assert.AreEqual(12, image.Count());
    }

}

