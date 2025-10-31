using DtekParsers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BotTests
{
    [TestClass]
    public class LocationUtilityTests
    {

        [TestMethod]
        [DataRow("https://www.dtek-kem.com.ua/ua/shutdowns", "kem")]
        [DataRow("https://www.dtek-krem.com.ua/ua/shutdowns", "krem")]
        public void GetRegionByUrlTests(string url, string expectedRegion)
        {
            var region = LocationNameUtility.GetRegionByUrl(url);
            
            Assert.AreEqual(expectedRegion, region);
        }

        [TestMethod]
        [DataRow("kem", "м.Київ")]
        [DataRow("krem", "Київська область")]
        public void GetLocationByRegionTests(string region, string expectedLocation)
        {
            var location = LocationNameUtility.GetLocationByRegion(region);

            Assert.AreEqual(expectedLocation, location);
        }

        [TestMethod]
        [DataRow("https://www.dtek-kem.com.ua/ua/shutdowns", "м.Київ")]
        [DataRow("https://www.dtek-krem.com.ua/ua/shutdowns", "Київська область")]
        public void GetLocationByUrlTests(string url, string expectedLocation)
        {
            var location = LocationNameUtility.GetLocationByUrl(url);

            Assert.AreEqual(expectedLocation, location);
        }
    }
}
