using System;
using APSIM.Builds.Models;
using Xunit;

namespace APSIM.Builds.Tests;

/// <summary>
/// Unit tests for the <see cref="Upgrade" /> class.
/// </summary>
public class UpgradeTests
{
    [Fact]
    public void TestReleaseUrl()
    {
        Upgrade upgrade = new Upgrade(37, 0, null, null, 1);
        Assert.Equal("https://apsimdev.apsim.info/ApsimXFiles/apsim-37.deb", upgrade.GetURL(Platform.Linux));
        Assert.Equal("https://apsimdev.apsim.info/ApsimXFiles/apsim-37.dmg", upgrade.GetURL(Platform.MacOS));
        Assert.Equal("https://apsimdev.apsim.info/ApsimXFiles/apsim-37.exe", upgrade.GetURL(Platform.Windows));
    }

    [Fact]
    public void TestConstructor()
    {
        Upgrade upgrade = new Upgrade(14, 153, "issuetitle", "issueuri", 1234);
        Assert.Equal<DateTime>(DateTime.Now.Date, upgrade.ReleaseDate.Date);
        Assert.Equal<uint>(14, upgrade.IssueNumber);
        Assert.Equal<uint>(153, upgrade.PullRequestNumber);
        Assert.Equal("issuetitle", upgrade.IssueTitle);
        Assert.Equal("issueuri", upgrade.IssueUrl);
        Assert.Equal<uint>(1234, upgrade.Revision);
    }

    [Fact]
    public void TestCompare()
    {
        Upgrade u1 = new Upgrade(1, new DateTime(1999, 12, 31), 0, 0, null, null, 1);
        Upgrade u2 = new Upgrade(2, new DateTime(2000, 1, 1), 0, 0, null, null, 1);
        Upgrade u3 = new Upgrade(3, new DateTime(2000, 1, 1), 0, 0, null, null, 1);
        Upgrade u4 = new Upgrade(4, new DateTime(2000, 1, 2), 0, 0, null, null, 1);
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
