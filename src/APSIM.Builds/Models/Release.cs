using System;

namespace APSIM.Builds.Models;

/// <summary>
/// Encapsulates an Apsim next gen release.
/// </summary>
public class Release
{
    /// <summary>
    /// Release date.
    /// </summary>
    public DateTime ReleaseDate { get; private init; }

    /// <summary>
    /// Issue number/ID.
    /// </summary>
    public uint Issue { get; private init; }

    /// <summary>
    /// Release title.
    /// </summary>
    public string Title { get; private init; }

    /// <summary>
    /// Download link for Debian installer.
    /// </summary>
    public string DownloadLinkDebian { get; private init; }

    /// <summary>
    /// Download link for windows installer.
    /// </summary>
    public string DownloadLinkWindows { get; private init; }

    /// <summary>
    /// Download link for macOS installer.
    /// </summary>
    public string DownloadLinkMacOS { get; private init; }

    /// <summary>
    /// URL of release info (the github issue addressed by the release).
    /// </summary>
    public string InfoUrl { get; set; }

    /// <summary>
    /// Version number.
    /// </summary>
    public string Version { get; private init; }

    /// <summary>
    /// Revision number (this is included in the version number).
    /// </summary>
    /// <value></value>
    public uint Revision { get; private init; }

    /// <summary>
    /// Create a new <see cref="Release"/> instance from an
    /// <see cref="Upgrade"/>.
    /// </summary>
    /// <param name="upgrade">An upgrade instance.</param>
    public Release(Upgrade upgrade)
    {
        ReleaseDate = upgrade.ReleaseDate;
        Issue = upgrade.IssueNumber;
        Title = upgrade.IssueTitle;
        const string downloadLinkFormat = "https://builds.apsim.info/api/nextgen/download/{0}/{1}";
        DownloadLinkDebian = string.Format(downloadLinkFormat, upgrade.Revision, "Linux");
        DownloadLinkWindows = string.Format(downloadLinkFormat, upgrade.Revision, "Windows");
        DownloadLinkMacOS = string.Format(downloadLinkFormat, upgrade.Revision, "MacOS");
        InfoUrl = $"https://github.com/APSIMInitiative/ApsimX/issues/{upgrade.IssueNumber}";
        Version = $"{upgrade.ReleaseDate:yyyy.MM}.{upgrade.Revision}.0";
        Revision = upgrade.Revision;
    }
}
