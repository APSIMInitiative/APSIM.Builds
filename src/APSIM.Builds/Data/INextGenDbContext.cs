using System;
using Microsoft.EntityFrameworkCore;

namespace APSIM.Builds
{
    public interface INextGenDbContext : IDisposable
    {
        int SaveChanges();
        DbSet<Upgrade> Upgrades { get; set; }
    }
}