using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TelegramMultiBot.Database;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.ImageCompare;
using static System.Collections.Specialized.BitVector32;

namespace BotTests.Properties;

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

class ScheduleParser
{
    public async Task<List<GroupSchedule>> Parse(string url)
    {
        var html = await GetHtmlFromUrl(url);
        

        var section = GetLastScriptSectionContaining(html, "DisconSchedule.fact");
        if (section == null)
            throw new Exception("Could not find script section containing DisconSchedule.fact");

        var timeZones = GetTimeZones(section);
        var gropus = GetGroups(section);

        FillRealSchedule(gropus, timeZones, section);
        //FillPlannedSchedule(gropus, section);


        return gropus;
    }

    private List<GroupSchedule> GetGroups(string[] scripSectionContent)
    {
        var groupsLine = scripSectionContent.First(line => line.StartsWith("DisconSchedule.preset")).Replace("DisconSchedule.preset = ", "");
        var groupsJson = JObject.Parse(groupsLine);

        var result = new List<GroupSchedule>();

        var groupNodes = groupsJson["sch_names"]?.Children();

        foreach (JProperty group in groupNodes)
        {
            var groupSchedule = new GroupSchedule()
            {
                Id = group.Name,
                GroupName = group.Value.ToString(),
            };
            result.Add(groupSchedule);
        }

        return result;
    }

    private void FillRealSchedule(List<GroupSchedule> groups, Dictionary<string, ScheduleTimeZone> timezones, string[] scripSectionContent)
    {
        var factScheduleLine = scripSectionContent.First(line => line.StartsWith("DisconSchedule.fact")).Replace("DisconSchedule.fact = ", "");
        var factScheduleJson = JObject.Parse(factScheduleLine);


        var daysNodes = factScheduleJson["data"]?.Children();

        foreach (JProperty day in daysNodes)
        {
            var groupShedules = day.First.Children();
            var dayTimestamp = long.Parse(day.Name);
            foreach (JProperty group in groupShedules)
            {
                var groupName = group.Name;

                var schedule = groups.First(x => x.Id == groupName);

                var scheduleNode = group[groupName];

                schedule.RealSchedule.Add(new RealScheduleDay()
                {
                    Date = UnixTimeStampToDateTime(dayTimestamp),
                    Items = GetScheduleStatuses(group.First, timezones)
                });
            }
        }


    }

    private Dictionary<ScheduleTimeZone, ScheduleStatus> GetScheduleStatuses(JToken? first, Dictionary<string, ScheduleTimeZone> timezones)
    {
        throw new NotImplementedException();
    }

    private Dictionary<string, ScheduleTimeZone> GetTimeZones(string[] scripSectionContent)
    {
        var timeZoneLine = scripSectionContent.First(line => line.StartsWith("DisconSchedule.preset")).Replace("DisconSchedule.preset = ", "");
        var timeZoneJson = JObject.Parse(timeZoneLine);

        var result = new Dictionary<string, ScheduleTimeZone>();

        var timeZonesNodes = timeZoneJson["time_zone"]?.Children();

        foreach (JProperty timeZone in timeZonesNodes)
        {
            var timeZoneObj = new ScheduleTimeZone()
            {
                Id = timeZone.Name,
                Short = timeZone.Value[0].ToString(),
                Start = timeZone.Value[1].ToString(),
                End = timeZone.Value[2].ToString(),
            };
            result.Add(timeZone.Name, timeZoneObj);
        }

        return result;
    }

    private static string[]? GetLastScriptSectionContaining(string html, string searchText)
    {
        var scriptStartTag = "<script";
        var scriptEndTag = "</script>";

        string[]? lastMatchingScript = null;
        var startIndex = 0;

        while (true)
        {
            var scriptStart = html.IndexOf(scriptStartTag, startIndex, StringComparison.OrdinalIgnoreCase);
            if (scriptStart == -1) break;

            var scriptContentStart = html.IndexOf('>', scriptStart) + 1;
            var scriptEnd = html.IndexOf(scriptEndTag, scriptContentStart, StringComparison.OrdinalIgnoreCase);

            if (scriptEnd == -1) break;

            var scriptContent = html.Substring(scriptContentStart, scriptEnd - scriptContentStart);

            if (scriptContent.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
                lastMatchingScript = scriptContent.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            }

            startIndex = scriptEnd + scriptEndTag.Length;
        }

        return lastMatchingScript;
    }

    public async Task<string> GetHtmlFromUrl(string url)
    {
        using var client = new HttpClient();
        return await client.GetStringAsync(url);
    }

    private static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
    {
        // Unix timestamp is seconds past epoch
        DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
        return dateTime;
    }
}
class GroupSchedule
{
    public string Id { get; set; }
    public string GroupName { get; set; }
    public List<RealScheduleDay> RealSchedule { get; set; } = new();
    public List<ScheduleDay> PlannedSchedule { get; set; } = new();
}

abstract class BaseScheduleDay
{
    public Dictionary<ScheduleTimeZone, ScheduleStatus> Items { get; set; } = new();
}

class ScheduleDay : BaseScheduleDay
{
    public string DayNumber { get; set; }
}

class RealScheduleDay : BaseScheduleDay
{
    public DateTime Date { get; set; }
}

public class ScheduleTimeZone
{
    public string Id { get; set; }
    public string Short { get; set; }
    public string Start { get; set; }
    public string End { get; set; }
}

public enum ScheduleStatus
{
    yes,
    no,
    maybe,
    first,
    second,
    mfirst,
    msecond
}