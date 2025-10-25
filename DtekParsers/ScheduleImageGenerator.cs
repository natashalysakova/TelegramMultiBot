
using HtmlAgilityPack;
using PuppeteerSharp;
using System.Threading.Tasks;

namespace DtekParsers;

public class ScheduleImageGenerator
{
    public static async Task<byte[]> GenerateRealScheduleSingleGroupImage(GroupSchedule groupSchedule)
    {
        return await GenerateSchedule(
            groupSchedule.GroupName,
            groupSchedule.Updated,
            groupSchedule.RealSchedule.Cast<BaseScheduleDay>().ToList(),
            "real");
    }

    public static async Task<byte[]> GeneratePlannedScheduleSingleGroupImage(GroupSchedule groupSchedule)
    {
        return await GenerateSchedule(
            groupSchedule.GroupName,
            groupSchedule.Updated,
            groupSchedule.RealSchedule.Cast<BaseScheduleDay>().ToList(),
            "planned");
    }

    public static async Task<byte[]> GenerateAllGroupsRealSchedule(List<GroupSchedule> groupSchedule)
    {
        var doc = new HtmlDocument();

        doc.Load("Templates/template.html");

        var headerRow = doc.GetElementbyId("header-row");

        foreach (var timezone in groupSchedule.First().RealSchedule.SelectMany(x => x.Items.Keys))
        {
            headerRow.AppendChild(HtmlNode.CreateNode($"<th class=\"timezone\">{timezone.Short}</th>"));
        }

        var tableBody = doc.GetElementbyId("schedule-body");

        foreach (var day in groupSchedule)
        {
            var dayRowNode = HtmlNode.CreateNode("<tr></tr>");
            dayRowNode.AppendChild(HtmlNode.CreateNode($"<td class=\"first-column\">{day.GroupName}</td>"));

            foreach (var item in day.RealSchedule.First().Items.OrderBy(x => x.Key.Id))
            {
                dayRowNode.AppendChild(HtmlNode.CreateNode($"<td class=\"{item.Value}\"></td>"));
            }

            tableBody.AppendChild(dayRowNode);
        }

        var fileName = $"all_{DateTime.Now.ToString("ddMMyyyyHHmmss")}_real.html";
        var html = doc.ToString();

        return await GetHtmlImage(html);
    }

    private static async Task<byte[]> GenerateSchedule(string groupName, DateTime updated, List<BaseScheduleDay> scheduleDays, string name_suffix)
    {
        var doc = new HtmlDocument();

        doc.Load("Templates/template.html");

        var headerRow = doc.GetElementbyId("header-row");

        foreach (var timezone in scheduleDays.SelectMany(x => x.Items.Keys))
        {
            headerRow.AppendChild(HtmlNode.CreateNode($"<th class=\"timezone\">{timezone.Short}</th>"));
        }

        var tableBody = doc.GetElementbyId("schedule-body");

        foreach (var day in scheduleDays)
        {
            var dayRowNode = HtmlNode.CreateNode("<tr></tr>");
            dayRowNode.AppendChild(HtmlNode.CreateNode($"<td class=\"first-column\">{day.DateHeader}</td>"));

            foreach (var item in day.Items.OrderBy(x => x.Key.Id))
            {
                dayRowNode.AppendChild(HtmlNode.CreateNode($"<td class=\"{item.Value}\"></td>"));
            }

            tableBody.AppendChild(dayRowNode);
        }

        var fileName = $"{groupName}_{updated.ToString("ddMMyyyyHHmmss")}_{name_suffix}.html";
        var html = doc.DocumentNode.OuterHtml;

        return await GetHtmlImage(html);
    }

    private static  async Task<byte[]> GetHtmlImage(string html)
    {
        int retry = 0;
        var maxretry = 3;
        do
        {
            try
            {
                var browserFetcher = new BrowserFetcher();
                await browserFetcher.DownloadAsync();

                await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions {});

                await using var page = await browser.NewPageAsync();
                await page.SetContentAsync(html);
                return await page.ScreenshotDataAsync(new ScreenshotOptions
                {
                    FullPage = true,
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