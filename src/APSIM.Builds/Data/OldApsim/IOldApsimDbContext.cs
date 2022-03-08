using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace APSIM.Builds.Data.OldApsim;

public interface IOldApsimDbContext : IDisposable
{
    DbSet<Build> Builds { get; }

    /// <summary>
    /// Save all pending changes to the DB.
    /// </summary>
    Task<int> SaveChangesAsync();
}
