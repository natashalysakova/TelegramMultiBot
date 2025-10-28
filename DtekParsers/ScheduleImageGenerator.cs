
using HtmlAgilityPack;
using PuppeteerSharp;
using System.Collections;
using System.Threading.Tasks;

namespace DtekParsers;

public class ScheduleImageGenerator
{
    private const int BASE_WIDTH = 1000;
    private const int ROW_HEIGHT = 35;
    private const int SCALE_FACTOR = 2;
    private const int HEADER_HEIGHT = 175;

    public class ImageRenderRquqest
    {
        public string? Group { get; set; }
        public string HtmlContent { get; set; } = string.Empty;
        public int RowNumber { get; set; }
        public long Date { get; set; }
        public bool Planned { get; internal set; }
    }

    public static async Task<IEnumerable<ImageGenerationResult>> GenerateRealScheduleSingleGroupImages(Schedule schedule)
    {
        var requests = new List<ImageRenderRquqest>();
        foreach (var group in schedule.Groups)
        {
            Dictionary<string, IEnumerable<LightStatus>> items = new();
            foreach (var day in schedule.RealSchedule)
            {
                items.Add(day.DateHeader, day.Statuses[group.Id]);
            }

            var html = await GenerateScheduleBody(
                $"Графік відключень {schedule.Location} {group.GroupName}",
                schedule.TimeZones,
                schedule.RealSchedule.Max(x=>x.Updated),
                items);
            requests.Add(new ImageRenderRquqest
            {
                Group = group.Id,
                HtmlContent = html,
                RowNumber = schedule.RealSchedule.Count,
                Date = schedule.RealSchedule.Max(x => x.DateTimeStamp)
            });
        }
        var images = await GetHtmlImage(requests);
        return images;
    }

    public static async Task<IEnumerable<ImageGenerationResult>> GeneratePlannedScheduleSingleGroupImages(Schedule schedule)
    {
        var requests = new List<ImageRenderRquqest>();
        foreach (var group in schedule.Groups)
        {
            Dictionary<string, IEnumerable<LightStatus>> items = new();
            foreach (var day in schedule.PlannedSchedule)
            {
                items.Add(day.DateHeader, day.Statuses[group.Id]);
            }

            var html = await GenerateScheduleBody(
                $"Графік можливих відключень {schedule.Location} {group.GroupName}",
                schedule.TimeZones,
                schedule.PlannedSchedule.Max(x => x.Updated),
                items);
            requests.Add(new ImageRenderRquqest
            {
                Group = group.Id,
                HtmlContent = html,
                RowNumber = schedule.RealSchedule.Count,
                Date = schedule.RealSchedule.Max(x => x.DateTimeStamp),
                Planned = true
            });
        }

        var images = await GetHtmlImage(requests);
        return images;
    }

    public static async Task<IEnumerable<ImageGenerationResult>> GenerateAllGroupsRealSchedule(Schedule schedule)
    {
        var requests = new List<ImageRenderRquqest>();
        foreach (var day in schedule.RealSchedule)
        {
            Dictionary<string, IEnumerable<LightStatus>> items = new();
            foreach (var group in schedule.Groups)
            {
                items.Add(group.GroupName, day.Statuses[group.Id]);
            }

            var html = await GenerateScheduleBody(
                $"Графік відключень {day.DateHeader} {schedule.Location}",
                schedule.TimeZones,
                day.Updated,
                items);
            requests.Add(new ImageRenderRquqest
            {
                HtmlContent = html,
                RowNumber = schedule.RealSchedule.Count,
                Date = day.DateTimeStamp
            });
        }

        var images = await GetHtmlImage(requests);
        return images;
    }

