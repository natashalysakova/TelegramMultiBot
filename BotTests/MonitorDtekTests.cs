using DtekParsers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TelegramMultiBot.BackgroundServies;
using TelegramMultiBot.Database;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.ImageCompare;

namespace BotTests;

[TestClass]
public class MonitorDtekTests
{

    [TestMethod]
    public async Task PageLoaded()
    {
        var parser = new ScheduleParser(null!);

        var html = await parser.GetHtmlFromUrl("https://www.dtek-krem.com.ua/ua/shutdowns");

        Assert.IsNotNull(html);
        Assert.Contains("DisconSchedule.fact", html);
        Assert.Contains("DisconSchedule.preset", html);
    }

    [TestMethod]
    [DataRow("https://www.dtek-krem.com.ua/ua/shutdowns", "incap_ses_689_2398465=DycxK/pvEyurbUSPUtKPCbIwc2kAAAAAAe+igeMdMAdgBAD1uSAV6A==")]
    [DataRow("https://www.dtek-kem.com.ua/ua/shutdowns")]
    [DataRow("https://www.dtek-oem.com.ua/ua/shutdowns")]

    public async Task PageParsed(string url, string? cookie = null)
    {
        var settings = new SvitlobotSettings();
        if (cookie != null)
        {
            settings.SetCookie(url, cookie);
        }
        var config = new Mock<ISqlConfiguationService>();
        config.Setup(c => c.SvitlobotSettings).Returns(settings);

        var parser = new ScheduleParser(config.Object);

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
        Assert.IsFalse(string.IsNullOrEmpty(schedule.AttentionNote));
    }

    [TestMethod]
    [DataRow("https://www.dtek-krem.com.ua/ua/shutdowns")]

    public async Task PageParsed_IncapsulaException(string url)
    {
        var settings = new SvitlobotSettings();
        var config = new Mock<ISqlConfiguationService>();
        config.Setup(c => c.SvitlobotSettings).Returns(settings);

        var parser = new ScheduleParser(config.Object);

        await Assert.ThrowsExactlyAsync<IncapsulaException>(async () => await parser.Parse(url));
    }
}
