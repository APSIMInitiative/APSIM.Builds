using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace APSIM.Builds.Models;

/// <summary>
/// An class encapsulating an APSIM Next Gen upgrade.
/// </summary>
[Table("ApsimX")]
public class Upgrade : IComparable<Upgrade>
{
    /// <summary>
    /// ID of the upgrade in the database.
    /// </summary>
    public int Id { get; private init; }

    /// <summary>
    /// Number/ID of the issue addressed by this upgrade.
    /// </summary>
    public uint IssueNumber { get; private init; }

    /// <summary>
    /// Number/ID of the pull request which generated this upgrade.
    /// </summary>
    public uint PullRequestNumber { get; private init; }

    /// <summary>
    /// Title of the issue addressed by this upgrade.
    /// </summary>
    public string IssueTitle { get; private init; }

    /// <summary>
    /// URL of the issue addressed by this upgrade.
    /// </summary>
    public string IssueUrl { get; private init; }

    /// <summary>
    /// Release date of the upgrade.
    /// </summary>
    public DateTime ReleaseDate { get; private init; }

    /// <summary>
    /// Revision number of the upgrade.
    /// </summary>
    public uint Revision { get; private init; }

    /// <summary>
    /// Create an <see cref="Upgrade" /> instance.
    /// </summary>
    /// <remarks>
    /// This constructor is provided for use by the entity framework, and
    /// should not be called directly from user code.
    /// </remarks>
    /// <param name="id">ID of the upgrade in the database.
    /// <param name="releaseDate">Release date of the upgrade.</param>
    /// <param name="issueNumber">Number/ID of the issue addressed by this upgrade.</param>
    /// <param name="pullRequestNumber">Number/ID of the pull request which generated this upgrade.</param>
    /// <param name="issueTitle">Upgrade title.</param>
    /// <param name="issueUrl">URL of the issue addressed by this upgrade.</param>
    /// <param name="revision">Revision number of the upgrade.</param>
    public Upgrade(int id, DateTime releaseDate, uint issueNumber, uint pullRequestNumber, string issueTitle, string issueUrl, uint revision)
        : this(issueNumber, pullRequestNumber, issueTitle, issueUrl, revision)
    {
        Id = id;
        ReleaseDate = releaseDate;
    }

    /// <summary>
    /// Create an <see cref="Upgrade" /> instance.
    /// </summary>
    /// <param name="issueNumber">Number/ID of the issue addressed by this upgrade.</param>
    /// <param name="pullRequestNumber">Number/ID of the pull request which generated this upgrade.</param>
    /// <param name="issueTitle">Upgrade title.</param>
    /// <param name="issueUrl">URL of the issue addressed by this upgrade.</param>
    /// <param name="revision">Revision number of the upgrade.</param>
    public Upgrade(uint issueNumber, uint pullRequestNumber, string issueTitle, string issueUrl, uint revision)
    {
        ReleaseDate = DateTime.Now;
        IssueNumber = issueNumber;
        PullRequestNumber = pullRequestNumber;
        IssueTitle = issueTitle;
        IssueUrl = issueUrl;
        Revision = revision;
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
