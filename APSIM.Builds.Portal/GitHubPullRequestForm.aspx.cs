namespace APSIM.Builds.Portal
{
    using APSIM.Shared.Utilities;
    using Octokit;
    using System;
    using System.IO;
    using System.Text;
    using APSIM.Builds;
    using Octokit.Internal;

    public partial class GitHubPullRequestForm : System.Web.UI.Page
    {
        /// <summary>
        /// Page has been loaded - called by GitHub when a pull request is opened, merged or closed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void Page_Load(object sender, EventArgs e)
        {
            string json = GetJsonFromInputStream();
            if (string.IsNullOrWhiteSpace(json))
            {
                Response.StatusCode = 200;
                return;
            }

            SimpleJsonSerializer octokitSerialiser = new SimpleJsonSerializer();
            PullRequestEventPayload payload = octokitSerialiser.Deserialize<PullRequestEventPayload>(json);

            if (payload == null || payload.PullRequest == null)
            {
                ShowMessage("Cannot find pull request in GitHub JSON.");
                Response.StatusCode = 400; // Bad request
            }
            else if (!payload.PullRequest.Merged)
                ShowMessage("Pull request not merged - ignored");
            else if (payload.PullRequest.GetIssueID() == -1)
                ShowMessage("Pull request doesn't reference an issue.");
            else if (payload.Repository.Owner.Login == "APSIMInitiative")
            {
                if (payload.Repository.Name == "ApsimX")
                {
                    // If an ApsimX Pull Request has been merged, start a CreateInstallation job on Jenkins.
                    string issueNumber = payload.PullRequest.GetIssueID().ToString();
                    string pullId = payload.PullRequest.Number.ToString();
                    string author = payload.PullRequest.User.Login;
                    string token = GetJenkinsToken();
                    string issueTitle = payload.PullRequest.GetIssueTitle("APSIMInitiative", "ApsimX");
                    bool released = payload.PullRequest.FixesAnIssue();
                    string mergeCommit = payload.PullRequest.MergeCommitSha;
                    string jenkinsUrl = $"http://apsimdev.apsim.info:8080/jenkins/job/CreateInstallation/buildWithParameters?token={token}&ISSUE_NUMBER={issueNumber}&PULL_ID={pullId}&COMMIT_AUTHOR={author}&ISSUE_TITLE={issueTitle}&RELEASED={released}&MERGE_COMMIT={mergeCommit}";
                    if (released)
                    {
                        WebUtilities.CallRESTService<object>(jenkinsUrl);
                        ShowMessage(string.Format("Triggered a deploy step for {0}'s pull request {1} - {2}", author, pullId, payload.PullRequest.Title));
                    }
                    else
                        ShowMessage($"No release will be generated {author}'s pull request #{pullId} - {payload.PullRequest.Title} as it doesn't resolve an issue");
                }
                else if (payload.Repository.Name == "APSIMClassic")
                {
                    // If an APSIM Classic Pull Request has been created, and it fixes an issue, start a ReleaseClassic job on Jenkins
                    string pullId = payload.PullRequest.Number.ToString();
                    string author = payload.PullRequest.User.Login;
                    if (payload.PullRequest.FixesAnIssue())
                    {
                        string token = GetJenkinsReleaseClassicToken();
                        string sha = payload.PullRequest.MergeCommitSha;
                        string jenkinsUrl = $"http://apsimdev.apsim.info:8080/jenkins/job/ReleaseClassic/buildWithParameters?token={token}&PULL_ID={pullId}&SHA1={sha}";
                        WebUtilities.CallRESTService<object>(jenkinsUrl);
                        ShowMessage($"Triggered a deploy step for {author}'s pull request #{pullId} - {payload.PullRequest.Title}");
                    }
                    else
                        ShowMessage($"No release will be generated {author}'s pull request #{pullId} - {payload.PullRequest.Title} as it doesn't resolve an issue");
                }
            }
        }

        /// <summary>Return the valid password for this web service.</summary>
        private static string GetValidPassword()
        {
            string connectionString = File.ReadAllText(@"D:\Websites\ChangeDBPassword.txt");
            int posPassword = connectionString.IndexOf("Password=");
            return connectionString.Substring(posPassword + "Password=".Length);
        }

        private static string GetClassicBuildPassword()
        {
            return File.ReadAllText(@"D:\Websites\ClassicBuildPassword.txt");
        }

        private string GetJenkinsToken()
        {
            return File.ReadAllText(@"D:\Websites\JenkinsToken.txt");
        }

        private string GetJenkinsReleaseClassicToken()
        {
            return File.ReadAllText(@"D:\Websites\ReleaseClassic.txt");
        }

        /// <summary>
        /// Get the JSON that GitHub has passed to us.
        /// </summary>
        /// <returns>The JSON string.</returns>
        private string GetJsonFromInputStream()
        {
            const int INPUT_BUFFER_LENGTH = 4096;
            byte[] data = new byte[INPUT_BUFFER_LENGTH];
            MemoryStream writer = new MemoryStream();
            Stream input = Request.InputStream;
            int rlen;
            while ((rlen = input.Read(data, 0, INPUT_BUFFER_LENGTH)) > 0)
                writer.Write(data, 0, rlen);

            string json = Encoding.ASCII.GetString(writer.ToArray());
            return json;
        }

        /// <summary>
        /// Write a message in the response stream.
        /// </summary>
        /// <param name="msg"></param>
        private void ShowMessage(string msg)
        {
            //File.WriteAllText(@"C:\inetpub\wwwroot\github.txt", msg);
            Response.Clear();
            Response.ContentType = "text/plain";
            Response.ContentEncoding = System.Text.Encoding.UTF8;
            Response.Write(msg);
            Response.End();
        }
    }
}