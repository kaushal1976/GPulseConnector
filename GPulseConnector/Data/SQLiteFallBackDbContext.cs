using GPulseConnector.Abstraction.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace GPulseConnector.Data
{
    public class SQLiteFallbackDbContext : DbContext
    {
        public SQLiteFallbackDbContext(DbContextOptions<SQLiteFallbackDbContext> options)
            : base(options) { }

        public DbSet<RetryQueueItem> RetryQueue => Set<RetryQueueItem>();
        public DbSet<PatternMapping> PatternMappings => Set<PatternMapping>();

        public DbSet<MachineEvent> MachineEvents => Set<MachineEvent>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            
            modelBuilder.Entity<RetryQueueItem>(b =>
            {
                b.HasKey(q => q.Id);
                b.Property(q => q.Id)
                .ValueGeneratedOnAdd(); 

                b.Property(q => q.PayloadType)
                .IsRequired()
                .HasMaxLength(512);

                b.Property(q => q.PayloadJson)
                .IsRequired();

                b.Property(q => q.AttemptCount)
                .IsRequired()
                .HasDefaultValue(0); 

                b.Property(q => q.PayloadHash)
                .HasMaxLength(128);

                b.Property(q => q.CreatedOnUtc)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

                b.Property(q => q.LastError)
                .HasMaxLength(1024);

                b.HasIndex(q => q.PayloadHash)
                .IsUnique(false);
            });
        }
    }
}
