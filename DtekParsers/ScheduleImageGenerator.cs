
using HtmlAgilityPack;
using PuppeteerSharp;
using System.Threading.Tasks;

namespace DtekParsers;

public class ScheduleImageGenerator
{
    public static async Task<byte[]> GenerateRealScheduleSingleGroupImage(GroupSchedule groupSchedule)
    {
        //return await GenerateSchedule(
        //    "Графік відключень",
        //    groupSchedule.GroupName,
        //    groupSchedule.Updated,
        //    groupSchedule.RealSchedule.Cast<BaseScheduleDay>().ToList(),
        //    "real");

        var html = await GenerateScheduleBody(
            "Графік відключень",
            groupSchedule.GroupName,
            groupSchedule.RealSchedule.First().Items.Keys.ToList(),
            groupSchedule.Updated,
            groupSchedule.RealSchedule.Cast<BaseScheduleDay>().ToList());

        return await GetHtmlImage(html, ViewportSize.SingleGroup);
    }

    public static async Task<byte[]> GeneratePlannedScheduleSingleGroup(GroupSchedule groupSchedule)
    {
        //return await GenerateSchedule(
        //    "Графік можливих відключень",
        //    groupSchedule.GroupName,
        //    groupSchedule.Updated,
        //    groupSchedule.PlannedSchedule.Cast<BaseScheduleDay>().ToList(),
        //    "planned");
        var html = await GenerateScheduleBody(
            "Графік можливих відключень",
            groupSchedule.GroupName,
            groupSchedule.PlannedSchedule.First().Items.Keys.ToList(),
            groupSchedule.Updated,
            groupSchedule.PlannedSchedule.Cast<BaseScheduleDay>().ToList());

        return await GetHtmlImage(html, ViewportSize.AllGroups);

    }

    public static async Task<byte[]> GenerateAllGroupsRealSchedule(List<GroupSchedule> groupSchedule)
    {
        var html = await GenerateScheduleBody(
            "Графік відключень по групах",
            "Часові проміжки",
            groupSchedule.First().RealSchedule.First().Items.Keys.ToList(),
            groupSchedule.Max(x => x.Updated),
            groupSchedule.Select(x=> x.RealSchedule.First()).Cast<BaseScheduleDay>().ToList(), true);

        return await GetHtmlImage(html, ViewportSize.AllGroups);
    }

    private static async Task<string> GenerateScheduleBody(string title, string firstColumnTitle, List<ScheduleTimeZone> timeZones, DateTime updated, List<BaseScheduleDay> schedule, bool useGroupHeader = false)
    {
        var doc = new HtmlDocument();

        doc.Load("Templates/template.html");

        var titleNode = doc.GetElementbyId("schedule-header");
        titleNode.InnerHtml = title;

        var headerRow = doc.GetElementbyId("header-row");
        headerRow.ChildNodes.Clear();
        headerRow.AppendChild(HtmlNode.CreateNode($"<td class=\"first-column\">{firstColumnTitle}</td>"));
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

        var updatedInfo = doc.GetElementbyId("updated-info");
        updatedInfo.InnerHtml = $"Оновлено: {updated.ToString("dd.MM.yyyy HH:mm")}";

        //var fileName = $"{groupName}_{updated.ToString("ddMMyyyyHHmmss")}_{name_suffix}.html";
        return doc.DocumentNode.OuterHtml;
    }

    private static async Task<byte[]> GetHtmlImage(string html, ViewportSize viewportSize)
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

                var viewPortOptions = viewportSize == ViewportSize.SingleGroup
                    ? new ViewPortOptions
                    {
                        Width = 2500,
                        Height = 440,
                    }
                    : new ViewPortOptions
                    {
                        Width = 1500,
                        Height = 760,
                    };

                await page.SetViewportAsync(viewPortOptions);
                await page.SetContentAsync(html);
                await page.EvaluateFunctionAsync("() => document.body.style.zoom = 2");
                return await page.ScreenshotDataAsync(new ScreenshotOptions
                {
                    Type = ScreenshotType.Png
                });
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

enum ViewportSize
{
    SingleGroup,
    AllGroups
}