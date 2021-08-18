using APSIM.Builds;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace APSIM.Builds.Data.OldApsim
{
    public class OldApsimDbContext : DbContext
    {
        public DbSet<Build> Builds { get; set; }

        /// <summary>
        /// Override model creation so that we can map the Builds property
        /// to a table in the DB with a different name (OldApsim).
        /// </summary>
        /// <param name="modelBuilder">Model builder object.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Upgrade>().ToTable("OldApsim");
        }
    }
}
