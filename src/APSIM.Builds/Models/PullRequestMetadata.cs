namespace APSIM.Builds.Models;

/// <summary>
/// Metadata for a github pull request.
/// </summary>
public class PullRequestMetadata
{
    /// <summary>
    /// Create a new <see cref="PullRequestMetadata"/> instance.
    /// </summary>
    /// <param name="issue">The issue referenced by the pull request.</param>
    /// <param name="resolvesIssue">True iff the pull request resolves the issue.</param>
    /// <param name="title">Pull request title.</param>
    /// <param name="author">Pull request author.</param>
    public PullRequestMetadata(IssueMetadata issue, bool resolvesIssue, string title, string author)
    {
        Issue = issue;
        ResolvesIssue = resolvesIssue;
        Title = title;
        Author = author;
    }

    /// <summary>
    /// Metadata related to the issue referenced by the pull request.
    /// </summary>
    /// <value></value>
    public IssueMetadata Issue { get; private init; }

    /// <summary>
    /// True iff the pull request resolves the issue.
    /// </summary>
    public bool ResolvesIssue { get; private init; }

    /// <summary>
    /// Pull request title.
    /// </summary>
    public string Title { get; private init; }

    /// <summary>
    /// Pull request author. For now, I'm using user.Login which will be github
    /// username, not display name.
    /// </summary>
    public string Author { get; private init; }
}
