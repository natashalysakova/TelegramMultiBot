
using HtmlAgilityPack;
using PuppeteerSharp;

namespace DtekParsers;

public class ScheduleImageGenerator
{
    private const int BASE_WIDTH = 1000;
    private const int ROW_HEIGHT = 35;
    private const int SCALE_FACTOR = 2;
    private const int HEADER_HEIGHT = 175;

    public static async Task<IEnumerable<ImageGenerationModel>> GenerateRealScheduleSingleGroupImages(Schedule schedule)
    {
        var requests = new List<ImageGenerationModel>();
        foreach (var group in schedule.Groups)
        {
            var statuses = new Dictionary<string, Dictionary<string, IEnumerable<LightStatus>>>();
            Dictionary<string, IEnumerable<LightStatus>> items = new();
            foreach (var day in schedule.RealSchedule)
            {
                items.Add(day.DateHeader, day.Statuses[group.Id]);
            }

            statuses[group.GroupName] = items;

            var html = await GenerateScheduleBody(
                $"Графік відключень {schedule.Location} {group.GroupName}",
                schedule.TimeZones,
                schedule.RealSchedule.Max(x => x.Updated),
                statuses);

            foreach (var day in schedule.RealSchedule)
            {
                requests.Add(new ImageGenerationModel
                {
                    Group = group.Id,
                    HtmlContent = html,
                    RowNumber = schedule.RealSchedule.Count,
                    Date = day.DateTimeStamp,
                });
            }

        }
        var images = await GetHtmlImage(requests);
        return images;
    }

    public static async Task<IEnumerable<ImageGenerationModel>> GeneratePlannedScheduleSingleGroupImages(Schedule schedule)
    {
        var requests = new List<ImageGenerationModel>();
        foreach (var group in schedule.Groups)
        {
            var statuses = new Dictionary<string, Dictionary<string, IEnumerable<LightStatus>>>();

            Dictionary<string, IEnumerable<LightStatus>> items = new();
            foreach (var day in schedule.PlannedSchedule)
            {
                items.Add(day.DateHeader, day.Statuses[group.Id]);
            }

            statuses[group.GroupName] = items;

            var html = await GenerateScheduleBody(
                $"Графік можливих відключень {schedule.Location} {group.GroupName}",
                schedule.TimeZones,
                schedule.PlannedSchedule.Max(x => x.Updated),
                statuses);

            requests.Add(new ImageGenerationModel
            {
                Group = group.Id,
                HtmlContent = html,
                RowNumber = schedule.RealSchedule.Count,
                Planned = true
            });
        }

        var images = await GetHtmlImage(requests);
        return images;
    }

