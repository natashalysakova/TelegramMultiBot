using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TelegramMultiBot.Database;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.Database.Models;

namespace BotTests.Properties
{
    [TestClass]
    public class MonitorDataServiceTests
    {
        IMonitorDataService service;
        BoberDbContext context;

        public MonitorDataServiceTests()
        {
            context = GetContext(Guid.NewGuid().ToString());
            service = new MonitorDataService(context);
        }

        [TestMethod]
        public async Task GetLocationsTest()
        {
            var result = await service.GetLocations();

            Assert.HasCount(2, result);
        }

        [TestMethod]
        [DataRow("kem")]
        [DataRow("krem")]
        public async Task GetLocationByRegion_ReturnsExpectedResult (string region)
        {
            var result = await service.GetLocationByRegion(region);

            Assert.IsNotNull(result);
            Assert.AreEqual(region, result.Region);
        }

        [TestMethod]
        [DataRow("notExisting")]
        [DataRow("")]
        [DataRow(null)]
        [DataRow("     ")]
        public async Task GetLocationByRegion_ReturnsNull(string region)
        {
            var result = await service.GetLocationByRegion(region);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetAllGroupsTest()
        {
            var result = await service.GetAllGroups();

            Assert.IsNotNull(result);
            Assert.HasCount(6, result);
        }

        [TestMethod]
        [DataRow("kem")]
        [DataRow("krem")]
        public async Task GetGroupByCodeAndLocationRegion_ReturnsExpectedResult(string region)
        {
            var result = await service.GetGroupByCodeAndLocationRegion(region, "group_1");

            Assert.IsNotNull(result);
            Assert.AreEqual(region, result.LocationRegion);
            Assert.AreEqual("group_1", result.GroupCode);
        }

        private BoberDbContext GetContext(string name)
        {
            var builder = new DbContextOptionsBuilder<BoberDbContext>().UseInMemoryDatabase(name, options =>
            {

            });

            var context = new BoberDbContext(builder.Options);
            context.Seed();

            SeedGroups(context);

            context.SaveChanges();
            return context;
        }

        private void SeedGroups(BoberDbContext context)
        {
            foreach (var location in context.ElectricityLocations)
            {
                for (int i = 0; i < 3; i++)
                {
                    context.ElectricityGroups.Add(new ElectricityGroup
                    {
                        LocationRegion = location.Region,
                        DataSnapshot = string.Empty,
                        GroupCode = $"group_{i}",
                        GroupName = $"Group {i}"
                    });
                }
            }
        }
    }
}
