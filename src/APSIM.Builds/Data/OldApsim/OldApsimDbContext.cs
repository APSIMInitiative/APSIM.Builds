using APSIM.Builds;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace APSIM.Builds.Data.OldApsim;

public class OldApsimDbContext : DbContext, IOldApsimDbContext
{
    public DbSet<Build> Builds { get; set; }

    /// <summary>
    /// Create a new <see cref="OldApsimDbContext"/>.
    /// </summary>
    /// <param name="options">DB context builder options.</param>
    public OldApsimDbContext(DbContextOptions options) : base(options)
    {
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync()
    {
        return base.SaveChangesAsync();
    }
}
