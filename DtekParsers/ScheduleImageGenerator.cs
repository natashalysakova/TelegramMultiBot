
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

    public static async Task<byte[]> GenerateRealScheduleSingleGroupImage(GroupSchedule groupSchedule)
    {
        var html = await GenerateScheduleBody(
            $"Графік відключень {groupSchedule.Location} {groupSchedule.GroupName}",
            groupSchedule.RealSchedule.First().Items.Keys.ToList(),
            groupSchedule.Updated,
            groupSchedule.RealSchedule.Cast<BaseScheduleDay>().ToList());

        var images = await GetHtmlImage([html], groupSchedule.RealSchedule.Count);
        return images.First();
    }

    public static async Task<byte[]> GeneratePlannedScheduleSingleGroup(GroupSchedule groupSchedule)
    {
        var html = await GenerateScheduleBody(
            $"Графік можливих відключень {groupSchedule.Location} {groupSchedule.GroupName}",
            groupSchedule.PlannedSchedule.First().Items.Keys.ToList(),
            groupSchedule.Updated,
            groupSchedule.PlannedSchedule.Cast<BaseScheduleDay>().ToList());

        var images = await GetHtmlImage([html], groupSchedule.PlannedSchedule.Count);
        return images.First();

    }

    public static async Task<byte[]> GenerateAllGroupsRealSchedule(List<GroupSchedule> groupSchedule)
    {
        var group = groupSchedule.First();

        var html = await GenerateScheduleBody(
            $"Графік відключень {group.RealSchedule.First().DateHeader} {groupSchedule.First().Location}",
            group.RealSchedule.First().Items.Keys.ToList(),
            groupSchedule.Max(x => x.Updated),
            groupSchedule.Select(x => x.RealSchedule.First()).Cast<BaseScheduleDay>().ToList(), true);

        var images = await GetHtmlImage([html], groupSchedule.Count);
        return images.First();
    }

    private static async Task<string> GenerateScheduleBody(string title, List<ScheduleTimeZone> timeZones, DateTime updated, List<BaseScheduleDay> schedule, bool useGroupHeader = false)
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
            if (useGroupHeader)
            {
                dayRowNode.AppendChild(HtmlNode.CreateNode($"<td class=\"first-column\">{line.Group}</td>"));
            }
            else
            {
                dayRowNode.AppendChild(HtmlNode.CreateNode($"<td class=\"first-column\">{line.DateHeader}</td>"));
            }

            foreach (var item in line.Items.OrderBy(x => int.Parse(x.Key.Id)))
            {
                dayRowNode.AppendChild(HtmlNode.CreateNode($"<td class=\"{item.Value}\"></td>"));
            }

            tableBody.AppendChild(dayRowNode);
        }

        HtmlNode legendToRemove;
        if (schedule.Any(x => x.Items.Any(y => y.Value != ScheduleStatus.maybe)))
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

    private static async Task<IEnumerable<byte[]>> GetHtmlImage(string[] html, int rowNumber)
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