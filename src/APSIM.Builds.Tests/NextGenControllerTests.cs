using APSIM.Builds.Controllers;
using APSIM.Builds.Data.NextGen;
using APSIM.Builds.Models;
using APSIM.Builds.VersionControl;
using APSIM.Builds.Tests.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace APSIM.Builds.Tests;

/// <summary>
/// Unit tests for the <see cref="Upgrade" /> class.
/// </summary>
public class NextGenControllerTests
{
    /// <summary>
    /// List of upgrades behind the DB Context. Changes made by the DB
    /// controller will be applied to this list.
    /// </summary>
    private readonly INextGenDbContext dbContext;

    /// <summary>
    /// The DB Controller instance. Database access is mocked out to access
    /// the above list of upgrades.
    /// </summary>
    private readonly NextGenController controller;

    /// <summary>
    /// Mocked github client. This has not been setup at all, so if any
    /// tests trigger github api calls, you'll need to setup the appropriate
    /// endpoints.
    /// </summary>
    private readonly Mock<IGitHub> mockGithub;

    /// <summary>
    /// Initialise the test environment.
    /// </summary>
    public NextGenControllerTests()
    {
        // The actual DB context used for the tests will be an in-memory DB.
        var builder = new DbContextOptionsBuilder<NextGenDBContext>().UseInMemoryDatabase(Guid.NewGuid().ToString());
        dbContext = new NextGenDBContext(builder.Options);

        // We wrap the actual DB context in a proxy db context to ensure
        // that the actual DB doesn't get disposed of.
        Mock<INextGenDbContext> mockContext = new Mock<INextGenDbContext>();
        mockContext.Setup(c => c.Upgrades).Returns(() => dbContext.Upgrades);
        mockContext.Setup(c => c.SaveChangesAsync()).Returns(() => dbContext.SaveChangesAsync());

        // Now we create a mocked DB generator to supply to the controller.
        var mockDbContextGenerator = new Mock<INextGenDbContextGenerator>();
        mockDbContextGenerator.Setup(p => p.GenerateDbContext()).Returns(mockContext.Object);

        // Create a mocked github client. It will be up to the individual
        // tests to ensure that this does something useful.
        mockGithub = new Mock<IGitHub>();

        // Now, create a new DB controller.
        controller = new NextGenController(mockDbContextGenerator.Object, mockGithub.Object);
    }

    /// <summary>
    /// Test adding a build with a standard/valid request, and ensure that
    /// the build is correctly added to the DB.
    /// </summary>
    [Fact]
    public async Task TestAddBuild()
    {
        IssueMetadata issue = new IssueMetadata(1234, "issue title", "issue url");
        PullRequestMetadata pullRequest = new PullRequestMetadata(issue, true, "PR title", "PR author");
        uint pullRequestNumber = 12345;
        mockGithub.SetupPullRequest(pullRequestNumber, pullRequest);
        await controller.AddBuild(pullRequestNumber);
        Assert.Single(dbContext.Upgrades);
        Upgrade upgrade = dbContext.Upgrades.First();
        Assert.Equal(1, upgrade.Id);
        // Technically this could fail if system time clocks over midnight
        // while the test is running.
        Assert.Equal(DateTime.Now.Date, upgrade.ReleaseDate.Date);
        Assert.Equal(issue.Number, upgrade.IssueNumber);
        Assert.Equal(pullRequestNumber, upgrade.PullRequestNumber);
        Assert.Equal(issue.Title, upgrade.IssueTitle);
        Assert.Equal(issue.Url, upgrade.IssueUrl);
        Assert.Equal(1u, upgrade.Revision);
    }

    /// <summary>
    /// Ensure that revision number is calculated correctly when the DB
    /// contains no jobs (ie empty DB).
    /// </summary>
    [Fact]
    public async Task TestFirstBuildRevision()
    {
        IssueMetadata issue = new IssueMetadata(1234, "issue title", "issue url");
        PullRequestMetadata pullRequest = new PullRequestMetadata(issue, true, "PR title", "asdf");
        uint id = 123;
        mockGithub.SetupPullRequest(id, pullRequest);

        await controller.AddBuild(id);
        await controller.AddBuild(id);

        Assert.Equal(1u, dbContext.Upgrades.First().Revision);
        Assert.Equal(2u, dbContext.Upgrades.Last().Revision);
    }

