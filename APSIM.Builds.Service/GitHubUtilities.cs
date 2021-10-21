using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using APSIM.Shared.Utilities;
using Octokit;
namespace APSIM.Builds
{
    /// <summary>
    /// Encapsulates a GitHub pull request.
    /// </summary>
    public static class GitHubUtilities
    {
        /// <summary>
        /// List of keywords which may be used to indicate progress on an issue.
        /// </summary>
        private static readonly string[] workingOnSyntax = { "working on" };

        /// <summary>
        /// List of keywords which may be used to close an issue.
        /// </summary>
        /// <remarks>
        /// Taken from here: https://help.github.com/articles/closing-issues-using-keywords/
        /// </remarks>
        private static readonly string[] resolvesSyntax = { "close", "closes", "closed", "fix", "fixes", "fixed", "resolve", "resolves", "resolved" };

        /// <summary>
        /// Fetches information about a GitHub pull request.
        /// </summary>
        /// <param name="id">ID of the pull request.</param>
        /// <param name="owner">
        /// Owner of the GitHub repository on which the pull request was created.
        /// </param>
        /// <param name="repo">
        /// Name of the GitHub repository on which the pull request was created.
        /// </param>
        /// <returns>
        /// An Octokit PullRequest object representing the pull request.
        /// </returns>
        public static PullRequest GetPullRequest(int id, string owner, string repo)
        {
            GitHubClient client = new GitHubClient(new ProductHeaderValue(owner));
            client.Credentials = GetGitHubCredentials();
            Task<PullRequest> pullRequestTask = client.PullRequest.Get(owner, repo, id);
            pullRequestTask.Wait();
            return pullRequestTask.Result;
        }

        /// <summary>
        /// Fetches information about a GitHub issue.
        /// </summary>
        /// <param name="id">ID of the issue.</param>
        /// <param name="owner">
        /// Owner of the GitHub repository on which the issue was created.
        /// </param>
        /// <param name="repo">
        /// Name of the GitHub repository on which the issue was created.
        /// </param>
        /// <returns>
        /// An Octokit Issue object representing the issue.
        /// </returns>
        public static Issue GetIssue(int id, string owner, string repo)
        {
            GitHubClient client = new GitHubClient(new ProductHeaderValue(owner));
            client.Credentials = GetGitHubCredentials();
            Task<Issue> issueTask = client.Issue.Get(owner, repo, id);
            issueTask.Wait();
            return issueTask.Result;
        }

        /// <summary>
        /// Fetches the title of the issue addressed by a pull request.
        /// </summary>
        /// <param name="pullRequest"></param>
        /// <returns></returns>
        public static string GetIssueTitle(this PullRequest pullRequest, string owner, string repo)
        {
            int issueID = pullRequest.GetIssueID();
            Issue issue = GetIssue(issueID, owner, repo);
            return issue.Title;
        }

        /// <summary>
        /// Fetches the ID of the issue addressed by a pull request.
        /// </summary>
        /// <param name="pullRequest">The pull request.</param>
        /// <returns>The issue ID or -1 if not found.</returns>
        /// <remarks>
        /// An extension method for an Octokit PullRequest object.
        /// 
        /// This is made more complicated by the fact that the user can
        /// reference multiple issues from a single pull request body.
        /// We're only interested in the first issue referenced.
        /// </remarks>
        public static int GetIssueID(this PullRequest pullRequest)
        {
            GetIssueDetails(pullRequest, out int issueID, out bool resolves);
            return issueID;
        }

        /// <summary>
        /// Checks whether an issue will be closed by the pull request when
        /// it is merged.
        /// </summary>
        /// <param name="pullRequest">The pull request.</param>
        public static bool FixesAnIssue(this PullRequest pullRequest)
        {
            GetIssueDetails(pullRequest, out int issueID, out bool resolves);
            return resolves;
        }

        /// <summary>
        /// Get the date on which tests were run for the given pull request.
        /// </summary>
        /// <param name="pull"></param>
        public static DateTime GetTestDate(this PullRequest pull, string owner, string repo)
        {
            GitHubClient client = new GitHubClient(new ProductHeaderValue(owner));
            client.Credentials = GetGitHubCredentials();

            Task<IReadOnlyList<PullRequestCommit>> task = client.PullRequest.Commits(owner, repo, pull.Number);
            task.Wait();
            IReadOnlyList<PullRequestCommit> commits = task.Result;
            if (commits.Any())
            {
                Task<CombinedCommitStatus> statusTask = client.Repository.Status.GetCombined(owner, repo, commits.Last().Sha);
                statusTask.Wait();
                CombinedCommitStatus combined = statusTask.Result;
                if (combined.Statuses.Any())
                    return combined.Statuses.Last().UpdatedAt.LocalDateTime;
            }
            return pull.UpdatedAt.DateTime;
        }

        /// <summary>
        /// Gets details about the issue addressed by a pull request.
        /// </summary>
        /// <param name="pullRequest">The pull request.</param>
        /// <param name="issueID">
        /// ID of the first issue referenced using one of the keywords in
        /// <see cref="resolvesSyntax"/> and <see cref="workingOnSyntax"/>.
        /// </param>
        /// <param name="resolves">True iff the pull request resolves an issue.</param>
        public static void GetIssueDetails(this PullRequest pullRequest, out int issueID, out bool resolves)
        {
            issueID = -1;
            resolves = false;
            int pos = int.MaxValue;

            for (int i = 0; i < resolvesSyntax.Length + workingOnSyntax.Length; i++)
            {
                string syntax = i < resolvesSyntax.Length ? resolvesSyntax[i] : workingOnSyntax[i - resolvesSyntax.Length];
                Regex pattern = new Regex(syntax + @"\s+#(\d+)", RegexOptions.IgnoreCase);
                MatchCollection matches = pattern.Matches(pullRequest.Body);
                foreach (Match match in matches)
                {
                    if (match.Index < pos)
                    {
                        pos = match.Index;
                        issueID = int.Parse(match.Groups[1].Value);
                        if (i < resolvesSyntax.Length)
                            resolves = true;
                    }
                }
            }
        }

        /// <summary>
        /// Gets credentials which will be passed to octokit to make API requests.
        /// </summary>
        private static Credentials GetGitHubCredentials()
        {
            try
            {
                string token = File.ReadAllText(@"D:\Websites\GitHubToken.txt");
                Credentials creds = new Credentials(token);
                token = null;
                return creds;
            }
            catch
            {
                throw new Exception("Unable to find GitHub token.");
            }
        }
    }
}