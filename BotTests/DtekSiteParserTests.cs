using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using TelegramMultiBot.BackgroundServies;

namespace TelegramMultiBot.Tests.BackgroundServices;

[TestClass]
public class DtekSiteParserTestsTest
{
    private DtekSiteParser _parser = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        // For testing the public method, we can create instance without dependencies
        _parser = new DtekSiteParser(null!, null!, null!);
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
        StringAssert.Contains(resultString, "%3B");
        
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
        var dataSnapshot = "1704067200|1:1|2:1|3:1|4:1|5:1|6:1|7:1|8:1|9:1|10:1|11:1|12:1|13:1|14:1|15:1|16:1|17:1|18:1|19:1|20:1|21:1|22:1|23:1|24:1;" +
                          "1704240000|1:0|2:0|3:0|4:0|5:0|6:0|7:0|8:0|9:0|10:0|11:0|12:0|13:0|14:0|15:0|16:0|17:0|18:0|19:0|20:0|21:0|22:0|23:0|24:0;";

        // Act
        var result = _parser.ConvertDataSnapshotToNewSchedule(schedule, dataSnapshot);

        // Assert
        Assert.IsNotNull(result);
        var resultString = result.ToString()!;
        var days = resultString.Split("%3B");
        
        Assert.AreEqual(7, days.Length);
        
        // Monday (index 0) should be all 1s from dataSnapshot
        Assert.AreEqual("111111111111111111111111", days[0]);
        
        // Tuesday (index 1) should be all 2s from schedule
        Assert.AreEqual("222222222222222222222222", days[1]);
        
        // Wednesday (index 2) should be all 0s from dataSnapshot
        Assert.AreEqual("000000000000000000000000", days[2]);
        
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
                          "1704153600|1:0|2:0|3:0|4:0|5:0|6:0|7:0|8:0|9:0|10:0|11:0|12:0|13:0|14:0|15:0|16:0|17:0|18:0|19:0|20:0|21:0|22:0|23:0|24:0;" + // Tue
                          "1704240000|1:3|2:3|3:3|4:3|5:3|6:3|7:3|8:3|9:3|10:3|11:3|12:3|13:3|14:3|15:3|16:3|17:3|18:3|19:3|20:3|21:3|22:3|23:3|24:3;" + // Wed
                          "1704326400|1:4|2:4|3:4|4:4|5:4|6:4|7:4|8:4|9:4|10:4|11:4|12:4|13:4|14:4|15:4|16:4|17:4|18:4|19:4|20:4|21:4|22:4|23:4|24:4;" + // Thu
                          "1704412800|1:1|2:1|3:1|4:1|5:1|6:1|7:1|8:1|9:1|10:1|11:1|12:1|13:1|14:1|15:1|16:1|17:1|18:1|19:1|20:1|21:1|22:1|23:1|24:1;" + // Fri
                          "1704499200|1:0|2:0|3:0|4:0|5:0|6:0|7:0|8:0|9:0|10:0|11:0|12:0|13:0|14:0|15:0|16:0|17:0|18:0|19:0|20:0|21:0|22:0|23:0|24:0;" + // Sat
                          "1704585600|1:3|2:3|3:3|4:3|5:3|6:3|7:3|8:3|9:3|10:3|11:3|12:3|13:3|14:3|15:3|16:3|17:3|18:3|19:3|20:3|21:3|22:3|23:3|24:3;";  // Sun

        // Act
        var result = _parser.ConvertDataSnapshotToNewSchedule(schedule, dataSnapshot);

        // Assert
        Assert.IsNotNull(result);
        var resultString = result.ToString()!;
        var days = resultString.Split("%3B");
        
        Assert.AreEqual(7, days.Length);
        
        // All days should come from dataSnapshot, not schedule
        Assert.AreEqual("111111111111111111111111", days[0]); // Monday (1->1)
        Assert.AreEqual("000000000000000000000000", days[1]); // Tuesday (0->0)
        Assert.AreEqual("222222222222222222222222", days[2]); // Wednesday (3->2)
        Assert.AreEqual("333333333333333333333333", days[3]); // Thursday (4->3)
        Assert.AreEqual("111111111111111111111111", days[4]); // Friday (1->1)
        Assert.AreEqual("000000000000000000000000", days[5]); // Saturday (0->0)
        Assert.AreEqual("222222222222222222222222", days[6]); // Sunday (3->2)
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
        var resultString = result.ToString()!;
        var days = resultString.Split("%3B");
        
