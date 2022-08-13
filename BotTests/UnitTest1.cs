namespace BotTests
{
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
    }
}