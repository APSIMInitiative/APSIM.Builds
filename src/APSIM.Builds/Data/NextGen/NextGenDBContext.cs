using APSIM.Builds.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace APSIM.Builds.Data.NextGen;

/// <summary>
/// DB context for the APSIM builds database.
/// </summary>
public class NextGenDBContext : DbContext, INextGenDbContext
{
    /// <summary>
    /// Available upgrades/versions of apsim.
    /// </summary>
    public DbSet<Upgrade> Upgrades { get; set; }

    /// <summary>
    /// Create a new <see cref="NextGenDBContext"/>.
    /// </summary>
    /// <param name="options">DB context builder options.</param>
    public NextGenDBContext(DbContextOptions options) : base(options)
    {
    }

    /// <summary>
    /// Save changes to the DB.
    /// </summary>
    public Task<int> SaveChangesAsync()
    {
        return base.SaveChangesAsync();
    }
}
