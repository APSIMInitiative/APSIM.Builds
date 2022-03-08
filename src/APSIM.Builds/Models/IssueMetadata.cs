namespace APSIM.Builds.Models;

/// <summary>
/// Github issue metadata.
/// </summary>
public class IssueMetadata
{
    /// <summary>
    /// Create a new <see cref="IssueMetadata"/> instance.
    /// </summary>
    /// <param name="number">Issue number.</param>
    /// <param name="title">Issue title.</param>
    /// <param name="url">Issue URL.</param>
    public IssueMetadata(uint number, string title, string url)
    {
        Number = number;
        Title = title;
        Url = url;
    }

    /// <summary>
    /// Github issue number.
    /// </summary>
    public uint Number { get; private init; }

    /// <summary>
    /// Issue title.
    /// </summary>
    public string Title { get; private init; }

    /// <summary>
    /// Issue URL.
    /// </summary>
    public string Url { get; private init; }
}
