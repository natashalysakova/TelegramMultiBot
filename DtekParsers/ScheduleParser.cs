using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using TelegramMultiBot.Database.Interfaces;
using HtmlAgilityPack;
using TelegramMultiBot.Database.DTO;

namespace DtekParsers;

public class ScheduleParser
{
    private readonly ISqlConfiguationService _configuationService;
    private readonly ILogger<ScheduleParser>? _logger;

    public ScheduleParser(ISqlConfiguationService configuationService, ILogger<ScheduleParser>? logger = null)
    {
        _configuationService = configuationService;
        _logger = logger;
    }

    public async Task<Schedule> Parse(string url)
    {
        var html = await GetHtmlUsingPuppeteer(url);

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
            var city = property.Name;
            var streetArray = property.Value as JArray;

            if (streetArray == null) 
                continue;   

            // Add to result with city prefix
            foreach (var street in streetArray)
            {
                var resultKey = city;
                result[resultKey] = streetArray.Select(s => s.ToString()).ToList();
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

    private JObject GetJsonFromScriptVariables(string html, string variable)
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

        try
        {
            var groupsLine = section.First(line => line.StartsWith(variable)).Replace($"{variable} = ", "");
            
            // Try to parse as JToken first to handle both objects and arrays
            var token = JToken.Parse(groupsLine);
            
            // If it's already a JObject, return it
            if (token is JObject jobject)
            {
                return jobject;
            }
            
            // If it's a JArray, wrap it in a JObject with a default property
            if (token is JArray jarray)
            {
                return new JObject
                {
                    ["м. Київ"] = jarray   
                };  
            }
            
            // For any other token type, try to convert to JObject
            return JObject.FromObject(token);
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex, 
                "Failed to parse JSON from variable {variable}: {message}. {html}", 
                variable, 
                ex.Message, 
                html);

            throw new ParseException($"Failed to parse JSON from variable {variable}: {ex.Message}");
        }
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
                GroupNumber = ExtractNumber(group.Value.ToString()),
            };
            result.Add(groupSchedule);
        }
        result = result.OrderBy(g => g.GroupNumber).ToList();
        return result;
    }

    private double ExtractNumber(string groupName)
    {
        if (string.IsNullOrEmpty(groupName))
            return 0;
            
        var numbers = new StringBuilder();
        foreach (char c in groupName)
        {
            if (char.IsDigit(c) || c == '.')
            {
                numbers.Append(c);
            }
        }
        
        return numbers.Length > 0 ? double.Parse(numbers.ToString()) : 0;
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

    public async Task<string> GetHtmlUsingPuppeteer(string url)
    {
        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();

        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args = new[]
            {
                "--disable-blink-features=AutomationControlled",
                "--disable-dev-shm-usage",
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-infobars",
                "--disable-web-security",
                "--disable-features=IsolateOrigins,site-per-process",
                "--window-size=1920,1080",
                "--start-maximized",
                "--lang=en-US"
            },
        });

        await using var page = await browser.NewPageAsync();
        
        // Set more realistic user agent (Chrome on Windows)
        await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        
        // Set extra HTTP headers
        await page.SetExtraHttpHeadersAsync(new Dictionary<string, string>
        {
            { "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8" },
            { "Accept-Language", "en-US,en;q=0.9" },
            { "Accept-Encoding", "gzip, deflate, br" },
            { "Sec-Ch-Ua", "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\"" },
            { "Sec-Ch-Ua-Mobile", "?0" },
            { "Sec-Ch-Ua-Platform", "\"Windows\"" },
            { "Sec-Fetch-Dest", "document" },
            { "Sec-Fetch-Mode", "navigate" },
            { "Sec-Fetch-Site", "none" },
            { "Sec-Fetch-User", "?1" },
            { "Upgrade-Insecure-Requests", "1" }
        });

        await page.SetViewportAsync(new ViewPortOptions
        {
            Width = 1920,
            Height = 1080,
        });

        // Evaluate anti-detection scripts before navigation
        await page.EvaluateExpressionOnNewDocumentAsync(@"
            // Override the navigator.webdriver property
            Object.defineProperty(navigator, 'webdriver', {
                get: () => false
            });

            // Override the navigator.plugins to add some
            Object.defineProperty(navigator, 'plugins', {
                get: () => [1, 2, 3, 4, 5]
            });

            // Override navigator.languages
            Object.defineProperty(navigator, 'languages', {
                get: () => ['en-US', 'en']
            });

            // Override permissions
            const originalQuery = window.navigator.permissions.query;
            window.navigator.permissions.query = (parameters) => (
                parameters.name === 'notifications' ?
                    Promise.resolve({ state: Notification.permission }) :
                    originalQuery(parameters)
            );

            // Mock chrome object
            window.chrome = {
                runtime: {}
            };

            // Override toString of these functions to hide modifications
            Object.defineProperty(navigator.webdriver, 'toString', {
                value: () => 'false'
            });
        ");

        // Navigate to the page and let Incapsula challenge run
        await page.GoToAsync(url, new NavigationOptions 
        { 
            WaitUntil = new[] { WaitUntilNavigation.Networkidle0 },
            Timeout = 60000
        });

        // Wait for Incapsula challenge to complete and page to fully load
        // Incapsula typically needs 5-10 seconds to validate the browser
        await Task.Delay(8000);

        // Check if still on Incapsula challenge page and wait more if needed
        var currentContent = await page.GetContentAsync();
        if (currentContent.Contains("/_Incapsula_Resource") || currentContent.Contains("incap_ses"))
        {
            _logger?.LogInformation("Incapsula challenge detected, waiting for completion...");
            await Task.Delay(7000);
            
            // Simulate human-like behavior: scroll down slightly
            await page.EvaluateExpressionAsync("window.scrollTo(0, 52)");
            await Task.Delay(1000);
        }

        // Get the final page content after challenge completion
        var content = await page.GetContentAsync();

        // Log cookies for debugging
        var cookies = await page.GetCookiesAsync();
        var incapCookies = cookies.Where(c => c.Name.StartsWith("incap_ses")).ToList();
        if (incapCookies.Any())
        {
            _logger?.LogInformation($"Successfully obtained Incapsula cookies: {string.Join(", ", incapCookies.Select(c => $"{c.Name}={c.Value}"))}");
        }
        else
        {
            _logger?.LogWarning("No Incapsula session cookies found");
        }

        HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", incapCookies.Select(c => $"{c.Name}={c.Value}")));
        
        _configuationService.SvitlobotSettings.SetCookie(url, string.Join("; ", incapCookies.Select(c => $"{c.Name}={c.Value}")));

        var str = await client.GetStringAsync(url); // Make a request to establish session with Incapsula

        return str;
    }

    private static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
    {
        // Unix timestamp is seconds past epoch
        DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
        return dateTime;
    }
}

public static class SvitlobotSettingsExtensions
{
    public static void SetCookie(this SvitlobotSettings settings, string url, string cookie)
    {
        var region = LocationNameUtility.GetRegionByUrl(url);
        switch (region)
        {
            case "krem":
                settings.KremCookie = cookie;
                break;
            case "kem":
                settings.KemCookie = cookie;
                break;
            case "oem":
                settings.OemCookie = cookie;
                break;
            default:
                throw new ArgumentException($"Unknown region '{region}' for URL '{url}'.", nameof(url));
        }
    }
}