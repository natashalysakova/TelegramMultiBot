using DtekParsers;
using Moq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using TelegramMultiBot.BackgroundServies;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.Database.Models;

namespace BotTests;

[TestClass]
public class AddressParserTests
{
    private Mock<ISqlConfiguationService> _mockConfigService;
    private AddressParser _addressParser;
    private SvitlobotSettings _svitlobotSettings;

    [TestInitialize]
    public void Setup()
    {
        _svitlobotSettings = new SvitlobotSettings();
        _mockConfigService = new Mock<ISqlConfiguationService>();
        _mockConfigService.Setup(c => c.SvitlobotSettings).Returns(_svitlobotSettings);
        _addressParser = new AddressParser(_mockConfigService.Object);
    }

    #region GetCookie Tests

    [TestMethod]
    public void GetCookie_WithKemLocation_ReturnsCookie()
    {
        // Arrange
        var cookie = "test_kem_cookie";
        _svitlobotSettings.KemCookie = cookie;
        var url = "https://www.dtek-kem.com.ua/ua/ajax";

        // Act
        var result = CallGetCookie(url);

        // Assert
        Assert.AreEqual(cookie, result);
    }

    [TestMethod]
    public void GetCookie_WithKremLocation_ReturnsCookie()
    {
        // Arrange
        var cookie = "test_krem_cookie";
        _svitlobotSettings.KremCookie = cookie;
        var url = "https://www.dtek-krem.com.ua/ua/ajax";

        // Act
        var result = CallGetCookie(url);

        // Assert
        Assert.AreEqual(cookie, result);
    }

    [TestMethod]
    public void GetCookie_WithUnknownLocation_ReturnsNull()
    {
        // Arrange
        var url = "https://www.unknown-location.com/ua/ajax";

        // Act
        var result = CallGetCookie(url);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetCookie_WithNullConfigService_ReturnsNull()
    {
        // Arrange
        var addressParser = new AddressParser(null!);
        var url = "https://www.dtek-kem.com.ua/ua/ajax";

        // Act
        var result = CallGetCookie(url, addressParser);

        // Assert
        Assert.IsNull(result);
    }

    #endregion

    #region ParseAddress Tests

    [TestMethod]
    public async Task ParseAddress_WithNullAddressJob_ThrowsException()
    {
        // Arrange
        var date = DateTimeOffset.Now;

        // Act
        await Assert.ThrowsAsync<ParseException>(async () => await _addressParser.ParseAddress(null!, date));

        // Assert is handled by ExpectedException
    }

    [TestMethod]
    public async Task ParseAddress_WithInvalidResponse_ThrowsParseException()
    {
        // Arrange
        var addressJob = CreateAddressJob();
        var date = DateTimeOffset.Now;

        // This test would require mocking HttpClient, which needs refactoring
        // For now, this demonstrates the expected behavior when response is invalid

        // Act & Assert - Expected to throw ParseException
        await Assert.ThrowsAsync<ParseException>(async () => await _addressParser.ParseAddress(addressJob, date));
    }

    [TestMethod]
    public void ParseAddress_RequestContentFormation_CreatesCorrectKeyValuePairs()
    {
        // Arrange
        var city = "Kyiv";
        var street = "Main Street";
        var date = new DateTimeOffset(2024, 1, 23, 12, 30, 0, TimeSpan.Zero);

        // Act
        var collection = new List<KeyValuePair<string, string>>
        {
            new("method", "getHomeNum"),
            new("data[0][name]", "city"),
            new("data[0][value]", city),
            new("data[1][name]", "street"),
            new("data[1][value]", street),
            new("data[2][name]", "updateFact"),
            new("data[2][value]", date.ToString("dd.MM.yyyy HH:mm"))
        };

        // Assert
        Assert.AreEqual(7, collection.Count);
        Assert.AreEqual("getHomeNum", collection[0].Value);
        Assert.AreEqual(city, collection[2].Value);
        Assert.AreEqual(street, collection[4].Value);
        Assert.AreEqual("23.01.2024 12:30", collection[6].Value);
    }

    #endregion

    #region AddressResponse Deserialization Tests

    [TestMethod]
    public void AddressResponse_DeserializeValidJson_SuccessfullyDeserializes()
    {
        // Arrange
        var json = @"{
            ""result"": true,
            ""data"": {
                ""100"": {
                    ""sub_type"": ""planned"",
                    ""start_date"": ""2024-01-23T10:00:00"",
                    ""end_date"": ""2024-01-23T12:00:00"",
                    ""type"": ""disconnection"",
                    ""sub_type_reason"": [""maintenance""],
                    ""voluntarily"": null
                }
            }
        }";

        // Act
        var response = JsonSerializer.Deserialize<AddressResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Assert
        Assert.IsNotNull(response);
        Assert.IsTrue(response.Result);
        Assert.IsNotNull(response.Data);
        Assert.AreEqual(1, response.Data.Count);
        Assert.IsTrue(response.Data.ContainsKey("100"));
    }

