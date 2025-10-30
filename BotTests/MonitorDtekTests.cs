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
}

