using APSIM.Builds.Data.NextGen;
using APSIM.Builds.Models;
using APSIM.Builds.Utility;
using APSIM.Builds.VersionControl;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Octokit;
using Octokit.Internal;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Release = APSIM.Builds.Models.Release;

namespace APSIM.Builds.Controllers;

/// <summary>
/// APSIM Next Gen API controller.
/// </summary>
[Route("api/nextgen")]
[ApiController]
public class NextGenController : ControllerBase
{
    /// <summary>
    /// Name of the HMAC signature header provided by github.
    /// </summary>
    private const string xhubHeaderName = "X-Hub-Signature-256";

    /// <summary>
    /// Environment variable containing the URL of the jenkins server.
    /// </summary>
    private const string jenkinsUrl = "JENKINS_URL";

    /// <summary>
    /// Environment variable containing the token used to remotely start an
    /// apsim-release job on the jenkins server.
    /// </summary>
    private const string jenkinsToken = "JENKINS_TOKEN_NG";

    /// <summary>
    /// Environment variable containing the private key for HMAC-256
    /// signature verification of github webhook requests.
    /// </summary>
    private const string hmacSecret = "HMAC_SECRET_KEY";

    /// <summary>
    /// Environment variable containing the documentation path.
    /// </summary>
    private const string documentationPath = "DOCUMENTATION_PATH";

    /// <summary>
    /// Owner of the apsim repository.
    /// </summary>
    private const string apsimOwner = "APSIMInitiative";

    /// <summary>
    /// Name of the apsim repository.
    /// </summary>
    private const string apsimRepo = "ApsimX";

    /// <summary>
    /// DB context generator. We use a new DB context instance for each
    /// request, rather than reusing a single instance over the lifetime of
    /// the controller instance.
    /// </summary>
    private readonly INextGenDbContextGenerator generator;

    /// <summary>
    /// GitHub client used to make API requests to github's rest api.
    /// </summary>
    private readonly IGitHub github;

    /// <summary>
    /// Create a new <see cref="NextGenController"/> instance.
    /// </summary>
    /// <param name="dbContextGenerator">DB Context generator.</param>
    /// <param name="githubClient">Github client used to make API requests to github's rest api.</param>
    public NextGenController(INextGenDbContextGenerator dbContextGenerator, IGitHub githubClient)
    {
        generator = dbContextGenerator;
        github = githubClient;
    }

