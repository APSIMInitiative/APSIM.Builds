namespace APSIM.Builds.Data.NextGen;

/// <summary>
/// An interface for a class which can construct NextGen DB contexts.
/// </summary>
public interface INextGenDbContextGenerator
{
    /// <summary>
    /// Create a NextGen DB context.
    /// </summary>
    INextGenDbContext GenerateDbContext();
}
