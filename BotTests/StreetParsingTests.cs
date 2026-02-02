using Microsoft.VisualStudio.TestTools.UnitTesting;
using DtekParsers;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using TelegramMultiBot.Database.Interfaces;
using Moq;

namespace BotTests;

[TestClass]
public class StreetParsingTests
{
    private ScheduleParser _parser;

    [TestInitialize]
    public void Setup()
    {
        var mockConfigService = new Mock<ISqlConfiguationService>();
        _parser = new ScheduleParser(mockConfigService.Object);
    }

    [TestMethod]
    public void ParseStreetWithCity_ShouldDefaultToKyivWhenNoCitySpecified()
    {
        // Use reflection to call private method for testing
        var method = typeof(ScheduleParser).GetMethod("ParseStreetWithCity", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var result = (ValueTuple<string, string>)method.Invoke(_parser, new object[] { "вул. Чубинського" });
        
        Assert.AreEqual("Київ", result.Item1);
        Assert.AreEqual("вул. Чубинського", result.Item2);
    }

    [TestMethod]
    public void ParseStreetWithCity_ShouldExtractCityFromPrefix()
    {
        var method = typeof(ScheduleParser).GetMethod("ParseStreetWithCity", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var result = (ValueTuple<string, string>)method.Invoke(_parser, new object[] { "м. Бровари, вул. Спортивна" });
        
        Assert.AreEqual("м. Бровари", result.Item1);
        Assert.AreEqual("вул. Спортивна", result.Item2);
    }

    [TestMethod]
    public void ParseStreetWithCity_ShouldHandleDistrictFormat()
    {
        var method = typeof(ScheduleParser).GetMethod("ParseStreetWithCity", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var result = (ValueTuple<string, string>)method.Invoke(_parser, new object[] { "Дарницький р-н, вул. Лугова" });
        
        Assert.AreEqual("Дарницький р-н", result.Item1);
        Assert.AreEqual("вул. Лугова", result.Item2);
    }

    [TestMethod]
    public void ParseStreetWithCity_ShouldHandleSpecialCases()
    {
        var method = typeof(ScheduleParser).GetMethod("ParseStreetWithCity", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var result = (ValueTuple<string, string>)method.Invoke(_parser, new object[] { "За межами с. Гнідин" });
        
        Assert.AreEqual("с. Гнідин", result.Item1);
        Assert.AreEqual("За межами", result.Item2);
    }

    [TestMethod]
    public void GetStreetsInfo_ShouldGroupStreetsByCity()
    {
        // Create test JSON data
        var streetsJson = JObject.Parse(@"{
            ""Group1"": [
                ""вул. Чубинського"",
                ""м. Бровари, вул. Спортивна"",
                ""Дарницький р-н, вул. Лугова""
            ]
        }");

        var method = typeof(ScheduleParser).GetMethod("GetStreetsInfo", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        var result = (Dictionary<string, List<string>>)method.Invoke(_parser, new object[] { streetsJson });
        
        // Should have Kyiv group (default) and other cities
        Assert.IsTrue(result.ContainsKey("Group1")); // Kyiv streets
        Assert.IsTrue(result.ContainsKey("м. Бровари_Group1")); // Brovary streets
        Assert.IsTrue(result.ContainsKey("Дарницький р-н_Group1")); // District streets
    }
}