    private static async Task<string> GenerateScheduleBody(
        string title,
        List<ScheduleTimeZone> timeZones,
        DateTime updated,
        Dictionary<string,
        IEnumerable<LightStatus>> schedule)
    {
        var doc = new HtmlDocument();

        doc.Load("Templates/template.html");

        var titleNode = doc.GetElementbyId("schedule-header");
        titleNode.InnerHtml = title;

        var headerRow = doc.GetElementbyId("header-row");
        headerRow.ChildNodes.Clear();
        headerRow.AppendChild(HtmlNode.CreateNode($"<td class=\"first-column\">Часові проміжки</td>"));
        foreach (var timezone in timeZones)
        {
            headerRow.AppendChild(HtmlNode.CreateNode($"<th class=\"timezone\">{timezone.Short}</th>"));
        }

        var tableBody = doc.GetElementbyId("schedule-body");
        tableBody.ChildNodes.Clear();
        foreach (var line in schedule)
        {
            var dayRowNode = HtmlNode.CreateNode("<tr></tr>");
            dayRowNode.AppendChild(HtmlNode.CreateNode($"<td class=\"first-column\">{line.Key}</td>"));

            foreach (var item in line.Value.OrderBy(x => x.Id))
            {
                dayRowNode.AppendChild(HtmlNode.CreateNode($"<td class=\"{item.Status}\"></td>"));
            }

            tableBody.AppendChild(dayRowNode);
        }

        HtmlNode legendToRemove;
        if (schedule.Any(x => x.Value.Any(y => y.Status != ScheduleStatus.maybe)))
        {
            legendToRemove = doc.GetElementbyId("legend-planned");
        }
        else
        {
            legendToRemove = doc.GetElementbyId("legend-real");
        }
        legendToRemove.Remove();

        var updatedInfo = doc.GetElementbyId("updated-info");
        updatedInfo.InnerHtml = $"Оновлено: {updated.ToString("dd.MM.yyyy HH:mm")}";

        //var fileName = $"{groupName}_{updated.ToString("ddMMyyyyHHmmss")}_{name_suffix}.html";
        return doc.DocumentNode.OuterHtml;
    }

    private static async Task<List<ImageGenerationResult>> GetHtmlImage(List<ImageRenderRquqest> requests)
    {
        int retry = 0;
        var maxretry = 3;
        do
        {
            try
            {
                var browserFetcher = new BrowserFetcher();
                await browserFetcher.DownloadAsync();

                await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {

                });

                await using var page = await browser.NewPageAsync();
                var rowNumber = requests.Max(x => x.RowNumber);

                var viewPortOptions = new ViewPortOptions
                {
                    Width = BASE_WIDTH,
                    Height = (HEADER_HEIGHT + (rowNumber * ROW_HEIGHT)) * SCALE_FACTOR,
                    DeviceScaleFactor = 1.5
                };

                await page.SetViewportAsync(viewPortOptions);

                var images = new List<ImageGenerationResult>();
                foreach (var renderRequest in requests)
                {
                    await page.SetContentAsync(renderRequest.HtmlContent);

                    var selector = await page.WaitForSelectorAsync("#body");
                    images.Add(new ImageGenerationResult() { 
                        ImageData = await selector.ScreenshotDataAsync(new ElementScreenshotOptions
                        {
                            Type = ScreenshotType.Png,
                            CaptureBeyondViewport = true,
                        }),
                        Group = renderRequest.Group,
                        Date = renderRequest.Date,
                        Planned = renderRequest.Planned
                    });
                }

                return images;
            }
            catch (Exception ex)
            {
                await Task.Delay(1000);
                Console.WriteLine("retry to make an image: " + ex.Message);
                retry += 1;
            }
        } while (retry < maxretry);

        throw new Exception("Cannot make a screenshot of page");
    }

    public static async Task<IEnumerable<ImageGenerationResult>> GenerateAllImages(Schedule schedule)
    {
        var allGroupsRealScheduleImages = await GenerateAllGroupsRealSchedule(schedule);
        var singleGroupRealScheduleImages = await GenerateRealScheduleSingleGroupImages(schedule);
        var singleGroupPlannedScheduleImages = await GeneratePlannedScheduleSingleGroupImages(schedule);

        return allGroupsRealScheduleImages
            .Concat(singleGroupRealScheduleImages)
            .Concat(singleGroupPlannedScheduleImages);
    }

    
}

public class ImageGenerationResult
{
    public required byte[] ImageData { get; set; }
    public string? Group { get; set; }

    public long? Date { get; set; }
    public bool Planned { get; internal set; }
}