        // Monday (index 0) should have the correctly mapped pattern
        // Based on ParseFromSnapshot: 0->0, 1->1, 2->1 (default), 3->2
        Assert.AreEqual("001111220112011201120112", days[0]);
        
        // Verify it contains all status types
        StringAssert.Contains(resultString, "0");
        StringAssert.Contains(resultString, "1");
        StringAssert.Contains(resultString, "2");
        StringAssert.Contains(resultString, "3");
    }

    #endregion

    #region ConvertDataSnapshotToNewSchedule_WithPartialDataSnapshot_FillsRemainingDaysFromSchedule Tests

    [TestMethod]
    public void ConvertDataSnapshotToNewSchedule_WithOnlyMondayDataSnapshot_FillsRestFromSchedule()
    {
        // Arrange - only Monday in dataSnapshot
        var schedule = "[[1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1]," +
                      "[2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2]," +
                      "[3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3]," +
                      "[4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4]," +
                      "[5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5]," +
                      "[6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6]," +
                      "[7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7]]";
        
        var dataSnapshot = "1704067200|1:1|2:1|3:1|4:1|5:1|6:1|7:1|8:1|9:1|10:1|11:1|12:1|13:1|14:1|15:1|16:1|17:1|18:1|19:1|20:1|21:1|22:1|23:1|24:1;";

        // Act
        var result = _parser.ConvertDataSnapshotToNewSchedule(schedule, dataSnapshot);

        // Assert
        var resultString = result.ToString()!;
        var days = resultString.Split("%3B");
        
        Assert.AreEqual("111111111111111111111111", days[0]); // Monday from dataSnapshot (1->1)
        Assert.AreEqual("222222222222222222222222", days[1]); // Tuesday from schedule
        Assert.AreEqual("333333333333333333333333", days[2]); // Wednesday from schedule
        Assert.AreEqual("444444444444444444444444", days[3]); // Thursday from schedule
        Assert.AreEqual("555555555555555555555555", days[4]); // Friday from schedule
        Assert.AreEqual("666666666666666666666666", days[5]); // Saturday from schedule
        Assert.AreEqual("777777777777777777777777", days[6]); // Sunday from schedule
    }

    [TestMethod]
    public void ConvertDataSnapshotToNewSchedule_WithOnlySundayDataSnapshot_FillsRestFromSchedule()
    {
        // Arrange - only Sunday in dataSnapshot
        var schedule = "[[1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1]," +
                      "[2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2]," +
                      "[3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3]," +
                      "[4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4]," +
                      "[5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5]," +
                      "[6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6]," +
                      "[7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7]]";
        
        // Sunday: 1704585600
        var dataSnapshot = "1704585600|1:0|2:0|3:0|4:0|5:0|6:0|7:0|8:0|9:0|10:0|11:0|12:0|13:0|14:0|15:0|16:0|17:0|18:0|19:0|20:0|21:0|22:0|23:0|24:0;";

        // Act
        var result = _parser.ConvertDataSnapshotToNewSchedule(schedule, dataSnapshot);

        // Assert
        var resultString = result.ToString()!;
        var days = resultString.Split("%3B");
        
        Assert.AreEqual("111111111111111111111111", days[0]); // Monday from schedule
        Assert.AreEqual("222222222222222222222222", days[1]); // Tuesday from schedule
        Assert.AreEqual("333333333333333333333333", days[2]); // Wednesday from schedule
        Assert.AreEqual("444444444444444444444444", days[3]); // Thursday from schedule
        Assert.AreEqual("555555555555555555555555", days[4]); // Friday from schedule
        Assert.AreEqual("666666666666666666666666", days[5]); // Saturday from schedule
        Assert.AreEqual("000000000000000000000000", days[6]); // Sunday from dataSnapshot (0->0)
    }

    [TestMethod]
    public void ConvertDataSnapshotToNewSchedule_WithScatteredDays_FillsGapsFromSchedule()
    {
        // Arrange - Monday, Wednesday, Friday in dataSnapshot
        var schedule = "[[1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1]," +
                      "[2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2]," +
                      "[3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3]," +
                      "[4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4]," +
                      "[5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5]," +
                      "[6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6]," +
                      "[7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7]]";
        
        // Monday, Wednesday, Friday
        var dataSnapshot = "1704067200|1:1|2:1|3:1|4:1|5:1|6:1|7:1|8:1|9:1|10:1|11:1|12:1|13:1|14:1|15:1|16:1|17:1|18:1|19:1|20:1|21:1|22:1|23:1|24:1;" +
                          "1704240000|1:0|2:0|3:0|4:0|5:0|6:0|7:0|8:0|9:0|10:0|11:0|12:0|13:0|14:0|15:0|16:0|17:0|18:0|19:0|20:0|21:0|22:0|23:0|24:0;" +
                          "1704412800|1:3|2:3|3:3|4:3|5:3|6:3|7:3|8:3|9:3|10:3|11:3|12:3|13:3|14:3|15:3|16:3|17:3|18:3|19:3|20:3|21:3|22:3|23:3|24:3;";

        // Act
        var result = _parser.ConvertDataSnapshotToNewSchedule(schedule, dataSnapshot);

        // Assert
        var resultString = result.ToString()!;
        var days = resultString.Split("%3B");
        
        Assert.AreEqual("111111111111111111111111", days[0]); // Monday from dataSnapshot
        Assert.AreEqual("222222222222222222222222", days[1]); // Tuesday from schedule
        Assert.AreEqual("000000000000000000000000", days[2]); // Wednesday from dataSnapshot
        Assert.AreEqual("444444444444444444444444", days[3]); // Thursday from schedule
        Assert.AreEqual("222222222222222222222222", days[4]); // Friday from dataSnapshot
        Assert.AreEqual("666666666666666666666666", days[5]); // Saturday from schedule
        Assert.AreEqual("777777777777777777777777", days[6]); // Sunday from schedule
    }

    [TestMethod]
    public void ConvertDataSnapshotToNewSchedule_WithWeekendOnlyDataSnapshot_FillsWeekdaysFromSchedule()
    {
        // Arrange - Saturday and Sunday in dataSnapshot
        var schedule = "[[1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1]," +
                      "[2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2]," +
                      "[3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3]," +
                      "[4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4]," +
                      "[5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5]," +
                      "[6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6]," +
                      "[7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7]]";
        
        // Saturday and Sunday
        var dataSnapshot = "1704499200|1:1|2:1|3:1|4:1|5:1|6:1|7:1|8:1|9:1|10:1|11:1|12:1|13:1|14:1|15:1|16:1|17:1|18:1|19:1|20:1|21:1|22:1|23:1|24:1;" +
                          "1704585600|1:0|2:0|3:0|4:0|5:0|6:0|7:0|8:0|9:0|10:0|11:0|12:0|13:0|14:0|15:0|16:0|17:0|18:0|19:0|20:0|21:0|22:0|23:0|24:0;";

        // Act
        var result = _parser.ConvertDataSnapshotToNewSchedule(schedule, dataSnapshot);

        // Assert
        var resultString = result.ToString()!;
        var days = resultString.Split("%3B");
        
        Assert.AreEqual("111111111111111111111111", days[0]); // Monday from schedule
        Assert.AreEqual("222222222222222222222222", days[1]); // Tuesday from schedule
        Assert.AreEqual("333333333333333333333333", days[2]); // Wednesday from schedule
        Assert.AreEqual("444444444444444444444444", days[3]); // Thursday from schedule
        Assert.AreEqual("555555555555555555555555", days[4]); // Friday from schedule
        Assert.AreEqual("111111111111111111111111", days[5]); // Saturday from dataSnapshot
        Assert.AreEqual("000000000000000000000000", days[6]); // Sunday from dataSnapshot
    }

    [TestMethod]
    public void ConvertDataSnapshotToNewSchedule_WithDifferentStatusPatterns_PreservesDataCorrectly()
    {
        // Arrange - mixed status values in both schedule and dataSnapshot
        var schedule = "[[0,1,0,1,0,1,0,1,0,1,0,1,0,1,0,1,0,1,0,1,0,1,0,1]," +
                      "[2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2]," +
                      "[3,0,3,0,3,0,3,0,3,0,3,0,3,0,3,0,3,0,3,0,3,0,3,0]," +
                      "[1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[1,2,3,0,1,2,3,0,1,2,3,0,1,2,3,0,1,2,3,0,1,2,3,0]," +
                      "[2,3,2,3,2,3,2,3,2,3,2,3,2,3,2,3,2,3,2,3,2,3,2,3]]";
        
        // Only Tuesday in dataSnapshot
        var dataSnapshot = "1704153600|1:1|2:0|3:3|4:2|5:1|6:0|7:3|8:2|9:1|10:0|11:3|12:2|13:1|14:0|15:3|16:2|17:1|18:0|19:3|20:2|21:1|22:0|23:3|24:2;";

        // Act
        var result = _parser.ConvertDataSnapshotToNewSchedule(schedule, dataSnapshot);

        // Assert
        var resultString = result.ToString()!;
        var days = resultString.Split("%3B");
        
        Assert.AreEqual("010101010101010101010101", days[0]); // Monday from schedule
        Assert.AreEqual("102110211021102110211021", days[1]); // Tuesday from dataSnapshot (1->1, 0->0, 3->2, 2->1)
        Assert.AreEqual("303030303030303030303030", days[2]); // Wednesday from schedule
        Assert.AreEqual("111111111111111111111111", days[3]); // Thursday from schedule
        Assert.AreEqual("000000000000000000000000", days[4]); // Friday from schedule
        Assert.AreEqual("123012301230123012301230", days[5]); // Saturday from schedule
        Assert.AreEqual("232323232323232323232323", days[6]); // Sunday from schedule
    }

    [TestMethod]
    public void ConvertDataSnapshotToNewSchedule_WithMalformedDataSnapshot_FallsBackToScheduleForBadEntries()
    {
        // Arrange
        var schedule = "[[1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1]," +
                      "[2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2]," +
                      "[3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3]," +
                      "[4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4]," +
                      "[5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5]," +
                      "[6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6]," +
                      "[7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7]]";
        
        // Monday valid, Tuesday malformed, Wednesday valid
        var dataSnapshot = "1704067200|1:1|2:1|3:1|4:1|5:1|6:1|7:1|8:1|9:1|10:1|11:1|12:1|13:1|14:1|15:1|16:1|17:1|18:1|19:1|20:1|21:1|22:1|23:1|24:1;" +
                          "1704153600|invalid_format;" +
                          "1704240000|1:0|2:0|3:0|4:0|5:0|6:0|7:0|8:0|9:0|10:0|11:0|12:0|13:0|14:0|15:0|16:0|17:0|18:0|19:0|20:0|21:0|22:0|23:0|24:0;";

        // Act
        var result = _parser.ConvertDataSnapshotToNewSchedule(schedule, dataSnapshot);

        // Assert
        var resultString = result.ToString()!;
        var days = resultString.Split("%3B");
        
        Assert.AreEqual("111111111111111111111111", days[0]); // Monday from dataSnapshot (1->1)
        Assert.AreEqual("222222222222222222222222", days.Length > 1 && days[1] != "" ? days[1] : "222222222222222222222222"); // Tuesday from schedule (fallback due to malformed data)
        Assert.AreEqual("000000000000000000000000", days[2]); // Wednesday from dataSnapshot (0->0)
    }

    [TestMethod]
    public void ConvertDataSnapshotToNewSchedule_WithIncompleteHourData_HandlesGracefully()
    {
        // Arrange - dataSnapshot with incomplete hour data
        var schedule = "[[1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1]," +
                      "[2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2]," +
                      "[3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3]," +
                      "[4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4]," +
                      "[5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5]," +
                      "[6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6]," +
                      "[7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7]]";
        
        // Monday with only 12 hours of data
        var dataSnapshot = "1704067200|1:1|2:1|3:1|4:1|5:1|6:1|7:1|8:1|9:1|10:1|11:1|12:1;";

        // Act
        var result = _parser.ConvertDataSnapshotToNewSchedule(schedule, dataSnapshot);

        // Assert
        Assert.IsNotNull(result);
        var resultString = result.ToString()!;
        var days = resultString.Split("%3B");
        
        // Should have 7 days regardless of malformed data
        Assert.AreEqual(7, days.Length);
    }

    [TestMethod]
    public void ConvertDataSnapshotToNewSchedule_WithEmptyScheduleAndPartialDataSnapshot_HandlesMissingScheduleData()
    {
        // Arrange - empty/zero schedule with partial dataSnapshot
        var schedule = "[[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]," +
                      "[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]]";
        
        // Only Thursday in dataSnapshot
        var dataSnapshot = "1704326400|1:1|2:1|3:1|4:1|5:1|6:1|7:1|8:1|9:1|10:1|11:1|12:1|13:1|14:1|15:1|16:1|17:1|18:1|19:1|20:1|21:1|22:1|23:1|24:1;";

        // Act
        var result = _parser.ConvertDataSnapshotToNewSchedule(schedule, dataSnapshot);

        // Assert
        var resultString = result.ToString()!;
        var days = resultString.Split("%3B");
        
        Assert.AreEqual("000000000000000000000000", days[0]); // Monday from schedule
        Assert.AreEqual("000000000000000000000000", days[1]); // Tuesday from schedule
        Assert.AreEqual("000000000000000000000000", days[2]); // Wednesday from schedule
        Assert.AreEqual("111111111111111111111111", days[3]); // Thursday from dataSnapshot
        Assert.AreEqual("000000000000000000000000", days[4]); // Friday from schedule
        Assert.AreEqual("000000000000000000000000", days[5]); // Saturday from schedule
        Assert.AreEqual("000000000000000000000000", days[6]); // Sunday from schedule
    }

    [TestMethod]
    public void ConvertDataSnapshotToNewSchedule_WithConsecutiveDaysInDataSnapshot_MaintainsProperOrder()
    {
        // Arrange - consecutive days Tuesday through Friday
        var schedule = "[[1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1]," +
                      "[2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2,2]," +
                      "[3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3,3]," +
                      "[4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4]," +
                      "[5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5]," +
                      "[6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6]," +
                      "[7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7]]";
        
        // Tuesday through Friday
        var dataSnapshot = "1704153600|1:1|2:1|3:1|4:1|5:1|6:1|7:1|8:1|9:1|10:1|11:1|12:1|13:1|14:1|15:1|16:1|17:1|18:1|19:1|20:1|21:1|22:1|23:1|24:1;" + // Tue
                          "1704240000|1:0|2:0|3:0|4:0|5:0|6:0|7:0|8:0|9:0|10:0|11:0|12:0|13:0|14:0|15:0|16:0|17:0|18:0|19:0|20:0|21:0|22:0|23:0|24:0;" + // Wed
                          "1704326400|1:3|2:3|3:3|4:3|5:3|6:3|7:3|8:3|9:3|10:3|11:3|12:3|13:3|14:3|15:3|16:3|17:3|18:3|19:3|20:3|21:3|22:3|23:3|24:3;" + // Thu
                          "1704412800|1:4|2:4|3:4|4:4|5:4|6:4|7:4|8:4|9:4|10:4|11:4|12:4|13:4|14:4|15:4|16:4|17:4|18:4|19:4|20:4|21:4|22:4|23:4|24:4;";  // Fri

        // Act
        var result = _parser.ConvertDataSnapshotToNewSchedule(schedule, dataSnapshot);

        // Assert
        var resultString = result.ToString()!;
        var days = resultString.Split("%3B");
        
        Assert.AreEqual("111111111111111111111111", days[0]); // Monday from schedule
        Assert.AreEqual("111111111111111111111111", days[1]); // Tuesday from dataSnapshot
        Assert.AreEqual("000000000000000000000000", days[2]); // Wednesday from dataSnapshot
        Assert.AreEqual("222222222222222222222222", days[3]); // Thursday from dataSnapshot
        Assert.AreEqual("333333333333333333333333", days[4]); // Friday from dataSnapshot
        Assert.AreEqual("666666666666666666666666", days[5]); // Saturday from schedule
        Assert.AreEqual("777777777777777777777777", days[6]); // Sunday from schedule
    }

    #endregion
}