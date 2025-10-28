
using HtmlAgilityPack;
using PuppeteerSharp;
using System.Threading.Tasks;

namespace DtekParsers;

public class ScheduleImageGenerator
{
    private const int BASE_WIDTH = 1000;
    private const int ROW_HEIGHT = 35;
    private const int SCALE_FACTOR = 2;
    private const int HEADER_HEIGHT = 175;

    public static async Task<IEnumerable<byte[]>> GenerateRealScheduleSingleGroupImages(Schedule schedule)
    {
        var htmls = new List<string>();
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
            htmls.Add(html);
        }

        var maxRows = schedule.RealSchedule.Count;
        var images = await GetHtmlImage(htmls, maxRows);
        return images;
    }

    public static async Task<IEnumerable<byte[]>> GeneratePlannedScheduleSingleGroupImages(Schedule schedule)
    {
        var htmls = new List<string>();
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
            htmls.Add(html);
        }

        var maxRows = schedule.RealSchedule.Count;
        var images = await GetHtmlImage(htmls, maxRows);
        return images;
    }

    public static async Task<IEnumerable<byte[]>> GenerateAllGroupsRealSchedule(Schedule schedule)
    {
        var htmls = new List<string>();
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
            htmls.Add(html);

        }

        var maxRows = schedule.Groups.Count;
        var images = await GetHtmlImage(htmls, maxRows);
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

    private static async Task<IEnumerable<byte[]>> GetHtmlImage(IEnumerable<string> html, int rowNumber)
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
                var viewPortOptions = new ViewPortOptions
                {
                    Width = BASE_WIDTH,
                    Height = (HEADER_HEIGHT + (rowNumber * ROW_HEIGHT)) * SCALE_FACTOR,
                    DeviceScaleFactor = 1.5
                };

                await page.SetViewportAsync(viewPortOptions);

                var images = new List<byte[]>();
                foreach (var part in html)
                {
                    await page.SetContentAsync(part);
                    var selector = await page.WaitForSelectorAsync("#body");
                    images.Add(await selector.ScreenshotDataAsync(new ElementScreenshotOptions
                    {
                        Type = ScreenshotType.Png,
                        CaptureBeyondViewport = true,
                    }));
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
}