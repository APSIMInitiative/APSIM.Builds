using APSIM.Builds.Controllers;
using APSIM.Builds.Data.OldApsim;
using APSIM.Builds.Models;
using APSIM.Builds.Tests.Extensions;
using APSIM.Builds.VersionControl;
using Microsoft.AspNetCore.Mvc;
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
/// Unit tests for <see cref="OldApsimController"/> class.
/// </summary>
public class OldApsimControllerTests
{
    /// <summary>
    /// List of builds behind the controller. Changes made by the controller
    /// will be applied to this DB context.
    /// </summary>
    private readonly IOldApsimDbContext dbContext;

    /// <summary>
    /// The controller instance used for tests. Database access is mocked out to
    /// the above DB context.
    /// </summary>
    private readonly OldApsimController controller;

    /// <summary>
    /// Mocked github client. This has not been setup at all, so if any tests
    /// trigger github api calls, you'll need to setup the appropriate
    /// endpoitns.
    /// </summary>
    private readonly Mock<IGitHub> mockGithub;

    /// <summary>
    /// Initialise the testing environment.
    /// </summary>
    public OldApsimControllerTests()
    {
        // Use an in-memory DB for tests.
        var builder = new DbContextOptionsBuilder<OldApsimDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString());
        dbContext = new OldApsimDbContext(builder.Options);

        // Wrap the DB context in a proxy object, to ensure that the actual DB
        // doesn't get disposed of.
        Mock<IOldApsimDbContext> mockDb = new Mock<IOldApsimDbContext>();
        mockDb.Setup(d => d.Builds).Returns(() => dbContext.Builds);
        mockDb.Setup(d => d.SaveChangesAsync()).Returns(() => dbContext.SaveChangesAsync());

        // Create a mocked DB context generator to supply to the controller.
        var mockGenerator = new Mock<IOldApsimDbContextGenerator>();
        mockGenerator.Setup(p => p.GenerateDbContext()).Returns(mockDb.Object);

        // Create a mock github client. Any tests which require this will need
        // to configure it to suit their purposes.
        mockGithub = new Mock<IGitHub>();

