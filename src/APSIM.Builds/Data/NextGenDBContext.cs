using APSIM.Builds;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace APSIM.Builds.Data
{
    public class NextGenDBContext : DbContext, INextGenDbContext
    {
        public DbSet<Upgrade> Upgrades { get; set; }

        public NextGenDBContext(DbContextOptions<NextGenDBContext> options) : base(options)
        {
        }

        /// <summary>
        /// Override model creation so that we can map the Upgrades property
        /// to a table in the DB with a different name (ApsimX).
        /// </summary>
        /// <param name="modelBuilder">Model builder object.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Upgrade>().ToTable("ApsimX");
        }
    }
}
