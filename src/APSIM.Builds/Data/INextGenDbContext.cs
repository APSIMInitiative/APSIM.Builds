using APSIM.Builds.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace APSIM.Builds.Data
{
    public interface INextGenDbContext : IDisposable
    {
        Task<int> SaveChangesAsync();
        DbSet<Upgrade> Upgrades { get; set; }
    }
}