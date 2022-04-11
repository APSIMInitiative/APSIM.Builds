using System;

namespace APSIM.Builds.Service
{
    /// <summary>
    /// Encapsulates an Apsim next gen release.
    /// </summary>
    public class Release
    {
        /// <summary>
        /// Release date.
        /// </summary>
        public DateTime ReleaseDate { get; set; }

        /// <summary>
        /// Issue number/ID.
        /// </summary>
        public uint Issue { get; set; }

        /// <summary>
        /// Release title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Download link for Debian installer.
        /// </summary>
        public string DownloadLinkDebian { get; set; }

        /// <summary>
        /// Download link for windows installer.
        /// </summary>
        public string DownloadLinkWindows { get; set; }

        /// <summary>
        /// Download link for macOS installer.
        /// </summary>
        public string DownloadLinkMacOS { get; set; }

        /// <summary>
        /// URL of release info (the github issue addressed by the release).
        /// </summary>
        public string InfoUrl { get; set; }

        /// <summary>
        /// Version number.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Revision number (this is included in the version number).
        /// </summary>
        /// <value></value>
        public uint Revision { get; set; }
    }
}