    /// <summary>
    /// Ensure that revision number is calculated correctly in a DB which
    /// already contains multiple builds.
    /// </summary>
    [Fact]
    public async Task TestRevisionNumber()
    {
        // Populate DB with initial data.
        Upgrade existing = new Upgrade(1, DateTime.Now, 1, 2, "", "", 324);
        await AddUpgrade(existing);

        IssueMetadata issue = new IssueMetadata(1234, "issue title", "issue url");
        PullRequestMetadata pullRequest = new PullRequestMetadata(issue, true, "PR title", "xyz");
        uint id = 123;
        mockGithub.SetupPullRequest(id, pullRequest);

        // Add a new build to the DB.
        await controller.AddBuild(id);

        // Ensure that the newly-inserted build has correct revision number.
        Assert.Equal(existing.Revision + 1, dbContext.Upgrades.Last().Revision);
    }

    /// <summary>
    /// Ensure that the nextversion endpoint returns the last revision
    /// number + 1.
    /// </summary>
    /// <param name="latestRevision">The latest revision number in the DB.</param>
    [Theory]
    [InlineData(42)]
    public async Task TestGetNextRevisionNumber(uint latestRevision)
    {
        await AddUpgrade(new Upgrade(1, DateTime.Now, 1, 2, "", "", latestRevision));
        uint nextRevision = controller.GetNextRevisionNumber();
        Assert.Equal(latestRevision + 1, nextRevision);
    }

    /// <summary>
    /// Ensure that nextrevision endpoint returns 1 for an empty DB.
    /// </summary>
    [Fact]
    public void TestGetFirstRevisionNumber()
    {
        uint revision = controller.GetNextRevisionNumber();
        Assert.Equal(1u, revision);
    }

    /// <summary>
    /// Ensure that the list endpoint returns nothing for an empty DB.
    /// </summary>
    [Fact]
    public async Task TestListEmptyDB()
    {
        IEnumerable<Upgrade> upgrades = await controller.ListUpgrades();
        Assert.False(upgrades.Any());
    }

    [Theory]
    [InlineData(3)]
    public async Task TestListAll(ushort numUpgrades)
    {
        // Populate the DB with some upgrades.
        List<Upgrade> added = await PopulateDB(numUpgrades);
        List<Upgrade> upgrades = (await controller.ListUpgrades()).ToList();

        Assert.Equal<int>(added.Count, upgrades.Count);
        for (int i = 0; i < upgrades.Count; i++)
            AssertEqual(added[i], upgrades[i]);
    }

    /// <summary>
    /// Test the max number of upgrades parameter passed to the LIST API
    /// endpoint.
    /// </summary>
    /// <param name="numToAdd">Number of upgrades with which to seed the DB.</param>
    /// <param name="numToList">Max number of upgrades to return.</param>
    [Theory]
    [InlineData(5, 2)]
    [InlineData(3, 20)]
    [InlineData(4, 4)]
    public async Task TestListN(ushort numToAdd, ushort numToList)
    {
        // Seed the DB.
        List<Upgrade> added = await PopulateDB(numToAdd);

        // List N upgrades.
        List<Upgrade> upgrades = (await controller.ListUpgrades(numToList)).ToList();

        // Check number of results in the return value.
        int expectedNumResults = numToList >= numToAdd ? numToAdd : numToList;
        Assert.Equal(expectedNumResults, upgrades.Count);

        added = added.Take(expectedNumResults).ToList();

        // Ensure that the results match what we added to the DB.
        for (int i = 0; i < expectedNumResults; i++)
            AssertEqual(added[i], upgrades[i]);
    }