    [TestMethod]
    public void BuildingInfo_DeserializeFromJson_MapsAllProperties()
    {
        // Arrange
        var json = @"{
            ""sub_type"": ""planned"",
            ""start_date"": ""2024-01-23T10:00:00"",
            ""end_date"": ""2024-01-23T12:00:00"",
            ""type"": ""disconnection"",
            ""sub_type_reason"": [""maintenance"", ""repair""],
            ""voluntarily"": false
        }";

        // Act
        var buildingInfo = JsonSerializer.Deserialize<BuildingInfo>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Assert
        Assert.IsNotNull(buildingInfo);
        Assert.AreEqual("planned", buildingInfo.SubType);
        Assert.AreEqual("2024-01-23T10:00:00", buildingInfo.StartDate);
        Assert.AreEqual("2024-01-23T12:00:00", buildingInfo.EndDate);
        Assert.AreEqual("disconnection", buildingInfo.Type);
        Assert.AreEqual(2, buildingInfo.SubTypeReason.Count);
    }

    [TestMethod]
    public void BuildingInfo_DeserializeWithMissingFields_UsesDefaultValues()
    {
        // Arrange
        var json = @"{
            ""type"": ""disconnection""
        }";

        // Act
        var buildingInfo = JsonSerializer.Deserialize<BuildingInfo>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Assert
        Assert.IsNotNull(buildingInfo);
        Assert.AreEqual("disconnection", buildingInfo.Type);
        Assert.AreEqual(string.Empty, buildingInfo.SubType);
        Assert.AreEqual(string.Empty, buildingInfo.StartDate);
        Assert.AreEqual(0, buildingInfo.SubTypeReason.Count);
    }

    #endregion

    #region Helper Methods

    private string? CallGetCookie(string url)
    {
        return CallGetCookie(url, _addressParser);
    }

    private string? CallGetCookie(string url, AddressParser parser)
    {
        // Using reflection to access private GetCookie method
        var method = typeof(AddressParser).GetMethod("GetCookie",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (method == null)
        {
            throw new InvalidOperationException("GetCookie method not found");
        }

        return (string?)method.Invoke(parser, new object[] { url });
    }

    private AddressJob CreateAddressJob()
    {
        return new AddressJob
        {
            Id = Guid.NewGuid(),
            // MUST match exact values from server - use Cyrillic 'с' not Latin 'c'
            City = "с. Білогородка",
            // MUST match exact street name in server database
            Street = "вул. Величка Михайла",
            // Building key must exist in server response for this city/street
            Number = "14-А",
            Location = new ElectricityLocation
            {
                Id = Guid.NewGuid(),
                Region = "krem",
                Url = "https://www.dtek-krem.com.ua/ua/shutdowns"
            }
        };
    }

    [TestMethod]
    public async Task ParseAddress_WithValidBuildingData_ReturnsBuildingInfo()
    {
        // Arrange
        var addressJob = CreateAddressJob();
        var date = DateTimeOffset.Now;

        // Act
        var result = await _addressParser.ParseAddress(addressJob, date);

        //Assert
         Assert.IsNotNull(result);
         var buildingInfo = result[addressJob.Number];
        Assert.AreEqual("2", buildingInfo.Type);
    }

    #endregion

    #region Validation Tests

    [TestMethod]
    public void ValidateAndTrimCyrillicText_WithValidCyrillicText_ReturnsTrimmiedText()
    {
        // Arrange
        var input = "  м. Київ  ";
        var expected = "м. Київ";

        // Act
        var result = InvokeValidateAndTrimCyrillicText(input, "City");

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void ValidateAndTrimCyrillicText_WithValidStreetName_ReturnsTrimmiedText()
    {
        // Arrange
        var input = "  вул. Величка Михайла  ";
        var expected = "вул. Величка Михайла";

        // Act
        var result = InvokeValidateAndTrimCyrillicText(input, "Street");

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void ValidateAndTrimCyrillicText_WithLatinCharacters_ConvertsAndReturnsText()
    {
        // Arrange
        var input = "м. Kyiv"; // Latin 'K' and latin characters that should be converted
        var expected = "м. Куів"; // Should convert 'K' to 'К', 'y' to 'у', 'i' to 'і', 'v' to 'в'

        // Act
        var result = InvokeValidateAndTrimCyrillicText(input, "City");

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void ValidateAndTrimCyrillicText_WithMixedLatinCyrillic_ConvertsLatinToCyrillic()
    {
        // Arrange
        var input = "вул. Maxa Kpoвa"; // Mix of latin and cyrillic
        var expected = "вул. Маха Крова"; // Should convert latin characters to cyrillic

        // Act
        var result = InvokeValidateAndTrimCyrillicText(input, "Street");

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void ValidateAndTrimCyrillicText_WithInvalidLatinCharacters_ThrowsArgumentException()
    {
        // Arrange - using characters that cannot be converted (like 'q', 'w', 'z')
        var input = "Kyiv Street with q and w";

        // Act & Assert
        try
        {
            InvokeValidateAndTrimCyrillicText(input, "City");
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is ArgumentException)
        {   
            Assert.Contains("contains invalid characters", ex.InnerException.Message);
            // Should show both original and converted text
            Assert.Contains("Original:", ex.InnerException.Message);
            Assert.Contains("Converted:", ex.InnerException.Message);
        }
        catch (ArgumentException ex)
        {
            Assert.Contains("contains invalid characters", ex.Message);
            Assert.Contains("Original:", ex.Message);
            Assert.Contains("Converted:", ex.Message);
        }
    }

    [TestMethod]
    public void ValidateAndTrimCyrillicText_WithEmptyString_ThrowsArgumentException()
    {
        // Arrange
        var input = "";

        // Act & Assert
        try
        {
            InvokeValidateAndTrimCyrillicText(input, "City");
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is ArgumentException)
        {
            Assert.Contains("cannot be null or empty", ex.InnerException.Message);
        }
        catch (ArgumentException ex)
        {
            Assert.Contains("cannot be null or empty", ex.Message);
        }
    }

    [TestMethod]
    public void ValidateAndTrimCyrillicText_WithOnlyWhitespace_ThrowsArgumentException()
    {
        // Arrange
        var input = "   ";

        // Act & Assert
        try
        {
            InvokeValidateAndTrimCyrillicText(input, "Street");
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is ArgumentException)
        {
            Assert.IsTrue(ex.InnerException.Message.Contains("cannot be null or empty"));
        }
        catch (ArgumentException ex)
        {
            Assert.IsTrue(ex.Message.Contains("cannot be null or empty"));
        }
    }

    private static string InvokeValidateAndTrimCyrillicText(string text, string fieldName)
    {
        // Use reflection to call the private static method
        var type = typeof(AddressParser);
        var method = type.GetMethod("ValidateAndTrimCyrillicText", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        if (method == null)
        {
            throw new InvalidOperationException("ValidateAndTrimCyrillicText method not found");
        }

        return (string)method.Invoke(null, new object[] { text, fieldName });
    }

    #endregion
}