        // Finally, setup the actual controller.
        controller = new OldApsimController(mockGenerator.Object, mockGithub.Object);
    }

    /// <summary>
    /// Test adding a build with a valid request, and ensure that the build is
    /// correctly added to the DB, and the returned build ID is correct.
    /// </summary>
    /// <param name="numExistingBuilds">
    /// Number of builds to be added to the DB before the test.
    /// </param>
    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(1)]
    public async Task TestAdd(ushort numExistingBuilds)
    {
        uint pullRequestId = 1234;
        uint jenkinsId = 142;

        // Add existing builds to the DB.
        await PopulateDB(numExistingBuilds);

        // Configure the Github client.
        PullRequestMetadata pullRequest = CreateSamplePullRequest();
        mockGithub.SetupPullRequest(pullRequestId, pullRequest);

        // Add a new build to the DB.
        uint result = await controller.AddBuildAsync(pullRequestId, jenkinsId);

        // Ensure the controller returned the ID of the new build.
        uint expectedId = (uint)numExistingBuilds + 1;
        Assert.Equal(expectedId, result);

        // Ensure inserted data is correct.
        Build actual = dbContext.Builds.Last();

        Assert.Equal(expectedId, actual.Id);
        Assert.Equal(pullRequest.Author, actual.Author);
        Assert.Equal(pullRequest.Issue.Title, actual.Title);
        Assert.Equal(pullRequest.Issue.Number, (uint)actual.BugID);
        Assert.Equal(DateTime.Now.Date, actual.StartTime.Date);
        Assert.Null(actual.FinishTime);
        Assert.Equal(jenkinsId, (uint)actual.JenkinsID);
        Assert.Equal(pullRequestId, (uint)actual.PullRequestID);
    }

    /// <summary>
    /// Attempt to add a build with an invalid pull request ID, and ensure that
    /// an appropriate error is returned.
    /// </summary>
    [Fact]
    public async Task TestAddInvalidPR()
    {
        // We haven't configured the github client for this pull request ID, so
        // in theory the controller should throw.
        await Assert.ThrowsAsync<ArgumentException>(async () => await controller.AddBuildAsync(666, 0));
    }

    /// <summary>
    /// Test updating a build with a valid request, and ensure that the build is
    /// correctly updated.
    /// </summary>
    /// <param name="pass">Build pass/fail status to be used.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TestUpdate(bool pass)
    {
        // Populate the DB.
        await PopulateDB(1);

        // Update the existing build (ID 1).
        IActionResult result = await controller.UpdateBuildAsync(1, pass);

        // Ensure result is a HTTP 200 (OK) response.
        Assert.IsType<OkResult>(result);

        Build build = dbContext.Builds.Last();

        // Ensure build was updated correctly.
        Assert.Equal(pass, build.Pass);
        Assert.Equal(DateTime.Now.Date, ((DateTime)build.FinishTime).Date);
    }

    /// <summary>
    /// Test updating a build with an invalid build ID, and ensure that an
    /// appropriate error is returned.
    /// </summary>
    /// <param name="pass">Build pass/fail status to be used.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TestUpdateInvalidID(bool pass)
    {
        // No build exists with this ID.
        IActionResult result = await controller.UpdateBuildAsync(42, pass);

        // Ensure response is a HTTP 400 (bad request) response.
        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Test the getrevision endpoint from a populated DB.
    /// </summary>
    /// <param name="numExistingBuilds">
    /// Number of builds to be added to the DB before the test.
    /// </param>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task TestGetRevision(ushort numExistingBuilds)
    {
        // Populate the DB with data.
        await PopulateDB(numExistingBuilds);

        // Get the next revision number.
        uint? responseValue = controller.GetLatestRevisionNumberAsync();

        // PopulateDB() will create N builds with revision numbers 0..N.
        uint expectedRevisionNumber = numExistingBuilds == 0 ? 0 : numExistingBuilds - 1u;
        Assert.Equal(expectedRevisionNumber, responseValue);
    }

    /// <summary>
    /// Test the setrevision endpoint with a valid request, and ensure that the
    /// DB is correctly updated.
    /// </summary>
    /// <param name="numBuildsToAdd">
    /// Number of builds to be added to the DB before the test.
    /// </param>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task TestSetRevision(ushort numBuildsToAdd)
    {
        uint pullRequestId = 14;
        uint revision = 2;

        // Create a build object.
        Build build = new Build();
        build.Author = "asdf";
        build.Title = "fdsa";
        build.BugID = 2;
        build.StartTime = DateTime.Now;
        build.JenkinsID = 15;
        build.PullRequestID =(uint)pullRequestId;

        // Add N builds to the DB.
        for (uint i = 0; i < numBuildsToAdd; i++)
        {
            build.Id = i + 1;
            await AddBuild(build);
        }

        // Update the revision number in the DB. This should modify the last
        // matching build only.
        IActionResult result = await controller.UpdateRevisionNumberAsync(pullRequestId, revision);

        // Ensure that the result is a HTTP 200 (OK) response.
        Assert.IsType<OkResult>(result);

        // Ensure that only the last build was modified, and that its revision
        // number has been correctly updated.
        List<Build> buildsInDb = dbContext.Builds.ToList();
        for (int i = 0; i < numBuildsToAdd; i++)
        {
            uint? expectedRevision = i == numBuildsToAdd - 1 ? revision : null;
            Assert.Equal(expectedRevision, (uint)buildsInDb[i].RevisionNumber);
        }
    }

    /// <summary>
    /// Test the setrevision endpoint with an invalid ID, and ensure that an
    /// appropriate error is returned.
    /// </summary>
    [Fact]
    public async Task TestSetRevisionInvalidID()
    {
        // Attempt to update the build with pull request ID 1. This should fail,
        // because the DB is empty.
        IActionResult result = await controller.UpdateRevisionNumberAsync(1, 2);

        // Ensure the result is a HTTP 400 (bad request) response.
        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Test the setrevision endpoint with an invalid revision (ie already in
    /// use), and ensure that an appropriate error is returned.
    /// </summary>
    [Fact]
    public async Task TestSetRevisionInvalidRevision()
    {
        // Add 2 builds to the DB. These will have pull request IDs of 3000 and
        // 3001, and revision numbers of 0 and 1, respectively.
        await PopulateDB(2);

        // Attempt to give the build with PR ID 3001 a revision number of 0.
        // This should fail, because this revision number is already allocated.
        IActionResult result = await controller.UpdateRevisionNumberAsync(3001, 0);

        // Ensure the reponse is a HTTP 400 (bad request) response.
        Assert.IsType<BadRequestObjectResult>(result);

        // Ensure the build was not modified.
        Build build = dbContext.Builds.Last();
        Assert.Equal(1u, (uint)build.RevisionNumber);
    }

    /// <summary>
    /// Seed the DB with a given number of builds.
    /// </summary>
    /// <param name="numBuilds">Number of builds to be added.</param>
    /// <returns>The builds which were inserted.</returns>
    private async Task<List<Build>> PopulateDB(ushort numBuilds)
    {
        List<Build> builds = new List<Build>((int)numBuilds);
        for (ushort i = 0; i < numBuilds; i++)
        {
            Build build = new Build();
            build.Author = $"Author {i}";
            build.Title = $"Build {i}";
            build.BugID = (uint) 1000 + i;
            build.StartTime = DateTime.Today.AddDays(-1).Date;
            build.FinishTime = DateTime.Today.Date;
            build.RevisionNumber = i;
            build.JenkinsID = (uint)2000 + i;
            build.PullRequestID = (uint)3000 + i;

            Build inserted = await AddBuild(build);
            builds.Add(inserted);
        }
        return builds;
    }

    /// <summary>
    /// Add a build to the DB, and save changes.
    /// </summary>
    /// <param name="build">Build to be added.</param>
    private async Task<Build> AddBuild(Build build)
    {
        EntityEntry<Build> inserted = await dbContext.Builds.AddAsync(build);
        await dbContext.SaveChangesAsync();
        return inserted.Entity;
    }

    /// <summary>
    /// Create a sample pull request.
    /// </summary>
    private PullRequestMetadata CreateSamplePullRequest()
    {
        IssueMetadata issue = new IssueMetadata(12, "issuetitle", "url");
        return new PullRequestMetadata(issue, true, "a s d f", "zyx");
    }
}
