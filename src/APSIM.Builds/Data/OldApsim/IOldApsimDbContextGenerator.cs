namespace APSIM.Builds.Data.OldApsim;

/// <summary>
/// An interface for a class which can construct old apsim DB contexts.
/// </summary>
public interface IOldApsimDbContextGenerator
{
    /// <summary>
    /// Create an old apsim DB context.
    /// </summary>
    IOldApsimDbContext GenerateDbContext();
}
