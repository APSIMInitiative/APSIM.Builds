using Microsoft.EntityFrameworkCore;

namespace APSIM.Builds.Data.OldApsim;

/// <summary>
/// This class can construct old apsim DB context instances by reading from an
/// environment variable a connection string to a mysql DB.
/// </summary>
public class OldApsimDbContextGenerator : IOldApsimDbContextGenerator
{
    /// <summary>
    /// Environment variable containing the old apsim DB connection string.
    /// </summary>
    private const string connectionStringVariable = "OLD_DB_CONNECTION_STRING";

    /// <inheritdoc />
    public IOldApsimDbContext GenerateDbContext()
    {
        string connectionString = EnvironmentVariable.Read(connectionStringVariable, "Old APSIM DB connection string");
        connectionString = connectionString.Replace("$MARIADB_USERNAME", "root")
                                           .Replace("$MARIADB_PASSWORD", "3sRz9DkbwStCGbXUeKLQSlVlTG9Qhd6x")
                                           .Replace("\"", "");

        var builder = new DbContextOptionsBuilder().UseLazyLoadingProxies().UseMySQL(connectionString);

        OldApsimDbContext context = new OldApsimDbContext(builder.Options);
        context.Database.EnsureCreated();
        return context;
    }
}
