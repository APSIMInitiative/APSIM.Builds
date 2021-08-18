using System;
using APSIM.Builds;
using Xunit;

namespace APSIM.Builds.Tests
{
    /// <summary>
    /// Unit tests for the <see cref="Upgrade" /> class.
    /// </summary>
    public class UpgradeTests
    {
        [Fact]
        public void TestReleaseUrl()
        {
            Upgrade upgrade = new Upgrade(new DateTime(2000, 1, 1), 37, 0, null, null);
            Assert.Equal("https://apsimdev.apsim.info/ApsimXFiles/apsim-37.deb", upgrade.GetURL(Platform.Linux));
            Assert.Equal("https://apsimdev.apsim.info/ApsimXFiles/apsim-37.dmg", upgrade.GetURL(Platform.MacOS));
            Assert.Equal("https://apsimdev.apsim.info/ApsimXFiles/apsim-37.exe", upgrade.GetURL(Platform.Windows));
        }

        [Fact]
        public void TestConstructor()
        {
            Upgrade upgrade = new Upgrade(new DateTime(1900, 12, 31), 14, 153, "issuetitle", "issueuri");
            Assert.Equal<int>(1900, upgrade.ReleaseDate.Year);
            Assert.Equal<int>(12, upgrade.ReleaseDate.Month);
            Assert.Equal<int>(31, upgrade.ReleaseDate.Day);
            Assert.Equal<uint>(14, upgrade.IssueNumber);
            Assert.Equal<uint>(153, upgrade.PullRequestNumber);
            Assert.Equal("issuetitle", upgrade.IssueTitle);
            Assert.Equal("issueuri", upgrade.IssueUrl);
        }

        [Fact]
        public void TestCompare()
        {
            Upgrade u1 = new Upgrade(new DateTime(1999, 12, 31), 0, 0, null, null);
            Upgrade u2 = new Upgrade(new DateTime(2000, 1, 1), 0, 0, null, null);
            Upgrade u3 = new Upgrade(new DateTime(2000, 1, 1), 0, 0, null, null);
            Upgrade u4 = new Upgrade(new DateTime(2000, 1, 2), 0, 0, null, null);
            Assert.Equal<int>(-1, u1.CompareTo(u2));
            Assert.Equal<int>(-1, u1.CompareTo(u3));
            Assert.Equal<int>(-1, u1.CompareTo(u4));
            Assert.Equal<int>(1, u2.CompareTo(u1));
            Assert.Equal<int>(0, u2.CompareTo(u3));
            Assert.Equal<int>(-1, u2.CompareTo(u4));
            Assert.Equal<int>(1, u3.CompareTo(u1));
            Assert.Equal<int>(0, u3.CompareTo(u2));
            Assert.Equal<int>(-1, u3.CompareTo(u4));
            Assert.Equal<int>(1, u4.CompareTo(u1));
            Assert.Equal<int>(1, u4.CompareTo(u2));
            Assert.Equal<int>(1, u4.CompareTo(u3));
        }
    }
}
