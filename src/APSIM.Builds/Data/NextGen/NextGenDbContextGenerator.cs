using Microsoft.EntityFrameworkCore;

namespace APSIM.Builds.Data.NextGen;

/// <summary>
/// This class can construct nextgen DB context instances by reading from an
/// environment variable a connection string to a mysql DB.
/// </summary>
public class NextGenDbContextGenerator : INextGenDbContextGenerator
{
    /// <summary>
    /// Environment variable containing the nextgen DB connection string.
    /// </summary>
    private const string connectionStringVariable = "NG_DB_CONNECTION_STRING";

    /// <inheritdoc />
    public INextGenDbContext GenerateDbContext()
    {
        string connectionString = EnvironmentVariable.Read(connectionStringVariable, "NextGen DB connection string");
        var builder = new DbContextOptionsBuilder().UseLazyLoadingProxies().UseMySQL(connectionString);

        NextGenDBContext context = new NextGenDBContext(builder.Options);
        context.Database.EnsureCreated();
        return context;
    }
}