    public static async Task<IEnumerable<ImageGenerationModel>> GenerateAllGroupsRealSchedule(Schedule schedule)
    {
        var statuses = new Dictionary<string,Dictionary<string, IEnumerable<LightStatus>>>();
        foreach (var day in schedule.RealSchedule.OrderBy(x=>x.DateTimeStamp))
        {
            Dictionary<string, IEnumerable<LightStatus>> items = new();
            foreach (var group in schedule.Groups)
            {
                items.Add(group.GroupName, day.Statuses[group.Id]);
            }
            statuses[day.DateHeader] = items;
        }

        var html = await GenerateScheduleBody(
            $"Графік відключень {schedule.Location}",
            schedule.TimeZones,
            schedule.RealSchedule.Max(x=> x.Updated),
            statuses);

        List<ImageGenerationModel> requests = new();
        foreach (var day in schedule.RealSchedule)
        {
            requests.Add( new ImageGenerationModel
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
        Dictionary<string, Dictionary<string, IEnumerable<LightStatus>>> scheduleList)
    {
        var doc = new HtmlDocument();

        doc.Load("Templates/template.html");
      
        var titleNode = doc.GetElementbyId("schedule-header");
        titleNode.InnerHtml = title;

        var container = doc.GetElementbyId("tables");
        //var templateNode = doc.GetElementbyId("table-template");

        foreach (var schedule in scheduleList)
        {
            var currentTable = HtmlNode.CreateNode("" +
                "<table class=\"schedule-table\">" +
                "  <thead>" +
                "    <tr id=\"header-row\">" +
                "    </tr>" +
                "  </thead>" +
                "  <tbody id=\"schedule-body\">" +
                "  </tbody>" +
                "</table>");

            currentTable.Id = string.Empty;

            var headerRow = currentTable.SelectSingleNode("//thead/tr[@id='header-row']");
            headerRow.ChildNodes.Clear();
            headerRow.AppendChild(HtmlNode.CreateNode($"<td class=\"first-column-date\">{schedule.Key}</td>"));
            foreach (var timezone in timeZones)
            {
                headerRow.AppendChild(HtmlNode.CreateNode($"<th class=\"timezone\">{timezone.Short}</th>"));
            }

            var tableBody = currentTable.SelectSingleNode("//tbody[@id='schedule-body']");
            tableBody.ChildNodes.Clear();
            foreach (var line in schedule.Value)
            {
                var dayRowNode = HtmlNode.CreateNode("<tr></tr>");
                dayRowNode.AppendChild(HtmlNode.CreateNode($"<td class=\"first-column\">{line.Key}</td>"));

                foreach (var item in line.Value.OrderBy(x => x.Id))
                {
                    dayRowNode.AppendChild(HtmlNode.CreateNode($"<td class=\"{item.Status}\"></td>"));
                }

                tableBody.AppendChild(dayRowNode);
            }

            container.AppendChild(currentTable);
        }

        //templateNode.Remove();

        HtmlNode legendToRemove;
        if (scheduleList.SelectMany(y=> y.Value.SelectMany(x => x.Value)).Any(y => y.Status == ScheduleStatus.maybe))
        {
            legendToRemove = doc.GetElementbyId("legend-real");
        }
        else
        {
            legendToRemove = doc.GetElementbyId("legend-planned");
        }
        legendToRemove.Remove();

        var updatedInfo = doc.GetElementbyId("updated-info");
        updatedInfo.InnerHtml = $"Оновлено: {updated.ToString("dd.MM.yyyy HH:mm")}";

        //var fileName = $"{groupName}_{updated.ToString("ddMMyyyyHHmmss")}_{name_suffix}.html";
        return doc.DocumentNode.OuterHtml;
    }

    private static async Task<List<ImageGenerationModel>> GetHtmlImage(List<ImageGenerationModel> requests)
    {
        int retry = 0;
        var maxretry = 3;

        if(requests.Count == 0)
        {
            return new List<ImageGenerationModel>();
        }

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

                foreach (var renderRequest in requests)
                {
                    await page.SetContentAsync(renderRequest.HtmlContent);

                    var selector = await page.WaitForSelectorAsync("#body");
                    renderRequest.ImageData = await selector.ScreenshotDataAsync(new ElementScreenshotOptions
                    {
                        Type = ScreenshotType.Png,
                        CaptureBeyondViewport = true,
                    });
                }

                return requests;
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

    public static async Task<IEnumerable<ImageGenerationModel>> GenerateAllImages(Schedule schedule)
    {
        var allGroupsRealScheduleImages = await GenerateAllGroupsRealSchedule(schedule);
        var singleGroupRealScheduleImages = await GenerateRealScheduleSingleGroupImages(schedule);
        var singleGroupPlannedScheduleImages = await GeneratePlannedScheduleSingleGroupImages(schedule);

        return allGroupsRealScheduleImages
            .Concat(singleGroupRealScheduleImages)
            .Concat(singleGroupPlannedScheduleImages)
            ;
    }

    
}

public class ImageGenerationModel
{
    public byte[] ImageData { get; set; }
    public string? Group { get; set; }

    public long? Date { get; set; }
    public bool Planned { get; internal set; }
    public int RowNumber { get; internal set; }
    public string HtmlContent { get; internal set; }
}