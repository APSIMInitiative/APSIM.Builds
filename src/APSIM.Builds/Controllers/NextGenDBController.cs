using APSIM.Builds;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace APSIM.Builds.Controllers
{
    [Route("nextgen")]
    [ApiController]
    public class NextGenDBController : ControllerBase
    {
        private INextGenDbContext dbContext;

        public NextGenDBController(INextGenDbContextGenerator dbContextGenerator)
        {
            dbContext = dbContextGenerator.GenerateDbContext();
        }

        /// <summary>Add a build to the builds database.</summary>
        /// <param name="pullRequestNumber">The Number/ID of the github pull request which triggered this build.</param>
        [HttpPost]
        public void AddBuild(uint pullRequestNumber)
        {
            throw new NotImplementedException();
        }

        /// <summary>Set the upgrade's Released status to true.</summary>
        /// <param name="pullRequestNumber">The Number/ID of the github pull request which triggered this build.</param>
        [HttpPost]
        public void ReleaseUpgrade(uint pullRequestNumber)
        {
            Upgrade upgrade = dbContext.Upgrades.LastOrDefault(u => u.PullRequestNumber == pullRequestNumber);
            if (upgrade == null)
                throw new InvalidOperationException($"Invalid pull request ID {pullRequestNumber}");
            upgrade.Released = true;
            upgrade.ReleaseDate = DateTime.Now;
        }

        /// <summary>Get all upgrades which are more recent than a particular github issue.</summary>
        /// <param name="issueNumber">Number/ID of the github issue.</param>
        [HttpPost]
        public IEnumerable<Upgrade> GetUpgradesSinceIssue(int issueNumber)
        {
            throw new NotImplementedException();
        }

        /// <summary>Get all upgrades which are more recent than a particular apsim version.</summary>
        /// <param name="versionNumber">The version number.</param>
        [HttpPost]
        public IEnumerable<Upgrade> GetUpgradesSinceVersion(string versionNumber)
        {
            throw new NotImplementedException();
        }

        /// <summary>Get the N most recent upgrades.</summary>
        /// <param name="n">Number of upgrades to fetch.</param>
        [HttpPost]
        public IEnumerable<Upgrade> GetLastNUpgrades(int n)
        {
            throw new NotImplementedException();
        }

        /// <summary>Get the version number of the most recent build/upgrade.</summary>
        [HttpPost]
        public string GetLatestVersion()
        {
            throw new NotImplementedException();
        }

        /***** todo: these don't really belong here(?) *****/

        /// <summary>Get a GitHub issue ID from a pull request ID.</summary>
        /// <param name="pullRequestID">The Pull Request number.</param>
        [HttpPost]
        public int? GetIssueReferencedByPullRequest(int pullRequestID)
        {
            throw new NotImplementedException();
        }

        /// <summary>Get documentation HTML for the specified version.</summary>
        /// <param name="version">Version number. Can be null for latest version.</param>
        [HttpPost]
        public FileStreamResult GetDocumentationHtmlForVersion(string version = null)
        {
            throw new NotImplementedException();
        }
    }
}
