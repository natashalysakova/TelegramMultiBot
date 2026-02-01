using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using TelegramMultiBot.Database.Interfaces;
using HtmlAgilityPack;

namespace DtekParsers;

public class ScheduleParser
{
    private readonly ISqlConfiguationService _configuationService;

    public ScheduleParser(ISqlConfiguationService configuationService)
    {
        _configuationService = configuationService;
    }

    public async Task<Schedule> Parse(string url)
    {
        var html = await GetHtmlFromUrl(url);

        var location = LocationNameUtility.GetLocationByUrl(url);

        var realVariableJobject = GetJsonFromScriptVariables(html, "DisconSchedule.fact");
        var presetVariableJobject = GetJsonFromScriptVariables(html, "DisconSchedule.preset");

        var streets = GetJsonFromScriptVariables(html, "DisconSchedule.streets");

        var schedule = new Schedule();

        schedule.TimeZones = GetTimeZones(presetVariableJobject);
        schedule.Groups = GetGroups(presetVariableJobject);
        schedule.Streets = GetStreetsInfo(streets);
        schedule.Location = location;
        schedule.AttentionNote = GetAttentionNote(html);


        FillRealSchedule(schedule, realVariableJobject);
        FillPlannedSchedule(schedule, presetVariableJobject);

        var updatedFact = DateTime.ParseExact(realVariableJobject["update"].ToString(), "dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);

        var udpatedPreset = DateTime.ParseExact(presetVariableJobject["updateFact"].ToString(), "dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);

        schedule.Updated = updatedFact >= udpatedPreset ? updatedFact : udpatedPreset;

        foreach (var group in schedule.Groups)
        {
            group.DataSnapshot = GenerateGroupDataSnapshot(schedule.RealSchedule, group.Id);
        }

        return schedule;
    }

    private Dictionary<string, List<string>> GetStreetsInfo(JObject streets)
    {
        var result = new Dictionary<string, List<string>>();

        if (streets == null) 
            return result;
            
        foreach (var property in streets.Properties())
        {
            var groupKey = property.Name;
            var streetArray = property.Value as JArray;
            
            if (streetArray == null) 
                continue;
                
            var cityStreets = new Dictionary<string, List<string>>();
            
            foreach (var streetToken in streetArray)
            {
                var streetName = streetToken.ToString();
                var (city, street) = ParseStreetWithCity(streetName);
                
                if (!cityStreets.ContainsKey(city))
                {
                    cityStreets[city] = new List<string>();
                }
                
                cityStreets[city].Add(street);
            }
            
            // Add to result with city prefix
            foreach (var cityGroup in cityStreets)
            {
                var resultKey = cityGroup.Key == "Київ" ? groupKey : $"{cityGroup.Key}_{groupKey}";
                result[resultKey] = cityGroup.Value;
            }
        }
        
        return result;
    }
    
    private (string city, string street) ParseStreetWithCity(string streetName)
    {
        // Default city is Kyiv (Київ)
        var defaultCity = "Київ";
        
        if (string.IsNullOrEmpty(streetName))
            return (defaultCity, string.Empty);
            
        // Look for patterns like "За межами с. Гнідин" - check this FIRST
        if (streetName.StartsWith("За межами "))
        {
            var cityPart = streetName.Substring("За межами ".Length);
            return (cityPart.Trim(), "За межами");
        }
            
        // Check for city prefixes like "м. Бровари", "с-т Святише", "пров.", etc.
        var cityPrefixes = new[] { "м. ", "с-т ", "с. ", "смт ", "пров. ", "вул. " };
        
        // Look for patterns that indicate a city
        foreach (var prefix in cityPrefixes)
        {
            var index = streetName.IndexOf(prefix);
            if (index >= 0)
            {
                var beforePrefix = streetName.Substring(0, index).Trim();
                var afterPrefix = streetName.Substring(index + prefix.Length).Trim();
                
                // If there's text before the prefix, it's likely a city name
                if (!string.IsNullOrEmpty(beforePrefix) && beforePrefix != "вул" && beforePrefix != "пров")
                {
                    // Extract city name (remove trailing comma or other punctuation)
                    var cityName = beforePrefix.TrimEnd(',', ' ');
                    var streetPart = prefix.TrimEnd() + " " + afterPrefix;
                    return (cityName, streetPart.Trim());
                }
            }
        }
        
        // Look for patterns like "Дарницький р-н, вул. Лугова"
        if (streetName.Contains("р-н, "))
        {
            var parts = streetName.Split(new[] { "р-н, " }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                var district = parts[0].Trim() + " р-н";
                var street = parts[1].Trim();
                return (district, street);
            }
        }
        
        // If no city pattern found, default to Kyiv
        return (defaultCity, streetName);
    }

    private string GetAttentionNote(string html)
    {
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(html);
        
        var attentionNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='m-attention__text']");
        
        if (attentionNode == null)
        {
            return string.Empty;
        }
        
        return attentionNode.InnerText.Trim();
    }

    private string? GenerateGroupDataSnapshot(List<RealSchedule> realSchedule, string groupCode)
    {
        var builder = new StringBuilder();

        foreach (var day in realSchedule)
        {
            if (day.Statuses.ContainsKey(groupCode))
            {
                var groupSchedule = day.Statuses[groupCode];
                if (groupSchedule is not null && groupSchedule.Sum(x=>x.RealHours) > 0)
                {
                    builder.Append($"{day.DateTimeStamp}");

                    foreach (var status in groupSchedule)
                    {
                        builder.Append($"|{status.Id}:{(int)status.Status}");
                    }
                    builder.Append(";");

                }
            }
        }

        return builder.ToString();
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
        {
            var incapsulaSection = html.Contains("Incapsula");
            if(incapsulaSection)
            {
                throw new IncapsulaException("Incapsula protection detected. Cannot parse the schedule.");
            }
        
            throw new ParseException("Could not find script section containing DisconSchedule.fact");
        }

        var groupsLine = section.First(line => line.StartsWith(variable)).Replace($"{variable} = ", "");
        return JObject.Parse(groupsLine);
    }

    private List<ScheduleGroup> GetGroups(JObject presetJson)
    {

        var result = new List<ScheduleGroup>();

        var groupNodes = presetJson["sch_names"]?.Children();

        foreach (JProperty group in groupNodes)
        {
            var groupSchedule = new ScheduleGroup()
            {
                Id = group.Name,
                GroupName = group.Value.ToString(),
                DataSnapshot = string.Empty,
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
                Id = int.Parse(timeZone.Name),
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
        HttpClient client = new HttpClient();
        string content;
        try
        {
            var dtekCookie = GetCookie(url);
            if(!string.IsNullOrEmpty(dtekCookie))
            {
                client.DefaultRequestHeaders.Add("Cookie", dtekCookie);
            }

            var response = await client.GetAsync(url);
            content = await response.Content.ReadAsStringAsync();

          }
        catch (Exception ex)
        {
            throw new ParseException($"Failed to fetch HTML: {ex.Message}");
        }

        if (content.Contains("META NAME=\"robots\" CONTENT=\"noindex,nofollow\"", StringComparison.OrdinalIgnoreCase))
        {
            throw new IncapsulaException("Incapsula protection detected. Cannot parse the schedule.");
        }

        return content;
    }

    private string? GetCookie(string url)
    {
        if (_configuationService == null)
            return null;

        var location = LocationNameUtility.GetRegionByUrl(url);
        switch (location)
        {
            case "kem": return _configuationService.SvitlobotSettings.KemCookie;
            case "krem": return _configuationService.SvitlobotSettings.KremCookie;

            default:
                return null;
        }

    }

    private static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
    {
        // Unix timestamp is seconds past epoch
        DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
        return dateTime;
    }
}