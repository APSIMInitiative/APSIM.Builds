using APSIM.Builds.Data.OldApsim;
using APSIM.Builds.Models;
using APSIM.Builds.VersionControl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace APSIM.Builds.Controllers;

/// <summary>
/// APSIM Classic API controller.
/// </summary>
[Route("api/oldapsim")]
[ApiController]
public class OldApsimController : ControllerBase
{
    /// <summary>
    /// Owner of the old apsim repo on github.
    /// </summary>
    private const string owner = "APSIMInitiative";

    /// <summary>
    /// Name of the old apsim repo on github.
    /// </summary>
    private const string repo = "APSIMClassic";

    /// <summary>
    /// DB context generator. We use a new DB context instance for each
    /// request, rather than reusing a single instance over the lifetime of
    /// the controller instance.
    /// </summary>
    private readonly IOldApsimDbContextGenerator generator;

    /// <summary>
    /// GitHub client used to make API requests to github's rest api.
    /// </summary>
    private readonly IGitHub github;

    /// <summary>
    /// Create a new <see cref="OldApsimController"/> instance.
    /// </summary>
    /// <param name="dbContextGenerator">DB Context generator.</param>
    /// <param name="githubClient">Github client used to make API requests to github's rest api.</param>
    public OldApsimController(IOldApsimDbContextGenerator dbContextGenerator, IGitHub githubClient)
    {
        generator = dbContextGenerator;
        github = githubClient;
    }

    /// <summary>
    /// Add a pull request to the builds DB and return the job ID.
    /// </summary>
    /// <remarks>
    /// This is called when a Jenkins CI run first starts.
    /// </remarks>
    /// <param name="pullRequestId">Pull request number.</param>
    /// <param name="jenkinsId">ID of the build on Jenkins.</param>
    [HttpPost("add")]
    [Authorize]
    public async Task<int> AddBuildAsync(uint pullRequestId, uint jenkinsId)
    {
        PullRequestMetadata pr = await github.GetMetadataAsync(pullRequestId, owner, repo);
        if (pr == null)
            throw new ArgumentException($"Pull request {pullRequestId} does not exist on {owner}/{repo}");

        Build build = new Build();
        build.Author = pr.Author;
        build.Title = pr.Issue.Title;
        build.BugID = pr.Issue.Number;
        build.StartTime = DateTime.Now;
        build.JenkinsID = jenkinsId;
        build.PullRequestID = pullRequestId;
        using (IOldApsimDbContext db = generator.GenerateDbContext())
        {
            EntityEntry<Build> entry = await db.Builds.AddAsync(build);
            await db.SaveChangesAsync();
            return entry.Entity.Id;
        }
    }

    /// <summary>
    /// Update a job's num diffs.
    /// </summary>
    /// <param name="number">ID of the job.</param>
    /// <param name="pass">True if the build passed. False otherwise.</param>
    [HttpGet("setnumdiffs")]
    [Authorize]
    public async Task<IActionResult> UpdateBuildAsync(uint jobID, uint numDiffs)
    {
        using (IOldApsimDbContext db = generator.GenerateDbContext())
        {
            Build build = await db.Builds.FindAsync((int)jobID);
            if (build == null)
                return BadRequest($"No build exists with ID {jobID}");

            build.NumDiffs = numDiffs;

            await db.SaveChangesAsync();
        }
        return Ok();
    }

    /// <summary>
    /// Update a job's status and set the end time to now.
    /// </summary>
    /// <param name="number">ID of the job.</param>
    /// <param name="pass">True if the build passed. False otherwise.</param>
    [HttpPost("update")]
    [Authorize]
    public async Task<IActionResult> UpdateBuildAsync(uint jobID, bool pass)
    {
        using (IOldApsimDbContext db = generator.GenerateDbContext())
        {
            Build build = await db.Builds.FindAsync((int)jobID);
            if (build == null)
                return BadRequest($"No build exists with ID {jobID}");

            build.Pass = pass;
            build.FinishTime = DateTime.Now;

            await db.SaveChangesAsync();
        }
        return Ok();
    }

    /// <summary>
    /// Get the latest revision number.
    /// </summary>
    [HttpPost("getrevision")]
    public async Task<IActionResult> GetLatestRevisionNumberAsync()
    {
        using (IOldApsimDbContext db = generator.GenerateDbContext())
        {
            Build latest = await db.Builds.ToAsyncEnumerable().LastOrDefaultAsync();
            if (latest == null)
                // No builds in DB. Revision numbers start at 0.
                return Ok(0u);

            return Ok(latest.RevisionNumber);
        }
    }

    /// <summary>
    /// Update the revision number for the given pull request
    /// </summary>
    /// <param name="pullRequestId">Pull request number.</param>
    /// <param name="revision">Revision number for the PR.</param>
    [HttpPost("setrevision")]
    [Authorize]
    public async Task<IActionResult> UpdateRevisionNumberAsync(uint pullRequestId, uint revision)
    {
        using (IOldApsimDbContext db = generator.GenerateDbContext())
        {
            Build build = await db.Builds.ToAsyncEnumerable().LastOrDefaultAsync(b => b.PullRequestID == pullRequestId);
            if (build == null)
                return BadRequest($"No build exists with pull request ID {pullRequestId}");

            // Check for any existing builds with this revision number.
            Build existing = await db.Builds.ToAsyncEnumerable().FirstOrDefaultAsync(b => b.RevisionNumber == revision);
            if (existing != null)
                return BadRequest($"Revision number {revision} already allocated to build {existing.Id} ({existing.Title})");

            build.RevisionNumber = revision;
            await db.SaveChangesAsync();
        }
        return Ok();
    }
}
