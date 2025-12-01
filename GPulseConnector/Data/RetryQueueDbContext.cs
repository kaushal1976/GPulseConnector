using Microsoft.EntityFrameworkCore;
using GPulseConnector.Abstraction.Models;

namespace GPulseConnector.Data
{
    public class RetryQueueDbContext : DbContext
    {
        public RetryQueueDbContext(DbContextOptions<RetryQueueDbContext> options) : base(options) { }

        public DbSet<RetryQueueItem> RetryQueue => Set<RetryQueueItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<RetryQueueItem>(b =>
            {
                b.HasKey(q => q.Id);
                b.Property(q => q.Id).ValueGeneratedOnAdd(); // ensure SQLite autoincrement
                b.Property(q => q.PayloadType).IsRequired().HasMaxLength(512);
                b.Property(q => q.PayloadJson).IsRequired();
                b.Property(q => q.AttemptCount).HasDefaultValue(0);
                b.Property(q => q.PayloadHash).HasMaxLength(128);
                b.HasIndex(q => q.PayloadHash).IsUnique(false);
            });
        }
    }
}
