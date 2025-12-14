
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using System;

namespace DtekParsers;

public class ScheduleImageGenerator
{
    private const int BASE_WIDTH = 1000;
    private const int ROW_HEIGHT = 35;
    private const int SCALE_FACTOR = 2;
    private const int HEADER_HEIGHT = 175;

    public static async Task<IEnumerable<ImageGenerationModel>> GenerateRealScheduleSingleGroupImages(Schedule schedule)
    {
        return await GenerateSingleGroupImages(
            $"Графік відключень {schedule.Location}",
            schedule.Groups,
            schedule.TimeZones.OrderBy(x => x.Id).Select(x => x.Short),
            schedule.RealSchedule);
    }

    public static async Task<IEnumerable<ImageGenerationModel>> GeneratePlannedScheduleSingleGroupImages(Schedule schedule)
    {
        return await GenerateSingleGroupImages(
            $"Графік можливих відключень {schedule.Location}",
            schedule.Groups,
            schedule.TimeZones.OrderBy(x => x.Id).Select(x => x.Short),
            schedule.PlannedSchedule);
    }

    public static async Task<IEnumerable<ImageGenerationModel>> GenerateSingleGroupImages(
        string title,
        IEnumerable<ScheduleGroup> groups,
        IEnumerable<string> timeZones,
        IEnumerable<BaseSchedule> days)
    {
        var requests = new List<ImageGenerationModel>();
        foreach (var group in groups)
        {
            var printTable = new PrintTable
            {
                Header = group.GroupName,
                TimeZones = timeZones,
                Updated = days.Max(x => x.Updated),
                Rows = days.Select(day => new PrintRow
                {
                    Header = day.DateHeader,
                    Statuses = day.Statuses[group.Id],
                    DateHeader = day.DateHeader
                }),
            };

            var html = await GenerateScheduleBody($"{title}", [printTable]);

            var minDate = days.Min(x => x is RealSchedule rs ? rs.DateTimeStamp : 0);

            requests.Add(new ImageGenerationModel
            {
                Group = group.Id,
                HtmlContent = html,
                RowNumber = days.Count(),
                IsPlanned = printTable.IsPlanned,
                Date = minDate
            });
        }

        var images = await GetHtmlImage(requests);
        return images;
    }

    public static async Task<IEnumerable<ImageGenerationModel>> GenerateAllGroupsRealSchedule(Schedule schedule)
    {
        var tables = new List<PrintTable>();
        foreach (var day in schedule.RealSchedule.OrderBy(x => x.DateTimeStamp))
        {
            var printTable = new PrintTable
            {
                Header = day.DateHeader,
                TimeZones = schedule.TimeZones.OrderBy(x => x.Id).Select(x => x.Short),
                Updated = day.Updated,
                Rows = schedule.Groups.Select(group => new PrintRow
                {
                    Header = group.GroupName,
                    Statuses = day.Statuses[group.Id],
                    DateHeader = day.DateHeader
                })
            };
            tables.Add(printTable);
        }

        var html = await GenerateScheduleBody($"Графік відключень {schedule.Location}", tables);

        List<ImageGenerationModel> requests = new();
        foreach (var day in schedule.RealSchedule)
        {
            requests.Add(new ImageGenerationModel
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
        IEnumerable<PrintTable> tables)
    {
        var doc = new HtmlDocument();

        doc.Load("Templates/template.html");

        var titleNode = doc.GetElementbyId("schedule-header");
        titleNode.InnerHtml = title;

        var container = doc.GetElementbyId("tables");
        //var templateNode = doc.GetElementbyId("table-template");

        foreach (var table in tables)
        {


            var currentTable = HtmlNode.CreateNode("" +
                "<table class=\"schedule-table\">" +
                "  <thead>" +
                "    <tr id=\"date-row\">" +
                "       <th class=\"table-header\" colspan=\"" + (table.TimeZones.Count() + 1) + "\">" + table.Header + "</th>" +
                "    </tr>" +
                "    <tr id=\"header-row\">" +
                "    </tr>" +
                "  </thead>" +
                "  <tbody id=\"schedule-body\">" +
                "  </tbody>" +
                "</table>");

            var headerRow = currentTable.SelectSingleNode("//thead/tr[@id='header-row']");
            headerRow.ChildNodes.Clear();

            if (table.Total == 0)
            {
                var noOutagePlannedNode = HtmlNode.CreateNode($"<th class=\"table-header\">Станом на {table.Updated.ToString("dd.MM.yyyy HH:mm")} відключень не планується</th>");
                headerRow.PrependChild(noOutagePlannedNode);
                container.AppendChild(currentTable);
                continue;
            }


            headerRow.AppendChild(HtmlNode.CreateNode($"<th class=\"first-column-date\">Часові проміжки</th>"));
            foreach (var timezone in table.TimeZones)
            {
                headerRow.AppendChild(HtmlNode.CreateNode($"<th class=\"timezone\">{timezone}</th>"));
            }

            var tableBody = currentTable.SelectSingleNode("//tbody[@id='schedule-body']");
            tableBody.ChildNodes.Clear();
            foreach (var row in table.Rows)
            {
                var dayRowNode = HtmlNode.CreateNode("<tr></tr>");
                dayRowNode.AppendChild(HtmlNode.CreateNode($"<td class=\"first-column\">{row.Header}</td>"));

                foreach (var item in row.Statuses)
                {
                    dayRowNode.AppendChild(HtmlNode.CreateNode($"<td class=\"{item.Status}\"></td>"));
                }

                tableBody.AppendChild(dayRowNode);
            }

            container.AppendChild(currentTable);
        }

        //templateNode.Remove();

        if (tables.All(x=>x.Total == 0))
        {
            doc.GetElementbyId("legend-planned").Remove();
            doc.GetElementbyId("legend-real").Remove();
        }
        else if (tables.All(x => !x.IsPlanned))
        {
            doc.GetElementbyId("legend-planned").Remove();
        }
        else if (tables.All(x => x.IsPlanned))
        {
            doc.GetElementbyId("legend-real").Remove();
        } 

        var updatedInfo = doc.GetElementbyId("updated-info");
        updatedInfo.InnerHtml = $"Оновлено: {tables.Max(x => x.Updated).ToString("dd.MM.yyyy HH:mm")}";

        //var fileName = $"{groupName}_{updated.ToString("ddMMyyyyHHmmss")}_{name_suffix}.html";
        return doc.DocumentNode.OuterHtml;
    }

    private static async Task<List<ImageGenerationModel>> GetHtmlImage(List<ImageGenerationModel> requests)
    {
        int retry = 0;
        var maxretry = 3;

        if (requests.Count == 0)
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
                    Args = [
                        "--disable-gpu",
                        "--disable-dev-shm-usage",
                        "--disable-setuid-sandbox",
                        "--no-sandbox" ],
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
            .Concat(singleGroupPlannedScheduleImages);
    }
}