    /// <summary>
    /// Test the min revision number parameter of the LIST API endpoint.
    /// </summary>
    /// <param name="numToAdd">Number of upgrades with which to seed the DB.</param>
    /// <param name="minRevision">Min revision number passed into the request.</param>
    [Theory]
    [InlineData(1, 6)]
    [InlineData(17, 1)]
    public async Task TestListMinRevision(ushort numToAdd, short minRevision)
    {
        // Seed the DB.
        List<Upgrade> added = await PopulateDB(numToAdd);

        // List upgrades more recent than the given revision number.
        List<Upgrade> response = (await controller.ListUpgrades(min: minRevision)).ToList();

        // Check number of results in return value.
        // The inserted upgrades' revision numbers start at 0.
        int expectedNumResults = Math.Max(0, numToAdd - minRevision - 1);
        Assert.Equal(expectedNumResults, response.Count);

        added = added.Where(u => u.Revision > minRevision).ToList();
        for (int i = 0; i < expectedNumResults; i++)
            AssertEqual(added[i], response[i]);
    }

    /// <summary>
    /// Test the LIST API endpoint when both max number and min revision
    /// number parameters are passed in.
    /// </summary>
    /// <param name="numToAdd">Number of upgrades with which to seed the DB.</param>
    /// <param name="n">Max number of upgrades parameter for the request.</param>
    /// <param name="min">Min revision number parameter for the request.</param>
    [Theory]
    [InlineData(10, 8, 5)]
    [InlineData(10, 8, 1)]
    [InlineData(10, 8, 0)]
    [InlineData(10, 8, 9)]
    [InlineData(10, 2, 6)]
    [InlineData(10, 2, 1)]
    public async Task TestList(ushort numToAdd, ushort n, ushort min)
    {
        // Seed the DB.
        List<Upgrade> added = await PopulateDB(numToAdd);

        // List upgrades more recent than the given revision number.
        List<Upgrade> response = (await controller.ListUpgrades(n: n, min: min)).ToList();

        // Check number of results in return value.
        // The inserted upgrades' revision numbers start at 0.
        int expectedNumResults = Math.Min(n, Math.Max(0, numToAdd - min - 1));
        Assert.Equal(expectedNumResults, response.Count);

        added = added.Where(u => u.Revision > min).Take(n).ToList();
        for (int i = 0; i < expectedNumResults; i++)
            AssertEqual(added[i], response[i]);
    }

    /// <summary>
    /// Seed the DB with a given number of upgrades.
    /// </summary>
    /// <param name="numUpgrades">Number of upgrades to be added.</param>
    /// <returns>The upgrades which were inserted.</returns>
    private async Task<List<Upgrade>> PopulateDB(ushort numUpgrades)
    {
        List<Upgrade> upgrades = new List<Upgrade>((int)numUpgrades);
        for (ushort i = 0; i < numUpgrades; i++)
        {
            Upgrade upgrade = new Upgrade(i, i * 1000u, $"Issue {i}", $"PR {i}", i);
            Upgrade inserted = await AddUpgrade(upgrade);
            upgrades.Add(inserted);
        }
        return upgrades;
    }

    /// <summary>
    /// Add an upgrade to the DB, and save changes.
    /// </summary>
    /// <param name="upgrade">Upgrade to be added.</param>
    private async Task<Upgrade> AddUpgrade(Upgrade upgrade)
    {
        EntityEntry<Upgrade> inserted = await dbContext.Upgrades.AddAsync(upgrade);
        await dbContext.SaveChangesAsync();
        return inserted.Entity;
    }

    /// <summary>
    /// Ensure that two Upgrade instances are equal. Throw if not.
    /// </summary>
    /// <param name="expected"></param>
    /// <param name="actual"></param>
    private void AssertEqual(Upgrade expected, Upgrade actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.IssueNumber, actual.IssueNumber);
        Assert.Equal(expected.PullRequestNumber, actual.PullRequestNumber);
        Assert.Equal(expected.IssueTitle, actual.IssueTitle);
        Assert.Equal(expected.IssueUrl, actual.IssueUrl);
        Assert.Equal(expected.ReleaseDate.Date, actual.ReleaseDate.Date);
        Assert.Equal(expected.Revision, actual.Revision);
    }
}
