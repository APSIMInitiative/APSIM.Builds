﻿
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
        /// Gets a list of possible upgrades since the specified issue number.
        /// </summary>
        /// <param name="issueNumber">The issue number.</param>
        /// <returns>The list of possible upgrades.</returns>
        public List<Upgrade> GetUpgradesSinceIssue(int issueNumber)
        {
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
                         "WHERE Date >= " + string.Format("'{0:yyyy-MM-ddThh:mm:ss tt}'", date) +
                         " ORDER BY Date DESC";

            using (SqlConnection connection = BuildsClassic.Open())
            {
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int buildIssueNumber = (int)reader["IssueNumber"];
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
                                    upgrade.ReleaseURL = @"http://apsimdev.apsim.info/ApsimXFiles/ApsimSetup" + buildIssueNumber + ".exe";
                                    upgrades.Add(upgrade);
                                }
                            }
                        }
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
                         "WHERE IssueNumber = " + issueNumber;

            using (SqlConnection connection = BuildsClassic.Open())
            {
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                            return @"http://apsimdev.apsim.info/ApsimXFiles/ApsimSetup" + issueNumber + ".exe";
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
                         "WHERE IssueNumber = " + issueNumber +
                         "ORDER BY Date DESC";
            using (SqlConnection connection = BuildsClassic.Open())
            {
                using (SqlCommand command = new SqlCommand(sql, connection))
                {
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

            return DateTime.Now.ToString("yyyy.MM.dd-HH:mm") + "," + issueID;
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
                            build.url = @"http://apsimdev.apsim.info/ApsimXFiles/ApsimSetup" + build.issueNumber + ".exe";
                            return build;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the version number of the latest build/upgrade.
        /// </summary>
        public string GetLatestVersion()
        {
            Build latest = GetLatestBuild();
            return latest.date.ToString("yyyy.MM.dd.") + latest.issueNumber;
        }

        /// <summary>Get latest documentation HTML.</summary>
        public Stream GetDocumentationHTML()
        {
            WebOperationContext.Current.OutgoingResponse.ContentType = "text/html; charset=utf-8";
            string html = "<html><head>" + Environment.NewLine;
            html += "<link rel=\"shortcut icon\" href=\"https://apsimnextgeneration.netlify.com/images/favicon.png\" type=\"image/x-icon\" />" +
                    "<link href=\"https://apsimnextgeneration.netlify.com/css/nucleus.css\" rel=\"stylesheet\" >" +
                    "<link href=\"https://apsimnextgeneration.netlify.com/css/font-awesome.min.css\" rel=\"stylesheet\">" +
                    "<link href=\"https://apsimnextgeneration.netlify.com/css/hybrid.css\" rel=\"stylesheet\">" +
                    "<link href=\"https://apsimnextgeneration.netlify.com/css/featherlight.min.css\" rel=\"stylesheet\">" +
                    "<link href=\"https://apsimnextgeneration.netlify.com/css/auto-complete.css\" rel=\"stylesheet\" >" +
                    "<link href=\"https://apsimnextgeneration.netlify.com/theme-original/style.css\" rel=\"stylesheet\">" +
                    "<link href=\"https://apsimnextgeneration.netlify.com/theme-original/variant-darkgreen.css\" rel=\"stylesheet\">" +
                    "<link rel=\"stylesheet\" href=\"https://apsimnextgeneration.netlify.com/css/bootstrap.min.css\" >";

            html += "</head><body>";
            Build latestBuild = GetLatestBuild();

            html += "<h2>Auto Generated Model Documentation for build <a href=\"https://github.com/APSIMInitiative/ApsimX/issues/" + latestBuild.issueNumber + "\">" +
                     latestBuild.issueNumber + "</a> " + latestBuild.date.ToLongDateString() + "</h2>";

            string pattern = "*" + latestBuild.issueNumber + ".pdf";
            foreach (string file in Directory.GetFiles(@"D:\WebSites\APSIM\ApsimxFiles", pattern))
            {
                string docURL = file.Replace(@"D:\WebSites\APSIM", "http://apsimdev.apsim.info");
                docURL = docURL.Replace('\\', '/');

                string modelName = Path.GetFileNameWithoutExtension(file);
                modelName = modelName.Replace(latestBuild.issueNumber.ToString(), "");
                html += "<a href=\"" + docURL + "\" target=\"_blank\">" + modelName + "</a><br/>" + Environment.NewLine;
            }

            html += "<h2>Under Review Models</h2>";
            foreach (string file in Directory.GetFiles(@"D:\WebSites\APSIM\ApsimxFiles\UnderReview", pattern))
            {
                string docURL = file.Replace(@"D:\WebSites\APSIM", "http://apsimdev.apsim.info");
                docURL = docURL.Replace('\\', '/');

                string modelName = Path.GetFileNameWithoutExtension(file);
                modelName = modelName.Replace(latestBuild.issueNumber.ToString(), "");
                html += "<a href=\"" + docURL + "\" target=\"_blank\">" + modelName + "</a><br/>" + Environment.NewLine;
            }

            // Add in extra docs.
            html += "<h2>Science Documentation</h2>";
            html += "<a href=\"http://apsimdev.apsim.info/Portals/0/Documentation/ApsimX/AgPastureScience.pdf\" target =\"_blank\"> AgPasture Science</a><br/>" + Environment.NewLine;
            html += "<a href=\"http://apsimdev.apsim.info/CLEM/Content/Home.htm\" target=\"_blank\">CLEM</a><br/>" + Environment.NewLine;
            html += "<a href=\"https://grazplan.csiro.au/wp-content/uploads/2007/08/TechPaperMay12.pdf\" target =\"_blank\">Stock Science</a><br/>" + Environment.NewLine;
            html += "</body></html>";

            return new MemoryStream(Encoding.UTF8.GetBytes(html));
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
