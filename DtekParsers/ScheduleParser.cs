using Newtonsoft.Json.Linq;

namespace DtekParsers;

public class ScheduleParser
{
    public async Task<byte[]> GetRealSchedule(string group, string url)
    {
        var parsed = await Parse(url);

        var groupSchedule = parsed.FirstOrDefault(x => x.Id == group);

        if (groupSchedule == null)
        {
            throw new ParseException($"Group {group} not found in schedule");
        }

        var imageBytes = await ScheduleImageGenerator.GenerateRealScheduleSingleGroupImage(groupSchedule);

        return imageBytes;
    }


    public async Task<List<GroupSchedule>> Parse(string url)
    {
        var html = await GetHtmlFromUrl(url);

        var location = GetLocationByUrl(url);

        var realVariableJobject = GetJsonFromScriptVariables(html, "DisconSchedule.fact");
        var presetVariableJobject = GetJsonFromScriptVariables(html, "DisconSchedule.preset");

        var timeZones = GetTimeZones(presetVariableJobject);
        var groups = GetGroups(presetVariableJobject, location);

        FillRealSchedule(groups, timeZones, realVariableJobject);
        FillPlannedSchedule(groups, timeZones, presetVariableJobject);

        var updatedFact = DateTime.ParseExact(realVariableJobject["update"].ToString(), "dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);

        var udpatedPreset = DateTime.ParseExact(presetVariableJobject["updateFact"].ToString(), "dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        foreach (var group in groups)
        {
            group.Updated = updatedFact != DateTime.MinValue ? updatedFact : udpatedPreset;
        }

        return groups;
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

    private void FillPlannedSchedule(List<GroupSchedule> groups, Dictionary<string, ScheduleTimeZone> timeZones, JObject presetVariableJobject)
    {
        if (presetVariableJobject is null)
        {
            throw new ParseException("Planned schedule JSON is null");
        }

        var groupsData = presetVariableJobject["data"]?.Children();

        foreach (JProperty group in groupsData)
        {
            foreach (JProperty day in group.Value)
            {
                var schedule = groups.First(x => x.Id == group.Name);

                schedule.PlannedSchedule.Add(new ScheduleDay()
                {
                    DayNumber = int.Parse(day.Name),
                    Items = GetScheduleStatuses(day.Value, timeZones),
                    Group = schedule.GroupName,
                });
            }
        }
    }
    private void FillRealSchedule(List<GroupSchedule> groups, Dictionary<string, ScheduleTimeZone> timezones, JObject? factScheduleJson)
    {
        if (factScheduleJson is null)
        {
            throw new ParseException("Fact schedule JSON is null");
        }

        var daysNodes = factScheduleJson["data"]?.Children();

        foreach (JProperty day in daysNodes)
        {
            var groupShedules = day.First.Children();
            var dayTimestamp = long.Parse(day.Name);
            foreach (JProperty group in groupShedules)
            {
                var schedule = groups.First(x => x.Id == group.Name);

                schedule.RealSchedule.Add(new RealScheduleDay()
                {
                    Date = UnixTimeStampToDateTime(dayTimestamp),
                    Items = GetScheduleStatuses(group.Value, timezones),
                    Group = schedule.GroupName,
                });
            }
        }
    }

    private static JObject GetJsonFromScriptVariables(string html, string variable)
    {
        var section = GetLastScriptSectionContaining(html, "DisconSchedule.fact");
        if (section == null)
            throw new ParseException("Could not find script section containing DisconSchedule.fact");

        var groupsLine = section.First(line => line.StartsWith(variable)).Replace($"{variable} = ", "");
        return JObject.Parse(groupsLine);
    }

    private List<GroupSchedule> GetGroups(JObject presetJson, string location)
    {

        var result = new List<GroupSchedule>();

        var groupNodes = presetJson["sch_names"]?.Children();

        foreach (JProperty group in groupNodes)
        {
            var groupSchedule = new GroupSchedule()
            {
                Id = group.Name,
                GroupName = group.Value.ToString(),
                Updated = DateTime.Now,
                Location = location,
            };
            result.Add(groupSchedule);
        }

        return result;
    }


    private Dictionary<ScheduleTimeZone, ScheduleStatus> GetScheduleStatuses(JToken? groupSchedule, Dictionary<string, ScheduleTimeZone> timezones)
    {
        var result = new Dictionary<ScheduleTimeZone, ScheduleStatus>();
        foreach (JProperty item in groupSchedule)
        {
            var name = item.Name;
            var value = Enum.Parse<ScheduleStatus>(item.Value.ToString());
            result.Add(timezones[name], value);
        }

        return result;
    }

    private Dictionary<string, ScheduleTimeZone> GetTimeZones(JObject presetJson)
    {
        var result = new Dictionary<string, ScheduleTimeZone>();

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


