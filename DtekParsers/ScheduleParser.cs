using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace DtekParsers;

public class ScheduleParser
{
    public async Task<Schedule> Parse(string url)
    {
        var html = await GetHtmlFromUrl(url);

        var location = GetLocationByUrl(url);

        var realVariableJobject = GetJsonFromScriptVariables(html, "DisconSchedule.fact");
        var presetVariableJobject = GetJsonFromScriptVariables(html, "DisconSchedule.preset");

        var schedule = new Schedule();

        schedule.TimeZones = GetTimeZones(presetVariableJobject);
        schedule.Groups = GetGroups(presetVariableJobject, location);

        FillRealSchedule(schedule, realVariableJobject);
        FillPlannedSchedule(schedule, presetVariableJobject);

        var updatedFact = DateTime.ParseExact(realVariableJobject["update"].ToString(), "dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);

        var udpatedPreset = DateTime.ParseExact(presetVariableJobject["updateFact"].ToString(), "dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);


        return schedule;
    }

    private string GetLocationByUrl(string url)
    {
        var location = url switch
        {
            "https://www.dtek-kem.com.ua/ua/shutdowns" => "м.Київ",
            "https://www.dtek-krem.com.ua/ua/shutdowns" => "Київська область",
            _ => throw new NotImplementedException(),
        };
        return location;
    }

    private void FillPlannedSchedule(Schedule schedule, JObject presetVariableJobject)
    {
        if (presetVariableJobject is null)
        {
            throw new ParseException("Planned schedule JSON is null");
        }

        var groupsData = presetVariableJobject["data"]?.Children();
        var updated = GetUpdateTime(presetVariableJobject, "updateFact");

        for (int i = 0; i < 7; i++)
        {
            schedule.PlannedSchedule.Add(new PlannedSchedule()
            {
                DayNumber = i,
                Statuses = GetScheduleStatuses(groupsData, i),
                Updated = updated,
            });
        }
    }


    private void FillRealSchedule(Schedule schedule, JObject? factScheduleJson)
    {
        if (factScheduleJson is null)
        {
            throw new ParseException("Fact schedule JSON is null");
        }

        var daysNodes = factScheduleJson["data"]?.Children();
        var updated = GetUpdateTime(factScheduleJson, "update");

        foreach (JProperty day in daysNodes)
        {
            var dayTimestamp = long.Parse(day.Name);
            var groupShedules = day.First.Children();

            schedule.RealSchedule.Add(new RealSchedule()
            {
                DateTimeStamp = dayTimestamp,
                Date = UnixTimeStampToDateTime(dayTimestamp),
                Statuses = GetScheduleStatuses(groupShedules),
                Updated = updated,
            });
        }
    }

    private static DateTime GetUpdateTime(JObject factScheduleJson, string nodeName)
    {
        var stringValue = factScheduleJson[nodeName]?.Value<string>();
        return DateTime.ParseExact(stringValue, "dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static JObject GetJsonFromScriptVariables(string html, string variable)
    {
        var section = GetLastScriptSectionContaining(html, "DisconSchedule.fact");
        if (section == null)
            throw new ParseException("Could not find script section containing DisconSchedule.fact");

        var groupsLine = section.First(line => line.StartsWith(variable)).Replace($"{variable} = ", "");
        return JObject.Parse(groupsLine);
    }

    private List<ScheduleGroup> GetGroups(JObject presetJson, string location)
    {

        var result = new List<ScheduleGroup>();

        var groupNodes = presetJson["sch_names"]?.Children();

        foreach (JProperty group in groupNodes)
        {
            var groupSchedule = new ScheduleGroup()
            {
                Id = group.Name,
                GroupName = group.Value.ToString()
            };
            result.Add(groupSchedule);
        }

        return result;
    }


    private Dictionary<string, IEnumerable<LightStatus>> GetScheduleStatuses(IEnumerable<JToken> groupSchedule, int day)
    {
        var result = new Dictionary<string, IEnumerable<LightStatus>>();
        foreach (JProperty item in groupSchedule)
        {
            var name = item.Name;
            var s1 = item.Value.Values().ElementAt(day);

            List<LightStatus> statuses = new List<LightStatus>();
            foreach (JProperty status in s1)
            {
                statuses.Add(new LightStatus()
                {
                    Id = int.Parse(status.Name),
                    Status = Enum.Parse<ScheduleStatus>(status.Value.ToString()),
                });
            }
            result.Add(name, statuses);
        }

        return result;
    }

    private Dictionary<string, IEnumerable<LightStatus>> GetScheduleStatuses(IEnumerable<JToken> groupSchedule)
    {
        var result = new Dictionary<string, IEnumerable<LightStatus>>();
        foreach (JProperty item in groupSchedule)
        {
            var name = item.Name;
            var statuses = new List<LightStatus>();
            var s1 = item.Values();
            for (int i = 0; i < s1.Count(); i++)
            {
                var status = s1.ElementAt(i) as JProperty;
                statuses.Add(new LightStatus()
                {
                    Id = int.Parse(status.Name),
                    Status = Enum.Parse<ScheduleStatus>(status.Value.ToString()),
                });
            }
            result.Add(name, statuses);
        }

        return result;
    }

    private List<ScheduleTimeZone> GetTimeZones(JObject presetJson)
    {
        var result = new List<ScheduleTimeZone>();

        var timeZonesNodes = presetJson["time_zone"]?.Children();

        if (timeZonesNodes is null)
        {
            throw new ParseException("Time zones nodes are null");
        }

        foreach (JProperty timeZone in timeZonesNodes)
        {
            if (timeZone.Value is null)
            {
                throw new ParseException($"Time zone {timeZone.Name} value is null");
            }
            if (timeZone.Value.Count() != 3)
            {
                throw new ParseException($"Time zone {timeZone.Name} has wrong number of elements {timeZone.Value.Count()}");
            }

            if (timeZone.Value.Any(v => v is null))
            {
                throw new ParseException($"Time zone {timeZone.Name} has null elements");
            }

            var timeZoneObj = new ScheduleTimeZone()
            {
                Id = timeZone.Name,
                Short = timeZone.Value[0]!.ToString(),
                Start = timeZone.Value[1]!.ToString(),
                End = timeZone.Value[2]!.ToString(),
            };
            result.Add(timeZoneObj);
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


