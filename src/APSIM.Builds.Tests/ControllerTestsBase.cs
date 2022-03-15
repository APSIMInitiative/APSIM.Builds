using APSIM.Builds.Models;
using APSIM.Builds.VersionControl;
using Moq;

namespace APSIM.Builds.Tests.Extensions;

public static class TestExtensions
{
    /// <summary>
    /// Configure the mocked github client to return a particular set of
    /// metadata for the given pull request number.
    /// </summary>
    /// <param name="pullRequestNumber">Pull request number.</param>
    /// <param name="metadata">Metadata which should be returned.</param>
    public static void SetupPullRequest(this Mock<IGitHub> client, uint pullRequestNumber, PullRequestMetadata metadata)
    {
        client.Setup(g => g.GetMetadataAsync(pullRequestNumber, It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(metadata);
    }
}
