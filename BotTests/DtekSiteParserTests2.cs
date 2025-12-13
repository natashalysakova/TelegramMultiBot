using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using TelegramMultiBot.BackgroundServies;
using TelegramMultiBot.Database;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.Tests.BackgroundServices;

[TestClass]
public class DtekSiteParserTests2
{
    private BoberDbContext _dbContext = null!;
    private IMonitorDataService _monitorDataService = null!;
    private Mock<IServiceProvider> _serviceProviderMock = null!;
    private Mock<ILogger<DtekSiteParser>> _loggerMock = null!;
    private Mock<IServiceScope> _scopeMock = null!;
    private Mock<IServiceScopeFactory> _scopeFactoryMock = null!;
    private Mock<ISvitlobotClient> _svitlobotClientMock = null!;
    private DtekSiteParser _dtekSiteParser = null!;

    [TestInitialize]
    public void Setup()
    {
        // Create in-memory database
        var options = new DbContextOptionsBuilder<BoberDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new BoberDbContext(options);
        _monitorDataService = new MonitorDataService(_dbContext);

        // Initialize mocks
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<DtekSiteParser>>();
        _scopeMock = new Mock<IServiceScope>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _svitlobotClientMock = new Mock<ISvitlobotClient>();

        // Setup service scope to return mocked service provider
        _scopeMock.Setup(x => x.ServiceProvider).Returns(_serviceProviderMock.Object);
        
        // Setup scope factory to create scope (this is what CreateScope extension method calls internally)
        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(_scopeMock.Object);
        
        // Setup service provider to return the scope factory
        _serviceProviderMock.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
            .Returns(_scopeFactoryMock.Object);
        
        // Setup service provider to return the real IMonitorDataService with in-memory DB
        _serviceProviderMock.Setup(x => x.GetService(typeof(IMonitorDataService)))
            .Returns(_monitorDataService);

        // Setup scope disposal
        _scopeMock.Setup(x => x.Dispose());

        _svitlobotClientMock.Setup(x => x.UpdateTimetable(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        // Create instance of DtekSiteParser with mocked dependencies
        _dtekSiteParser = new DtekSiteParser(_serviceProviderMock.Object, _loggerMock.Object, _svitlobotClientMock.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _dbContext?.Dispose();
    }

    [TestMethod]
    public async Task SendUpdatesToSvitlobot_WithEmptySvitlobotList_CompletesSuccessfully()
    {
        // Arrange
        // Database is empty, so GetAllSvitlobots will return empty list

        // Act
        await _dtekSiteParser.SendUpdatesToSvitlobot();

        // Assert
        var svitlobots = await _monitorDataService.GetAllSvitlobots();
        Assert.AreEqual(0, svitlobots.Count(), "Should have no svitlobots in database");
        
        // Verify no warnings were logged since there's nothing to process
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [TestMethod]
    public async Task SendUpdatesToSvitlobot_HappyPath_WithValidDataInDatabase_ProcessesSuccessfully()
    {
        // Arrange - Set up database with test data
        var location = new ElectricityLocation
        {
            Id = Guid.NewGuid(),
            Region = "Kyiv",
            Url = "https://example.com/schedule"
        };
        await _dbContext.ElectricityLocations.AddAsync(location);

        var group = new ElectricityGroup
        {
            Id = Guid.NewGuid(),
            LocationRegion = "Kyiv",
            GroupCode = "1.1",
            GroupName = "Group 1.1",
            DataSnapshot = "1704067200|1:1|2:1|3:1|4:1|5:1|6:1|7:1|8:1|9:1|10:1|11:1|12:1|13:1|14:1|15:1|16:1|17:1|18:1|19:1|20:1|21:1|22:1|23:1|24:1;"
        };
        await _dbContext.ElectricityGroups.AddAsync(group);

        var svitlobot = new SvitlobotData
        {
            Id = Guid.NewGuid(),
            SvitlobotKey = "test-key-123",
            GroupId = group.Id,
            Group = group
        };
        await _dbContext.Svitlobot.AddAsync(svitlobot);
        
        await _dbContext.SaveChangesAsync();

        _svitlobotClientMock.Setup(x => x.UpdateTimetable("test-key-123", It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
    
            await _dtekSiteParser.SendUpdatesToSvitlobot();
        
        // Assert
        var expectedSchedule = 
        "111111111111111111111111%3B" +
        "000000000000000000000000%3B" + 
        "000000000000000000000000%3B" + 
        "000000000000000000000000%3B" +
        "000000000000000000000000%3B" + 
        "000000000000000000000000%3B" + 
        "000000000000000000000000";

        _svitlobotClientMock
            .Verify(x => x.UpdateTimetable("test-key-123", expectedSchedule), Times.Once);  


    }

    [TestMethod]
    public async Task SendUpdatesToSvitlobot_WithNullDataSnapshot_UsesScheduleArray()
    {
        // Arrange
        var group = new ElectricityGroup
        {
            Id = Guid.NewGuid(),
            LocationRegion = "Kyiv",
            GroupCode = "1.1",
            GroupName = "Group 1.1",
            DataSnapshot = null // Null data snapshot - should use schedule array
        };
        await _dbContext.ElectricityGroups.AddAsync(group);

        var svitlobot = new SvitlobotData
        {
            Id = Guid.NewGuid(),
            SvitlobotKey = "test-key-123",
            GroupId = group.Id,
            Group = group
        };
        await _dbContext.Svitlobot.AddAsync(svitlobot);
        
        await _dbContext.SaveChangesAsync();

        _svitlobotClientMock.Setup(x => x.GetTimetable("test-key-123"))
            .ReturnsAsync(
@"19;&;Група 1.1;&;0
20;&;Група 1.2;&;0
21;&;Група 2.1;&;0
22;&;Група 2.2;&;0
23;&;Група 3.1;&;0
24;&;Група 3.2;&;0
25;&;Група 4.1;&;0
26;&;Група 4.2;&;0
27;&;Група 5.1;&;0
28;&;Група 5.2;&;0
29;&;Група 6.1;&;0
30;&;Група 6.2;&;0
;&&&;[[1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]]"
            );
        // Act

        await _dtekSiteParser.SendUpdatesToSvitlobot();
        
        // Assert

        var expectedSchedule = 
        "111111111111111111111111%3B" +
        "000000000000000000000000%3B" + 
        "000000000000000000000000%3B" + 
        "000000000000000000000000%3B" +
        "000000000000000000000000%3B" + 
        "000000000000000000000000%3B" + 
        "000000000000000000000000";
        _svitlobotClientMock
            .Verify(x => x.UpdateTimetable("test-key-123", expectedSchedule), Times.Once); 
    }

    [TestMethod]
    public async Task SendUpdatesToSvitlobot_WithMultipleSvitlobots_ProcessesAll()
    {
        // Arrange
        var group1 = new ElectricityGroup
        {
            Id = Guid.NewGuid(),
            LocationRegion = "Kyiv",
            GroupCode = "1.1",
            GroupName = "Group 1.1",
            DataSnapshot = "1704067200|1:1|2:1|3:1|4:1|5:1|6:1|7:1|8:1|9:1|10:1|11:1|12:1|13:1|14:1|15:1|16:1|17:1|18:1|19:1|20:1|21:1|22:1|23:1|24:1;"
        };
        await _dbContext.ElectricityGroups.AddAsync(group1);

        var group2 = new ElectricityGroup
        {
            Id = Guid.NewGuid(),
            LocationRegion = "Kyiv",
            GroupCode = "1.2",
            GroupName = "Group 1.2",
            DataSnapshot = "1704067200|1:0|2:0|3:0|4:0|5:0|6:0|7:0|8:0|9:0|10:0|11:0|12:0|13:0|14:0|15:0|16:0|17:0|18:0|19:0|20:0|21:0|22:0|23:0|24:0;"
        };
        await _dbContext.ElectricityGroups.AddAsync(group2);

        var svitlobot1 = new SvitlobotData
        {
            Id = Guid.NewGuid(),
            SvitlobotKey = "test-key-1",
            GroupId = group1.Id
        };
        await _dbContext.Svitlobot.AddAsync(svitlobot1);

        var svitlobot2 = new SvitlobotData
        {
            Id = Guid.NewGuid(),
            SvitlobotKey = "test-key-2",
            GroupId = group2.Id
        };
        await _dbContext.Svitlobot.AddAsync(svitlobot2);
        
        await _dbContext.SaveChangesAsync();

        // Act
        await _dtekSiteParser.SendUpdatesToSvitlobot();

        // Assert
        _svitlobotClientMock
            .Verify(x => x.UpdateTimetable(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2)); 
    }

    [TestMethod]
    public async Task AddSvitlobotKey_HappyPath_AddsRecordToDatabase()
    {
        // Arrange
        var group = new ElectricityGroup
        {
            Id = Guid.NewGuid(),
            LocationRegion = "Kyiv",
            GroupCode = "GPV1.1",
            GroupName = "Group 1.1",
            DataSnapshot = null
        };
        await _dbContext.ElectricityGroups.AddAsync(group);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _monitorDataService.AddSvitlobotKey("my-svitlobot-key", group.Id);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("my-svitlobot-key", result.SvitlobotKey);
        Assert.AreEqual(group.Id, result.GroupId);

        // Verify it's in the database
        var svitlobots = await _monitorDataService.GetAllSvitlobots();
        Assert.AreEqual(1, svitlobots.Count());
        Assert.AreEqual("my-svitlobot-key", svitlobots.First().SvitlobotKey);
    }

    [TestMethod]
    public async Task GetAllSvitlobots_HappyPath_ReturnsAllRecordsWithGroups()
    {
        // Arrange
        var group1 = new ElectricityGroup
        {
            Id = Guid.NewGuid(),
            LocationRegion = "Kyiv",
            GroupCode = "1.1",
            GroupName = "Group 1.1",
            DataSnapshot = "test-data-1"
        };
        var group2 = new ElectricityGroup
        {
            Id = Guid.NewGuid(),
            LocationRegion = "Lviv",
            GroupCode = "2.1",
            GroupName = "Group 2.1",
            DataSnapshot = "test-data-2"
        };
        
        await _dbContext.ElectricityGroups.AddRangeAsync(group1, group2);
        await _dbContext.SaveChangesAsync();

        await _monitorDataService.AddSvitlobotKey("key-1", group1.Id);
        await _monitorDataService.AddSvitlobotKey("key-2", group2.Id);

        // Act
        var result = await _monitorDataService.GetAllSvitlobots();

        // Assert
        Assert.AreEqual(2, result.Count());
        
        // Verify groups are loaded
        var svitlobot1 = result.First(x => x.SvitlobotKey == "key-1");
        Assert.IsNotNull(svitlobot1.Group);
        Assert.AreEqual("1.1", svitlobot1.Group.GroupCode);
        Assert.AreEqual("test-data-1", svitlobot1.Group.DataSnapshot);

        var svitlobot2 = result.First(x => x.SvitlobotKey == "key-2");
        Assert.IsNotNull(svitlobot2.Group);
        Assert.AreEqual("2.1", svitlobot2.Group.GroupCode);
        Assert.AreEqual("test-data-2", svitlobot2.Group.DataSnapshot);
    }
}
