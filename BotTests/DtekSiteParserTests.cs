using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using TelegramMultiBot.BackgroundServies;

namespace TelegramMultiBot.Tests.BackgroundServices;

[TestClass]
public class DtekSiteParserTests
{
    private DtekSiteParser _parser = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        // For testing the public method, we can create instance without dependencies
        _parser = new DtekSiteParser(null!, null!);
    }

    #region ConvertDataSnapshotToNewSchedule Tests

    [TestMethod]
    public void ConvertDataSnapshotToNewSchedule_WithValidInputs_ReturnsFormattedString()
    {
        // Arrange
        var schedule = "[[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,1,0,0,1,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,1,0,0,0,0,0,1,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]]";
        
        var dataSnapshot = "1764885600|1:0|2:0|3:0|4:0|5:0|6:0|7:0|8:0|9:1|10:1|11:0|12:0|13:0|14:0|15:0|16:0|17:0|18:1|19:1|20:1|21:3|22:0|23:0|24:0;";

        // Act
        var result = _parser.ConvertDataSnapshotToNewSchedule(schedule, dataSnapshot);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType(result, typeof(string));
        var resultString = result.ToString();
        StringAssert.Contains(resultString, "%3B");
    }

    [TestMethod]
    public void ConvertDataSnapshotToNewSchedule_WithNullSchedule_ThrowsException()
    {
        // Arrange
        string? schedule = null;
        var dataSnapshot = "1764885600|1:0|2:0|3:0;";

        // Act & Assert
        try
        {
            _parser.ConvertDataSnapshotToNewSchedule(schedule, dataSnapshot);
            Assert.Fail("Expected ArgumentNullException was not thrown");
        }
        catch (ArgumentNullException)
        {
            // Expected exception
        }
        catch (Exception ex)
        {
            Assert.Fail($"Expected ArgumentNullException but got {ex.GetType().Name}");
        }
    }

    [TestMethod]
    public void ConvertDataSnapshotToNewSchedule_WithNullDataSnapshot_UsesScheduleArray()
    {
        // Arrange
        var schedule = "[[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]]";
        string? dataSnapshot = null;

        // Act
        var result = _parser.ConvertDataSnapshotToNewSchedule(schedule, dataSnapshot);

        // Assert
        Assert.IsNotNull(result);
        var resultString = result.ToString();
        StringAssert.Contains(resultString, "111111111111111111111111");
    }

    [TestMethod]
    public void ConvertDataSnapshotToNewSchedule_WithEmptyDataSnapshot_ReturnsScheduleArrayData()
    {
        // Arrange
        var schedule = "[[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]]";
        var dataSnapshot = "";

        // Act
        var result = _parser.ConvertDataSnapshotToNewSchedule(schedule, dataSnapshot);

        // Assert
        Assert.IsNotNull(result);
        var resultString = result.ToString();
        var days = resultString.Split("%3B");
        Assert.AreEqual(7, days.Length);
    }

    [TestMethod]
    public void ConvertDataSnapshotToNewSchedule_WithMultipleDays_MergesCorrectly()
    {
        // Arrange
        var schedule = "[[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]]";
        
        // Monday and Tuesday
        var dataSnapshot = "1704067200|1:1|2:1|3:1|4:1|5:1|6:1|7:1|8:1|9:1|10:1|11:1|12:1|13:1|14:1|15:1|16:1|17:1|18:1|19:1|20:1|21:1|22:1|23:1|24:1;" +
                          "1704153600|1:0|2:0|3:0|4:0|5:0|6:0|7:0|8:0|9:0|10:0|11:0|12:0|13:0|14:0|15:0|16:0|17:0|18:0|19:0|20:0|21:0|22:0|23:0|24:0;";

        // Act
        var result = _parser.ConvertDataSnapshotToNewSchedule(schedule, dataSnapshot);

        // Assert
        Assert.IsNotNull(result);
        var resultString = result.ToString();
        StringAssert.Contains(resultString, "111111111111111111111111");
        StringAssert.Contains(resultString, "000000000000000000000000");
    }

    [TestMethod]
    public void ConvertDataSnapshotToNewSchedule_WithSundayDate_MapsToDayIndex6()
    {
        // Arrange
        var schedule = "[[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]]";
        
        // Sunday: 2024-01-07 00:00:00 UTC
        var dataSnapshot = "1704585600|1:1|2:1|3:1|4:1|5:1|6:1|7:1|8:1|9:1|10:1|11:1|12:1|13:1|14:1|15:1|16:1|17:1|18:1|19:1|20:1|21:1|22:1|23:1|24:1;";

        // Act
        var result = _parser.ConvertDataSnapshotToNewSchedule(schedule, dataSnapshot);

        // Assert
        Assert.IsNotNull(result);
        var resultString = result.ToString();
        var days = resultString.Split("%3B");
        Assert.AreEqual("111111111111111111111111", days[6]); // Sunday should be last
    }

    [TestMethod]
    public void ConvertDataSnapshotToNewSchedule_WithInvalidDataFormat_HandlesGracefully()
    {
        // Arrange
        var schedule = "[[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]]";
        
        var dataSnapshot = "1704067200|1:1|invalid_data;";

        // Act
        var result = _parser.ConvertDataSnapshotToNewSchedule(schedule, dataSnapshot);

        // Assert
        Assert.IsNotNull(result);
        // Should fall back to schedule array for missing/invalid days
    }

    #endregion

    #region SendUpdatesToSvitlobot Integration Tests

    [TestMethod]
    public void ConvertDataSnapshotToNewSchedule_ForSvitlobotIntegration_WithRealWorldData_ReturnsCorrectFormat()
    {
        // Arrange - simulating data from svitlobot API
        var schedule = "[[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,1,0,0,1,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,1,0,0,0,0,0,1,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]]";
        
        var dataSnapshot = "1764885600|1:0|2:0|3:0|4:0|5:0|6:0|7:0|8:0|9:1|10:1|11:0|12:0|13:0|14:0|15:0|16:0|17:0|18:1|19:1|20:1|21:3|22:0|23:0|24:0;";

        // Act
        var result = _parser.ConvertDataSnapshotToNewSchedule(schedule, dataSnapshot);

        // Assert
        Assert.IsNotNull(result);
        var resultString = result.ToString()!;
        
        // Should be URL encoded with %3B as separator
        Assert.IsTrue(resultString.Contains("%3B"));
        
        // Should have 7 days (6 separators)
        var parts = resultString.Split("%3B");
        Assert.AreEqual(7, parts.Length, "Result should contain 7 days");
        
        // Each day should have 24 characters (one per hour)
        foreach (var part in parts)
        {
            Assert.AreEqual(24, part.Length, $"Each day should have 24 hours, but got {part.Length}");
        }
    }

    [TestMethod]
    public void ConvertDataSnapshotToNewSchedule_WithPartialDataSnapshot_FillsRemainingDaysFromSchedule()
    {
        // Arrange - schedule has data for all 7 days, dataSnapshot only has 2 days
        var schedule = "[[1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1]," +
                      "[2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2]," +
                      "[3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3]," +
                      "[4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4]," +
                      "[5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5]," +
                      "[6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6]," +
                      "[7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7]]";
        
        // Only Monday (1704067200) and Wednesday (1704240000)
        var dataSnapshot = "1704067200|1:9|2:9|3:9|4:9|5:9|6:9|7:9|8:9|9:9|10:9|11:9|12:9|13:9|14:9|15:9|16:9|17:9|18:9|19:9|20:9|21:9|22:9|23:9|24:9;" +
                          "1704240000|1:8|2:8|3:8|4:8|5:8|6:8|7:8|8:8|9:8|10:8|11:8|12:8|13:8|14:8|15:8|16:8|17:8|18:8|19:8|20:8|21:8|22:8|23:8|24:8;";

        // Act
        var result = _parser.ConvertDataSnapshotToNewSchedule(schedule, dataSnapshot);

        // Assert
        Assert.IsNotNull(result);
        var resultString = result.ToString()!;
        var days = resultString.Split("%3B");
        
        Assert.AreEqual(7, days.Length);
        
        // Monday (index 0) should be all 9s from dataSnapshot
        Assert.AreEqual("999999999999999999999999", days[0]);
        
        // Tuesday (index 1) should be all 2s from schedule
        Assert.AreEqual("222222222222222222222222", days[1]);
        
        // Wednesday (index 2) should be all 8s from dataSnapshot
        Assert.AreEqual("888888888888888888888888", days[2]);
        
        // Other days should come from schedule array
        Assert.AreEqual("444444444444444444444444", days[3]); // Thursday
        Assert.AreEqual("555555555555555555555555", days[4]); // Friday
        Assert.AreEqual("666666666666666666666666", days[5]); // Saturday
        Assert.AreEqual("777777777777777777777777", days[6]); // Sunday
    }

    [TestMethod]
    public void ConvertDataSnapshotToNewSchedule_WithAllSevenDaysInDataSnapshot_IgnoresScheduleArray()
    {
        // Arrange - schedule has zeros, but dataSnapshot has all 7 days
        var schedule = "[[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]]";
        
        // All 7 days from Monday to Sunday
        var dataSnapshot = "1704067200|1:1|2:1|3:1|4:1|5:1|6:1|7:1|8:1|9:1|10:1|11:1|12:1|13:1|14:1|15:1|16:1|17:1|18:1|19:1|20:1|21:1|22:1|23:1|24:1;" + // Mon
                          "1704153600|1:2|2:2|3:2|4:2|5:2|6:2|7:2|8:2|9:2|10:2|11:2|12:2|13:2|14:2|15:2|16:2|17:2|18:2|19:2|20:2|21:2|22:2|23:2|24:2;" + // Tue
                          "1704240000|1:3|2:3|3:3|4:3|5:3|6:3|7:3|8:3|9:3|10:3|11:3|12:3|13:3|14:3|15:3|16:3|17:3|18:3|19:3|20:3|21:3|22:3|23:3|24:3;" + // Wed
                          "1704326400|1:4|2:4|3:4|4:4|5:4|6:4|7:4|8:4|9:4|10:4|11:4|12:4|13:4|14:4|15:4|16:4|17:4|18:4|19:4|20:4|21:4|22:4|23:4|24:4;" + // Thu
                          "1704412800|1:5|2:5|3:5|4:5|5:5|6:5|7:5|8:5|9:5|10:5|11:5|12:5|13:5|14:5|15:5|16:5|17:5|18:5|19:5|20:5|21:5|22:5|23:5|24:5;" + // Fri
                          "1704499200|1:6|2:6|3:6|4:6|5:6|6:6|7:6|8:6|9:6|10:6|11:6|12:6|13:6|14:6|15:6|16:6|17:6|18:6|19:6|20:6|21:6|22:6|23:6|24:6;" + // Sat
                          "1704585600|1:7|2:7|3:7|4:7|5:7|6:7|7:7|8:7|9:7|10:7|11:7|12:7|13:7|14:7|15:7|16:7|17:7|18:7|19:7|20:7|21:7|22:7|23:7|24:7;";  // Sun

        // Act
        var result = _parser.ConvertDataSnapshotToNewSchedule(schedule, dataSnapshot);

        // Assert
        Assert.IsNotNull(result);
        var resultString = result.ToString()!;
        var days = resultString.Split("%3B");
        
        Assert.AreEqual(7, days.Length);
        
        // All days should come from dataSnapshot, not schedule
        Assert.AreEqual("111111111111111111111111", days[0]); // Monday
        Assert.AreEqual("222222222222222222222222", days[1]); // Tuesday
        Assert.AreEqual("333333333333333333333333", days[2]); // Wednesday
        Assert.AreEqual("444444444444444444444444", days[3]); // Thursday
        Assert.AreEqual("555555555555555555555555", days[4]); // Friday
        Assert.AreEqual("666666666666666666666666", days[5]); // Saturday
        Assert.AreEqual("777777777777777777777777", days[6]); // Sunday
        
        // None should be zeros from the schedule array
        Assert.IsFalse(resultString.Contains("000000000000000000000000"));
    }

    [TestMethod]
    public void ConvertDataSnapshotToNewSchedule_WithMixedStatusValues_PreservesValues()
    {
        // Arrange - testing that different status values (0, 1, 2, 3) are preserved
        var schedule = "[[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]]";
        
        // Using different status values: 0=no power, 1=power, 2=maybe, 3=unknown
        // Monday: 2024-01-01 00:00:00 UTC (1704067200)
        var dataSnapshot = "1704067200|1:0|2:0|3:1|4:1|5:2|6:2|7:3|8:3|9:0|10:1|11:2|12:3|13:0|14:1|15:2|16:3|17:0|18:1|19:2|20:3|21:0|22:1|23:2|24:3;";

        // Act
        var result = _parser.ConvertDataSnapshotToNewSchedule(schedule, dataSnapshot);

        // Assert
        Assert.IsNotNull(result);
        var resultString = result.ToString()!;
        var days = resultString.Split("%3B");
        
        // Monday (index 0) should have the mixed pattern
        Assert.AreEqual("001122330123012301230123", days[0]);
        
        // Verify it contains all status types
        StringAssert.Contains(resultString, "0");
        StringAssert.Contains(resultString, "1");
        StringAssert.Contains(resultString, "2");
        StringAssert.Contains(resultString, "3");
    }

    #endregion
}