    /// <summary>
    /// Add a release build to the builds database.
    /// </summary>
    /// <param name="pullRequestNumber">The Number/ID of the github pull request which triggered this build.</param>
    [HttpPost("add")]
    [Authorize]
    public async Task AddBuild(uint pullRequestNumber)
    {
        // Retrieve release metadata from github.
        PullRequestMetadata pullRequest = await github.GetMetadataAsync(pullRequestNumber, apsimOwner, apsimRepo);
        IssueMetadata issue = pullRequest.Issue;

        using (INextGenDbContext context = generator.GenerateDbContext())
        {
            uint revision = GetNextRevisionNumber();
            Upgrade upgrade = new Upgrade(issue.Number, pullRequestNumber, issue.Title, issue.Url, revision);

            // Add the release to the builds DB.
            await context.Upgrades.AddAsync(upgrade);
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Enumerate the available releases.
    /// </summary>
    /// <param name="n">Number of upgrades to fetch, -1 for unlimited.</param>
    /// <param name="min">Min revision number. Return all upgrades more recent than this. -1 for unlimited.</param>
    [HttpPost("list")]
    [AllowAnonymous]
    public async Task<IEnumerable<Release>> ListReleasesAsync(int n = -1, int min = -1)
    {
        IEnumerable<Upgrade> upgrades = await ListUpgrades(n, min);
        return upgrades.Select(u => new Release(u));
    }

    /// <summary>
    /// Enumerate the available upgrades.
    /// </summary>
    /// <param name="n">Number of upgrades to fetch, -1 for unlimited.</param>
    /// <param name="min">Min revision number. Return all upgrades more recent than this. -1 for unlimited.</param>
    public async Task<IEnumerable<Upgrade>> ListUpgrades(int n = -1, int min = -1)
    {
        using (INextGenDbContext context = generator.GenerateDbContext())
        {
            IAsyncEnumerable<Upgrade> result = context.Upgrades.ToAsyncEnumerable();
            if (min >= 0)
                result = result.Where(u => u.Revision > min);
            if (n > 0)
                result = result.Take(n);
            return await result.ToListAsync();
        }
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
        try
        {
            // fixme: hash verification doesn't seem to be working (I get a
            // different hash to what github sends).
            // Validate the request signature.
            await ValidateGithubRequestAsync();
        }
        catch (Exception error)
        {
            return BadRequest(error.Message);
        }

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
            PullRequestMetadata pr = await githubClient.GetMetadataAsync((uint)payload.PullRequest.Number, apsimOwner, apsimRepo);
            if (!pr.ResolvesIssue)
                return Ok("Ignored: pull request does not resolve an issue");

            string jenkinsUrlBase = EnvironmentVariable.Read(jenkinsUrl, "Jenkins URL");
            string token = EnvironmentVariable.Read(jenkinsToken, "Jenkins apsim-release remote execution token");

            // todo: figure out if all of these parameters are actually
            // used by the build scripts. If not, remove them.
            string url = $"{jenkinsUrlBase}/job/apsim-release/buildWithParameters";
            string parameters = $"?token={token}&ISSUE_NUMBER={pr.Issue.Number}&PULL_ID={payload.PullRequest.Number}&COMMIT_AUTHOR={payload.PullRequest.User.Login}&ISSUE_TITLE={pr.Issue.Title}&RELEASED=true&MERGE_COMMIT={payload.PullRequest.MergeCommitSha}";

            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(url);
            HttpResponseMessage response = await client.GetAsync(parameters);
            response.EnsureSuccessStatusCode();

            return Ok("Initiated a release build of apsim");
        }
    }

    /// <summary>
    /// Get documentation HTML for the specified version.
    /// </summary>
    /// <param name="version">Version number. Can be null for latest version.</param>
    [HttpGet("docs")]
    [AllowAnonymous]
    public FileStreamResult GetDocumentationHtmlForVersion(string version = null)
    {
        uint revision;
        if (string.IsNullOrEmpty(version))
            using (INextGenDbContext context = generator.GenerateDbContext())
                revision = GetLatestRevision(context);
        else
            revision = ParseVersionString(version);

        string revisionString = revision.ToString(CultureInfo.InvariantCulture);
        string baseDocsPath = EnvironmentVariable.Read(documentationPath, "Documentation path");
        string filePath = Path.Combine(baseDocsPath, revisionString, "index.html");
        using (FileStream stream = System.IO.File.OpenRead(filePath))
            return new FileStreamResult(stream, "text/html; charset=utf-8");
    }

    /// <summary>
    /// Parse a revision number from a version string.
    /// </summary>
    /// <param name="version">Version number.</param>
    private uint ParseVersionString(string version)
    {
        string revisionString = version.Split('.').Select(v => v.Trim()).Where(v => v != "0").LastOrDefault();
        return Convert.ToUInt32(revisionString);
    }

    private async Task ValidateGithubRequestAsync()
    {
        string signature = Request.GetHeader(xhubHeaderName);
        string githubHmacKey = EnvironmentVariable.Read(hmacSecret, "HMAC secret key");
        await HashUtils.VerifyRequestHmac256Async(signature, githubHmacKey, Request.Body);
    }

    /// <summary>
    /// Get the next revision number.
    /// </summary>
    [HttpGet("nextversion")]
    [AllowAnonymous]
    public uint GetNextRevisionNumber()
    {
        using (INextGenDbContext context = generator.GenerateDbContext())
        {
            return GetLatestRevision(context) + 1;
        }
    }

    /// <summary>
    /// Get the latest revision number.
    /// </summary>
    /// <param name="context">DB context.</param>
    private static uint GetLatestRevision(INextGenDbContext context)
    {
        if (context.Upgrades.Any())
            return context.Upgrades.Max(u => u.Revision);
        return 0;
    }
}
