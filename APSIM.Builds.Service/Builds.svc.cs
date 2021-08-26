
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
                    string sql = "INSERT INTO ApsimX (Date, PullRequestID, IssueNumber, IssueTitle, Released) " +
                                 "VALUES (@Date, @PullRequestID, @IssueNumber, @IssueTitle, @Released)";

                    DateTime date = DateTime.Now;
                    PullRequest pull = GitHubUtilities.GetPullRequest(pullRequestNumber, owner, repo);
                    pull.GetIssueDetails(out int issueNumber, out bool released);
                    string issueTitle = pull.GetIssueTitle(owner, repo);
                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.Add(new SqlParameter("@Date", date));
                        command.Parameters.Add(new SqlParameter("@PullRequestID", pullRequestNumber));
                        command.Parameters.Add(new SqlParameter("@IssueNumber", issueNumber));
                        command.Parameters.Add(new SqlParameter("@IssueTitle", issueTitle));
                        command.Parameters.Add(new SqlParameter("@Released", released));
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

        /// <summary>
        /// Gets a list of possible upgrades since the specified Apsim version.
        /// </summary>
        /// <param name="version">Fully qualified (a.b.c.d) version number.</param>
        /// <returns>List of possible upgrades.</returns>
        public List<Upgrade> GetUpgradesSinceVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return GetAllUpgrades();

            int issueNumber = 0;

            int lastDotPosition = version.LastIndexOf(".");
            int.TryParse(version.Substring(lastDotPosition + 1), out issueNumber);

            string dateFromVersion = version.Substring(0, lastDotPosition);
            string[] formats = new string[]
            {
                "yyyy.mm.dd",
                "yyyy.m.dd",
                "yyyy.mm.d",
                "yyyy.m.d"
            };
            DateTime issueResolvedDate;
            if (!DateTime.TryParseExact(dateFromVersion, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out issueResolvedDate))
                throw new Exception(string.Format("Date is not in a valid format: {0}.", dateFromVersion));
            return GetUpgradesSinceDate(issueResolvedDate).Where(u => u.IssueNumber != issueNumber).ToList();
        }

        /// <summary>
        /// Gets the N most recent upgrades.
        /// </summary>
        /// <param name="n">Number of upgrades to fetch.</param>
        public List<Upgrade> GetLastNUpgrades(int n)
        {
            using (SqlConnection connection = BuildsClassic.Open())
            {
                using (SqlCommand command = new SqlCommand("SELECT TOP (@NumRows) * FROM ApsimX WHERE Released = 1 ORDER BY Date DESC;", connection))
                {
                    if (n > 0)
                        command.Parameters.AddWithValue("@NumRows", n);
                    else
                        command.CommandText = "SELECT * FROM ApsimX ORDER BY Date DESC;";

                    using (SqlDataReader reader = command.ExecuteReader())
                        return GetUpgrades(reader);
                }
            }
        }

        /// <summary>
        /// Gets a list of possible upgrades since the specified issue number.
        /// </summary>
        /// <param name="issueNumber">The issue number.</param>
        /// <returns>The list of possible upgrades.</returns>
        public List<Upgrade> GetUpgradesSinceIssue(int issueNumber)
        {
            if (issueNumber <= 0)
                return GetAllUpgrades();

            DateTime date = GetIssueResolvedDate(issueNumber);
            // We need to filter the list of all upgrades to remove any upgrades which are on the same day and
            // fix the same issue.
            return GetUpgradesSinceDate(date).Where(u => u.IssueNumber != issueNumber || u.ReleaseDate.DayOfYear != date.DayOfYear).ToList();
        }

        /// <summary>
        /// Gets all list of released upgrades since a given date.
        /// </summary>
        /// <param name="date">The date.</param>
        /// <returns>List of possible upgrades.</returns>
        private List<Upgrade> GetUpgradesSinceDate(DateTime date)
        {
            List<Upgrade> upgrades = new List<Upgrade>();

            string sql = "SELECT * FROM ApsimX " +
                         "WHERE Date >= @Date" +
                         " ORDER BY Date DESC";

            using (SqlConnection connection = BuildsClassic.Open())
            {
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@Date", date.ToString("yyyy-MM-ddTHH:mm:ss"));
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        return GetUpgrades(reader);
                    }
                }
            }
        }

        private List<Upgrade> GetAllUpgrades()
        {
            using (SqlConnection connection = BuildsClassic.Open())
                using (SqlCommand command = new SqlCommand("SELECT * FROM ApsimX ORDER BY Date DESC;", connection))
                    using (SqlDataReader reader = command.ExecuteReader())
                        return GetUpgrades(reader);
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
                        upgrade.ReleaseURL = $@"http://apsimdev.apsim.info/ApsimXFiles/{GetApsimXInstallerFileName(buildIssueNumber, pullRequestID)}";
                        upgrades.Add(upgrade);
                    }
                }
            }
            return upgrades;
        }

        /// <summary>
        /// Gets a URL for a version that resolves the specified issue
        /// </summary>
        /// <param name="issueNumber">The issue number.</param>
        public string GetURLOfVersionForIssue(int issueNumber)
        {
            List<Upgrade> upgrades = new List<Upgrade>();

            DateTime issueResolvedDate = GetIssueResolvedDate(issueNumber);

            string sql = "SELECT * FROM ApsimX " +
                         "WHERE IssueNumber = @IssueNumber";

            using (SqlConnection connection = BuildsClassic.Open())
            {
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@IssueNumber", issueNumber);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int pullRequestID = (int)reader["PullRequestID"];
                            return $@"http://apsimdev.apsim.info/ApsimXFiles/{GetApsimXInstallerFileName(issueNumber, pullRequestID)}";
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the URL of the latest version.
        /// </summary>
        /// <param name="operatingSystem">Operating system to get url for.</param>
        /// <returns>The URL of the latest version of APSIM Next Generation.</returns>
        public string GetURLOfLatestVersion(string operatingSystem)
        {
            Build latestBuild = GetLatestBuild();
            if (operatingSystem == "Debian")
                return Path.ChangeExtension(latestBuild.url, ".deb");
            else if (operatingSystem == "Mac")
                return Path.ChangeExtension(latestBuild.url, ".dmg");
            else
                return latestBuild.url;
        }

        /// <summary>
        /// Return the date the specified issue was resolved.
        /// </summary>
        /// <param name="issueNumber">The issue number</param>
        /// <returns>The date.</returns>
        private DateTime GetIssueResolvedDate(int issueNumber)
        {
            DateTime resolvedDate = new DateTime(2015, 1, 1);

            string sql = "SELECT * FROM ApsimX " +
                         "WHERE IssueNumber = @IssueNumber " +
                         "ORDER BY Date DESC";
            using (SqlConnection connection = BuildsClassic.Open())
            {
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@IssueNumber", issueNumber);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                            resolvedDate = (DateTime)reader["Date"];
                    }
                }
            }

            return resolvedDate;
        }

        /// <summary>
        /// Get details about a GitHub pull request ID. Called from Jenkins.
        /// </summary>
        /// <param name="pullRequestID"></param>
        /// <returns>Format of return string is yyyy-MM-dd hh:mm tt,ID</returns>
        public string GetPullRequestDetails(int pullRequestID)
        {
            PullRequest pull = GitHubUtilities.GetPullRequest(pullRequestID, owner, repo);
            pull.GetIssueDetails(out int issueID, out _);

            if (issueID <= 0)
                throw new Exception("Cannot find issue number in pull request: " + pullRequestID);

            return DateTime.Now.ToString("yyyy.M.d-HH:mm") + "," + issueID;
        }
     
        /// <summary>Get the latest build.</summary>
        private static Build GetLatestBuild()
        {
            string sql = "SELECT TOP 1 * FROM ApsimX " +
                         " WHERE Released=1" +
                         " ORDER BY Date DESC";

            using (SqlConnection connection = BuildsClassic.Open())
            {
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            Build build = new Build();
                            build.date = (DateTime)reader["Date"];
                            build.pullRequestID = (int)reader["PullRequestID"]; ;
                            build.issueNumber = (int)reader["IssueNumber"];
                            build.issueTitle = (string)reader["IssueTitle"];
                            string fileName = GetApsimXInstallerFileName(build.issueNumber, build.pullRequestID);
                            build.url = $@"http://apsimdev.apsim.info/ApsimXFiles/{fileName}";
                            return build;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Get the URL of the windows installer for a version with a given issue
        /// and pull request number.
        /// </summary>
        /// <param name="issueNumber">Issue number of the release.</param>
        /// <param name="pullID">Pull request number of the release.</param>
        private static string GetApsimXInstallerFileName(int issueNumber, int pullID)
        {
            // ApsimX pull request #6713 will be the first "official"
            // .net core release.
            if (pullID >= 6713)
                return $"apsim-{issueNumber}.exe";
            return $"ApsimSetup{issueNumber}.exe";
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
            Build latest = GetLatestBuild();
            return latest.date.ToString("yyyy.M.d.") + latest.issueNumber;
        }

        /// <summary>Get documentation HTML for the specified version.</summary>
        /// <param name="apsimVersion">The version to get the doc for. Can be null for latest version.</param>
        public Stream GetDocumentationHTMLForVersion(string apsimVersion)
        {
            if (apsimVersion == null)
                apsimVersion = GetLatestVersion();

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
