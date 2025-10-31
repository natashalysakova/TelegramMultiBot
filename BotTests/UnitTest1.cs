using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.ImageGeneration;
using TelegramMultiBot.Reminder;

namespace BotTests;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void TestMethod1()
    {
        //2022 - 08 - 09 04:05:00
        var next = CronUtil.ParseNext("5 4 * * *");
        Assert.AreEqual(new DateTime(2022, 08, 09, 04, 05, 00), next);
    }

    [TestMethod]
    public void MyTestMethod()
    {
        string s = "cat, dog, cute beavers\r\nNegative prompt: horse\r\nSteps: 5, Sampler: DPM++ SDE Karras, CFG scale: 2.0, Seed: 3342802166, Size: 1024x768, Model: dreamshaperXL_turboDpmppSDE, Denoising strength: 0.35, Version: v1.6.0-2-g4afaaf8a";

        var obj = new UpscaleParams(new JobResultInfoView() { Info = s, FilePath = string.Empty, Id = "test" });

        Assert.AreEqual("cat, dog, cute beavers", obj.Prompt);
        Assert.AreEqual("horse", obj.NegativePrompt);
        Assert.AreEqual(5, obj.Steps);
        Assert.AreEqual("DPM++ SDE Karras", obj.Sampler);
        Assert.AreEqual(2.0, obj.CFGScale);
        Assert.AreEqual(3342802166, obj.Seed);
        Assert.AreEqual(1024, obj.Width);
        Assert.AreEqual(768, obj.Height);
        Assert.AreEqual("dreamshaperXL_turboDpmppSDE", obj.Model);
        Assert.AreEqual(0.35, obj.Denoising);
        Assert.AreEqual("v1.6.0-2-g4afaaf8a", obj.Version);
    }
}