using Microsoft.EntityFrameworkCore;
using GPulseConnector.Abstraction.Models;

namespace GPulseConnector.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<LogEntry> LogEntries => Set<LogEntry>();
        public DbSet<MachineEvent> MachineEvents => Set<MachineEvent>();
        public DbSet<PatternMapping> PatternMappings => Set<PatternMapping>();
        public DbSet<DeviceRecord> DeviceRecords => Set<DeviceRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<LogEntry>(b =>
            {
                b.HasKey(l => l.Id);
                b.Property(l => l.Message).IsRequired();
                b.Property(l => l.Level).HasMaxLength(50);
            });

            modelBuilder.Entity<DeviceInputState>(entity =>
            {
                entity.HasKey(x => x.Id);
                });

        }
    }
}
