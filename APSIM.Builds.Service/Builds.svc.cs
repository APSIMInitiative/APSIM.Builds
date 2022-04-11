
namespace APSIM.Builds.Service
{
    using Octokit;
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.ServiceModel;
    using System.ServiceModel.Web;
    using System.Text;
    using System.Threading.Tasks;
    using System.Globalization;
    using APSIM.Shared.Web;
    using System.Net.Http;
    using System.Net;
    using Newtonsoft.Json;

    /// <summary>
    /// Web service that provides access to the ApsimX builds system.
    /// </summary>
    public class Builds : IBuilds
    {
        /// <summary>
        /// Owner of the ApsimX repo on github.
        /// </summary>
        private const string owner = "APSIMInitiative";

        /// <summary>
        /// Name of the ApsimX repo on github.
        /// </summary>
        private const string repo = "ApsimX";

        /// <summary>Add a build to the build database.</summary>
        /// <param name="pullRequestNumber">The GitHub pull request number.</param>
        /// <param name="changeDBPassword">The password</param>
        public void AddBuild(int pullRequestNumber, string changeDBPassword)
        {
            if (changeDBPassword == BuildsClassic.GetValidPassword())
            {
                using (SqlConnection connection = BuildsClassic.Open())
                {
                    string sql = "INSERT INTO ApsimX (Date, PullRequestID, IssueNumber, IssueTitle, Released, Version) " +
                                 "VALUES (@Date, @PullRequestID, @IssueNumber, @IssueTitle, @Released, @Version)";

                    PullRequest pull = GitHubUtilities.GetPullRequest(pullRequestNumber, owner, repo);
                    DateTime date = pull.GetTestDate(owner, repo);
                    pull.GetIssueDetails(out int issueNumber, out bool released);
                    string issueTitle = pull.GetIssueTitle(owner, repo);
                    int nextVersion = Convert.ToInt32(GetNextVersion());
                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.Add(new SqlParameter("@Date", date));
                        command.Parameters.Add(new SqlParameter("@PullRequestID", pullRequestNumber));
                        command.Parameters.Add(new SqlParameter("@IssueNumber", issueNumber));
                        command.Parameters.Add(new SqlParameter("@IssueTitle", issueTitle));
                        command.Parameters.Add(new SqlParameter("@Released", released));
                        command.Parameters.Add(new SqlParameter("@Version", nextVersion));
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>Add a green build to the build database.</summary>
        /// <param name="pullRequestNumber">The GitHub pull request number.</param>
        /// <param name="buildTimeStamp">The build time stamp</param>
        /// <param name="changeDBPassword">The password</param>
        public void AddGreenBuild(int pullRequestNumber, string buildTimeStamp, string changeDBPassword)
        {
            if (changeDBPassword == BuildsClassic.GetValidPassword())
            {
                using (SqlConnection connection = BuildsClassic.Open())
                {
                    string sql = "INSERT INTO ApsimX (Date, PullRequestID, IssueNumber, IssueTitle, Released) " +
                                 "VALUES (@Date, @PullRequestID, @IssueNumber, @IssueTitle, @Released)";

                    DateTime date = DateTime.ParseExact(buildTimeStamp, "yyyy.MM.dd-HH:mm", null);
                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.Add(new SqlParameter("@Date", date));
                        command.Parameters.Add(new SqlParameter("@PullRequestID", pullRequestNumber));
                        command.Parameters.Add(new SqlParameter("@IssueNumber", string.Empty));
                        command.Parameters.Add(new SqlParameter("@IssueTitle", string.Empty));
                        command.Parameters.Add(new SqlParameter("@Released", false));
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        private void WriteToLog(string msg)
        {
            string logFile = @"D:\Websites\builds.log";
            File.AppendAllLines(logFile, new[] { msg });
        }

        /// <summary>
        /// Get the next version number.
        /// </summary>
        public uint GetNextVersion()
        {
            return GetLatestRevisionNumber() + 1;
        }

        /// <summary>
        /// Get the latest version number.
        /// </summary>
        public uint GetLatestRevisionNumber()
        {
            using (SqlConnection connection = BuildsClassic.Open())
            {
                string sql = "SELECT MAX([Version]) FROM ApsimX";
                using (SqlCommand command = new SqlCommand(sql, connection))
                    // This will throw OverFlowException if last version is < 0.
                    return Convert.ToUInt32((int)command.ExecuteScalar());
            }
        }

        /// <summary>
        /// Gets a list of possible upgrades since the specified Apsim version.
        /// </summary>
        /// <param name="version">Fully qualified (a.b.c.d) version number.</param>
        /// <returns>List of possible upgrades.</returns>
        public List<Upgrade> GetUpgradesSinceVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return GetAllUpgrades();

            if (!int.TryParse(version, NumberStyles.Integer, CultureInfo.InvariantCulture, out int revision))
            {
                int? maybeRevision = GetRevisionFromVersion(version);
                if (maybeRevision == null)
                    return GetAllUpgrades();
                revision = (int)maybeRevision;
            }

            return GetUpgradesSinceIssue(revision);
        }

        /// <summary>
        /// Attmept to parse a revision number from a version string. If string is invalid, return null.
        /// </summary>
        /// <param name="version">String to be parsed</param>
        private int? GetRevisionFromVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return null;
            string[] parts = version.Split('.');
            if (parts.Length != 4)
                return null;
            if (int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
                return result;
            return null;
        }

        private async Task<T> PostAsync<T>(string url)
        {
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.PostAsync(url, null);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    string message = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Received http response {response.StatusCode}: {message}");
                }
                string responseBody = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(responseBody);
            }
        }

        /// <summary>
        /// Gets the N most recent upgrades.
        /// </summary>
        /// <param name="n">Number of upgrades to fetch.</param>
        public List<Upgrade> GetLastNUpgrades(int n)
        {
            return GetReleases(n: n).Select(r => ToUpgrade(r)).ToList();
        }

        /// <summary>
        /// Gets a list of possible upgrades since the specified issue number.
        /// </summary>
        /// <param name="issueNumber">The issue number.</param>
        /// <returns>The list of possible upgrades.</returns>
        public List<Upgrade> GetUpgradesSinceIssue(int issueNumber)
        {
            return GetReleases(issueNumber).Select(r => ToUpgrade(r)).ToList();
        }

        private List<Release> GetReleases(int revision = -1, int n = -1)
        {
            Task<List<Release>> task = GetReleasesAsync(revision, n);
            task.Wait();
            if (task.Exception != null)
                throw task.Exception;
            return task.Result;

        }
        private async Task<List<Release>> GetReleasesAsync(int revision = -1, int min = -1)
        {
            return await PostAsync<List<Release>>($"https://builds.apsim.info/api/nextgen/list?min={revision}&min={min}");
        }

        private Upgrade ToUpgrade(Release release)
        {
            return new Upgrade()
            {
                IssueNumber = (int)release.Issue,
                IssueTitle = release.Title,
                IssueURL = release.InfoUrl,
                ReleaseDate = release.ReleaseDate,
                issueNumber = (int)release.Issue,
                ReleaseURL = release.DownloadLinkWindows,
                RevisionNumber = release.Revision
            };
        }

        private List<Upgrade> GetAllUpgrades()
        {
            return GetReleases().Select(r => ToUpgrade(r)).ToList();
        }

        private List<Upgrade> GetUpgrades(SqlDataReader reader)
        {
            List<Upgrade> upgrades = new List<Upgrade>();
            while (reader.Read())
            {
                int buildIssueNumber = (int)reader["IssueNumber"];
                int pullRequestID = (int)reader["PullRequestID"];
                bool released = (bool)reader["Released"];
                if (buildIssueNumber > 0)
                {
                    if (upgrades.Find(u => u.IssueNumber == buildIssueNumber) == null && released)
                    {
                        Upgrade upgrade = new Upgrade();
                        upgrade.ReleaseDate = (DateTime)reader["Date"];
                        upgrade.IssueNumber = buildIssueNumber;
                        upgrade.IssueTitle = (string)reader["IssueTitle"];
                        upgrade.IssueURL = @"https://github.com/APSIMInitiative/ApsimX/issues/" + buildIssueNumber;
                        int revision = (int)reader["Version"];
                        upgrade.RevisionNumber = Convert.ToUInt32(revision);
                        upgrade.ReleaseURL = $@"https://apsimdev.apsim.info/ApsimXFiles/{GetApsimXInstallerFileName(revision, pullRequestID)}";
                        upgrades.Add(upgrade);
                    }
                }
            }
            return upgrades;
        }

        /// <summary>
        /// Gets the URL of the latest version.
        /// </summary>
        /// <param name="operatingSystem">Operating system to get url for.</param>
        /// <returns>The URL of the latest version of APSIM Next Generation.</returns>
        public string GetURLOfLatestVersion(string operatingSystem)
        {
            List<Release> releases = GetReleases(n: 1);
            Release latest = releases[0];
            if (operatingSystem == "Debian")
                return latest.DownloadLinkDebian;
            else if (operatingSystem == "Mac")
                return latest.DownloadLinkMacOS;
            else
                return latest.DownloadLinkWindows;
        }

        /// <summary>
        /// Get the URL of the windows installer for a version with a given issue
        /// and pull request number.
        /// </summary>
        /// <param name="revision">Issue number of the release. For older versions, the revision number and issue number are the same.</param>
        /// <param name="pullID">Pull request number of the release.</param>
        private static string GetApsimXInstallerFileName(int revision, int pullID)
        {
            // ApsimX pull request #6713 will be the first "official"
            // .net core release.
            if (pullID >= 6713)
                return $"apsim-{revision}.exe";
            return $"ApsimSetup{revision}.exe";
        }

        /// <summary>
        /// Gets the version number of the latest build/upgrade.
        /// </summary>
        /// <remarks>
        /// This is just plain wrong. This little algorithm is hardcoded into
        /// so many different places. If you change this here, be sure to pay
        /// a visit to the following places:
        /// - APSIM.POStats.Collector
        /// - Jenkins release scripts (batch and bash)
        /// - APSIM.Registration.Service
        /// - Possibly others which I still haven't realised are broken.
        /// </remarks>
        public string GetLatestVersion()
        {
            List<Release> releases = GetReleases(n: 1);
            return releases[0].Version;
        }

        /// <summary>Get documentation HTML for the specified version.</summary>
        /// <param name="apsimVersion">The version to get the doc for. Can be null for latest version.</param>
        public Stream GetDocumentationHTMLForVersion(string apsimVersion)
        {
            if (apsimVersion == null)
                apsimVersion = GetLatestRevisionNumber().ToString(CultureInfo.InvariantCulture);

            WebOperationContext.Current.OutgoingResponse.ContentType = "text/html; charset=utf-8";
            var indexFileName = @"D:\Websites\ApsimX\Releases\" + apsimVersion + @"\index.html";
            string html = File.ReadAllText(indexFileName);
            return new MemoryStream(Encoding.UTF8.GetBytes(html));
        }


        private class Build
        {
            public DateTime date;
            public int pullRequestID;
            public int issueNumber;
            public string issueTitle;
            public string url;
        }
    }
}
