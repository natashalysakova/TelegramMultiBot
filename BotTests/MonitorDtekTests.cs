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
        Assert.Contains("DisconSchedule.fact", html);
        Assert.Contains("DisconSchedule.preset", html);
    }

    [TestMethod]
    [DataRow("https://www.dtek-krem.com.ua/ua/shutdowns")]
    [DataRow("https://www.dtek-kem.com.ua/ua/shutdowns")]
    [DataRow("https://www.dtek-oem.com.ua/ua/shutdowns")]

    public async Task PageParsed(string url)
    {
        var parser = new ScheduleParser();

        var schedule = await parser.Parse(url);

        Assert.IsNotNull(schedule);
        Assert.IsNotEmpty(schedule.Groups);
        Assert.HasCount(24, schedule.TimeZones);
        Assert.HasCount(7, schedule.PlannedSchedule);
        foreach (var item in schedule.PlannedSchedule)
        {
            Assert.HasCount(12, item.Statuses);
        }
        Assert.HasCount(2, schedule.RealSchedule);
        foreach (var item in schedule.RealSchedule)
        {
            Assert.HasCount(12, item.Statuses);
        }

    }
}

