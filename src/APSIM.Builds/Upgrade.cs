using System;

namespace APSIM.Builds
{
    /// <summary>
    /// An class encapsulating an APSIM Next Gen upgrade.
    /// </summary>
    public class Upgrade : IComparable<Upgrade>
    {
        /// <summary>
        /// ID of the upgrade in the database.
        /// </summary>
        /// <value></value>
        public uint Id { get; set; }

        /// <summary>
        /// Release date of the upgrade.
        /// </summary>
        public DateTime ReleaseDate { get; set; }

        /// <summary>
        /// Number/ID of the issue addressed by this upgrade.
        /// </summary>
        public uint IssueNumber { get; set; }

        /// <summary>
        /// Number/ID of the pull request which generated this upgrade.
        /// </summary>
        public uint PullRequestNumber { get; set; }

        /// <summary>
        /// Title of the issue addressed by this upgrade.
        /// </summary>
        public string IssueTitle { get; set; }

        /// <summary>
        /// URL of the issue addressed by this upgrade.
        /// </summary>
        public string IssueUrl { get; set; }

        /// <summary>
        /// Is this upgrade released?
        /// </summary>
        /// <remarks>
        /// This will be true iff the pull request fixed an issue.
        /// </remarks>
        public bool Released { get; set; }

        /// <summary>
        /// Create an <see cref="Upgrade" /> instance.
        /// </summary>
        /// <param name="date">Release date of the upgrade.</param>
        /// <param name="issue">Number/ID of the issue addressed by this upgrade.</param>
        /// <param name="title">Upgrade title.</param>
        /// <param name="issueUrl">URL of the issue addressed by this upgrade.</param>
        public Upgrade(DateTime date, uint issue, uint pullRequest, string title, string issueUrl)
        {
            ReleaseDate = date;
            IssueNumber = issue;
            PullRequestNumber = pullRequest;
            IssueTitle = title;
            IssueUrl = issueUrl;
        }

        /// <summary>
        /// URL of the installer for this upgrade.
        /// </summary>
        public string GetURL(Platform platform)
        {
            // fixme
            string ext = GetInstallerFileExtension(platform);
            return $"https://apsimdev.apsim.info/ApsimXFiles/apsim-{IssueNumber}.{ext}";
        }

        /// <summary>
        /// Get the file extension (without a leading period) of the installer for the given platform.
        /// </summary>
        /// <param name="platform">The platform.</param>
        private string GetInstallerFileExtension(Platform platform)
        {
            switch (platform)
            {
                case Platform.Linux:
                    return "deb";
                case Platform.Windows:
                    return "exe";
                case Platform.MacOS:
                    return "dmg";
                default:
                    throw new NotImplementedException($"Unknown platform {platform}");
            }
        }

        /// <summary>
        /// Compare two upgrades and determine which was released earlier.
        /// </summary>
        /// <param name="other">The upgrade to which this one will be compared.</param>
        /// <returns>
        /// A signed number indicating the relative values of the two upgrade instances.
        /// Less than zero – This instance is earlier than the other.
        /// Zero – This instance is the same as the other.
        /// Greater than zero – This instance is later than the other.
        /// </returns>
        public int CompareTo(Upgrade other)
        {
            return ReleaseDate.CompareTo(other.ReleaseDate);
        }
    }
}
