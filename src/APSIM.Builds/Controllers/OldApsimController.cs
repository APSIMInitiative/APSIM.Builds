using APSIM.Builds.Data.OldApsim;
using APSIM.Builds.Models;
using APSIM.Builds.VersionControl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Octokit.Internal;
using Octokit;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;

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
    /// Environment variable containing the URL of the jenkins server.
    /// </summary>
    private const string jenkinsUrl = "JENKINS_URL";

    /// <summary>
    /// Environment variable containing the token used to remotely start an
    /// apsim-release job on the jenkins server.
    /// </summary>
    private const string jenkinsToken = "JENKINS_TOKEN_CLASSIC";

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
        build.BugID = (uint)pr.Issue.Number;
        build.StartTime = DateTime.Now;
        build.JenkinsID = (uint)jenkinsId;
        build.PullRequestID = (int)pullRequestId;
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

            build.NumDiffs = (int)numDiffs;

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
    public uint? GetLatestRevisionNumberAsync()
    {
        using (IOldApsimDbContext db = generator.GenerateDbContext())
        {

            var revisionNumber = db.Builds.Max(u => u.RevisionNumber);
            if (revisionNumber == null)
                // No builds in DB. Revision numbers start at 0.
                return 0;

            return Convert.ToUInt32(revisionNumber);
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

            build.RevisionNumber = (int)revision;
            await db.SaveChangesAsync();
        }
        return Ok();
    }


    /// <summary>
    /// Called when a pull request is merged on github. Triggers a CI build
    /// on jenkins if the pull request resolved an issue.
    /// 
    /// This is invoked by a github webhook.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> PullRequestMerged()
    {
        //try
        //{
        //    // fixme: hash verification doesn't seem to be working (I get a
        //    // different hash to what github sends).
        //    // Validate the request signature.
        //    await ValidateGithubRequestAsync();
        //}
        //catch (Exception error)
        //{
        //    return BadRequest(error.Message);
        //}

        // If a nextgen PR has been merged, and it resolves an issue,
        // trigger a release build on jenkins.
        using (StreamReader reader = new StreamReader(Request.Body))
        {
            string json = await reader.ReadToEndAsync();
            if (string.IsNullOrEmpty(json))
                return BadRequest("Empty payload");

            SimpleJsonSerializer serializer = new SimpleJsonSerializer();
            PullRequestEventPayload payload = serializer.Deserialize<PullRequestEventPayload>(json);
            if (payload == null || payload.PullRequest == null)
                return BadRequest("Payload does not contain a pull request");

            if (!payload.PullRequest.Merged)
                return Ok("Ignored: pull request is not merged");

            GitHub githubClient = new GitHub();
            PullRequestMetadata pr = await githubClient.GetMetadataAsync((uint)payload.PullRequest.Number, owner, repo);
            if (!pr.ResolvesIssue)
                return Ok("Ignored: pull request does not resolve an issue");

            string jenkinsUrlBase = EnvironmentVariable.Read(jenkinsUrl, "Jenkins URL");
            string token = EnvironmentVariable.Read(jenkinsToken, "Jenkins apsim-release remote execution token");

            // todo: figure out if all of these parameters are actually
            // used by the build scripts. If not, remove them.
            string url = $"{jenkinsUrlBase}/job/oldapsim-release/buildWithParameters";
            string parameters = $"?token={token}&PULL_ID={payload.PullRequest.Number}&COMMIT_AUTHOR={payload.PullRequest.User.Login}&ISSUE_TITLE={pr.Issue.Title}&RELEASED=true&MERGE_COMMIT={payload.PullRequest.MergeCommitSha}";

            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(url);
            HttpResponseMessage response = await client.GetAsync(parameters);
            response.EnsureSuccessStatusCode();

            return Ok("Initiated a release build of apsim");
        }
    }


    /// <summary>
    /// Enumerate the available upgrades.
    /// </summary>
    /// <param name="n">Number of upgrades to fetch, -1 for unlimited.</param>
    [HttpPost("list")]
    [AllowAnonymous]
    public IEnumerable<Build> List(int n = -1)
    {
        using (IOldApsimDbContext db = generator.GenerateDbContext())
        {
            return db.Builds.Take(1);

            //IAsyncEnumerable<Build> result = db.Builds.Where(b => b.RevisionNumber != null && 
            //                                                      b.Pass).ToAsyncEnumerable();
            //result = result.OrderByDescending(u => u.RevisionNumber);
            //if (n > 0)
            //    result = result.Take(n);
            //return await result.ToListAsync();
        }
    }
}
 