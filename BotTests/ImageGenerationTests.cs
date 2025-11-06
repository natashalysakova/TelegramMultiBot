using DtekParsers;
using System.Runtime.Serialization;

namespace BotTests;

[TestClass]
public class  ImageGenerationTests
{
    [TestMethod]
    [DataRow("https://www.dtek-kem.com.ua/ua/shutdowns", "kem.real")]
    [DataRow("https://www.dtek-krem.com.ua/ua/shutdowns", "krem.real")]
    public async Task Image_RealScheduleSingleGroupImageReady(string url, string folder)
    {
        var parser = new ScheduleParser();

        var schedule = await parser.Parse(url);

        var image = await ScheduleImageGenerator.GenerateRealScheduleSingleGroupImages(schedule);

        SaveImages(folder, image.Select(x=>x.ImageData));

        Assert.AreEqual(12, image.Count());
    }

    private static void SaveImages(string folder, IEnumerable<byte[]> image)
    {
        folder = Path.Combine("images", folder);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }

        Directory.CreateDirectory(folder);

        for (int i = 0; i < image.Count(); i++)
        {
            File.WriteAllBytes(Path.Combine(folder, $"{i}.png"), image.ElementAt(i));
        }
    }

    [TestMethod]
    [DataRow("https://www.dtek-kem.com.ua/ua/shutdowns", "kem.all")]
    [DataRow("https://www.dtek-krem.com.ua/ua/shutdowns", "krem.all")]
    public async Task Image_GenerateAllGroupsRealScheduleImageReady(string url, string folder)
    {
        var parser = new ScheduleParser();

        var schedule = await parser.Parse(url);

        var image = await ScheduleImageGenerator.GenerateAllGroupsRealSchedule(schedule);

        SaveImages(folder, image.Select(x => x.ImageData));

        Assert.AreEqual(2, image.Count());
    }

    [TestMethod]
    [DataRow("https://www.dtek-kem.com.ua/ua/shutdowns", "kem.planned")]
    [DataRow("https://www.dtek-krem.com.ua/ua/shutdowns", "krem.planned")]
    public async Task Image_GeneratePlannedScheduleSingleGroupImageReady(string url, string folder)
    {
        var parser = new ScheduleParser();

        var schedule = await parser.Parse(url);

        var image = await ScheduleImageGenerator.GeneratePlannedScheduleSingleGroupImages(schedule);

        SaveImages(folder, image.Select(x => x.ImageData));

        Assert.AreEqual(12, image.Count());
    }

    private string[] urls = [
        "https://www.dtek-kem.com.ua/ua/shutdowns",
        "https://www.dtek-krem.com.ua/ua/shutdowns",
        "https://www.dtek-oem.com.ua/ua/shutdowns",
        ];
}

