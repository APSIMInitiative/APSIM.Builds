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
    /// Environment variable containing the path to the apsim installers.
    /// </summary>
    private const string installersPath = "INSTALLERS_PATH";

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
            result = result.OrderByDescending(u => u.ReleaseDate);
            if (min >= 0)
                result = result.Where(u => u.Revision > min);
            if (n > 0)
                result = result.Take(n);
            return await result.ToListAsync();
        }
    }

    /// <summary>
    /// Upload an apsim nextgen installer. The installer should be attached as
    /// the request body.
    /// </summary>
    /// <param name="revision">Revision number of the installer.</param>
    /// <param name="platform">Target platform of the installer.</param>
    /// <returns></returns>
    [HttpPost("upload/installer")]
    [Authorize]
    public async Task<IActionResult> UploadInstallerAsync([FromForm] IFormFile file, uint revision, Platform platform)
    {
        string basePath = EnvironmentVariable.Read(installersPath, "Path to apsim installers");
        string fileName = GetInstallerFileName(revision, platform);
        string outputPath = Path.Combine(basePath, fileName);

        using (Stream output = System.IO.File.Open(outputPath, System.IO.FileMode.Create))
            using (Stream input = file.OpenReadStream())
                await input.CopyToAsync(output);

        return Ok();
    }

    /// <summary>
    /// Upload an autodocs file generated by a CI build of a pull request. The
    /// file should be attached as multipart form content.
    /// </summary>
    /// <remarks>
    /// The uploaded file will overwrite any existing file of the same name
    /// generated by this pull request. The file name will be retrieved from the
    /// Content-Disposition request header.
    /// </remarks>
    /// <param name="file">The file.</param>
    /// <param name="pullRequestNumber">Pull request number, used for archival purposes.</param>
    [HttpPost("upload/docs")]
    [Authorize]
    public async Task<IActionResult> UploadDocsAsync([FromForm] IFormFile file, uint pullRequestNumber)
    {
        string basePath = EnvironmentVariable.Read(documentationPath, "Path to autodocs");
        string prIdString = pullRequestNumber.ToString(CultureInfo.InvariantCulture);
        string outputPath = Path.Combine(basePath, prIdString);
        if (!Directory.Exists(outputPath))
            Directory.CreateDirectory(outputPath);
        string outputFile = Path.Combine(outputPath, file.FileName);

        using (Stream output = System.IO.File.Open(outputFile, System.IO.FileMode.Create))
            using (Stream input = file.OpenReadStream())
                await input.CopyToAsync(output);

        return Ok();
    }

    /// <summary>
    /// Download an apsim installer.
    /// </summary>
    /// <remarks>
    /// todo: consider an asynchronous approach. We're relying on the base
    /// class' File() method to read the stream, which doesn't have an
    /// asynchronous overload. We could read the file ourselves and pass the
    /// byte array into one of the File() overloads, but this results in huge
    /// memory allocations which aren't returned to the host afterward.
    /// 
    /// It would also be nice to externalise this functionality. The logic here
    /// arguably doesn't belong in the controller.
    /// </remarks>
    /// <param name="revision">The version of apsim to be downloaded.</param>
    /// <param name="platform">The target platform (valid values are Linux, MacOS, or Windows).</param>
    [HttpGet("download/{revision}/{platform}")]
    public IActionResult DownloadApsim(uint revision, Platform platform)
    {
        string basePath = EnvironmentVariable.Read(installersPath, "Path to apsim installers");
        string fileName = GetInstallerFileName(revision, platform);
        string filePath = Path.Combine(basePath, fileName);

        Stream file = System.IO.File.OpenRead(filePath);
        if (file == null)
            return BadRequest("File not found");

        return File(file, "application/octet-stream", fileName);
    }

    /// <summary>
    /// Get the installer file name for the specified version and platform. Note
    /// that this method returns the file name only (not the path to the file).
    /// </summary>
    /// <param name="revision">Revision number of the desired version.</param>
    /// <param name="platform">Target platform.</param>
    private string GetInstallerFileName(uint revision, Platform platform)
    {
        string ext = GetInstallerFileExtension(platform);
        return $"apsim-{revision}.{ext}";
    }

    /// <summary>
    /// Get the installer file name extension for the given platform. The return
    /// value will not include the leading period.
    /// </summary>
    /// <param name="platform">The platform.</param>
    private string GetInstallerFileExtension(Platform platform)
    {
        switch (platform)
        {
            case Platform.Linux:
                return "deb";
            case Platform.MacOS:
                return "dmg";
            case Platform.Windows:
                return "exe";
            default:
                throw new PlatformNotSupportedException($"Platform not supported: {platform}");
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

        // Get the pull request corresponding to this release.
        uint pullRequest = GetPullRequestNumber(revision);
        string pullRequestString = pullRequest.ToString(CultureInfo.InvariantCulture);

        string baseDocsPath = EnvironmentVariable.Read(documentationPath, "Documentation path");
        string filePath = Path.Combine(baseDocsPath, pullRequestString, "index.html");
        using (FileStream stream = System.IO.File.OpenRead(filePath))
            return new FileStreamResult(stream, "text/html; charset=utf-8");
    }

    /// <summary>
    /// Get the pull request number for the specified revision. Throw if no
    /// release is found.
    /// </summary>
    /// <param name="revision">Revision number.</param>
    private uint GetPullRequestNumber(uint revision)
    {
        using (INextGenDbContext context = generator.GenerateDbContext())
        {
            Upgrade upgrade = context.Upgrades.FirstOrDefault(u => u.Revision == revision);
            if (upgrade == null)
                throw new InvalidOperationException($"No release exists with revision number {revision}");
            return upgrade.PullRequestNumber;
        }
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
