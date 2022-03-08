using System.Threading.Tasks;
using APSIM.Builds.Models;

namespace APSIM.Builds.VersionControl
{
    /// <summary>
    /// An interface for a github (or other version control) API client.
    /// </summary>
    public interface IGitHub
    {
        /// <summary>
        /// Get pull request metadata.
        /// </summary>
        /// <param name="pullRequestNumber">Pull request number.</param>
        /// <param name="owner">Owner of the repository of the pull request.</param>
        /// <param name="repo">Name of the repository of the pull request.</param>
        Task<PullRequestMetadata> GetMetadataAsync(uint pullRequestNumber, string owner, string repo);
    }
}