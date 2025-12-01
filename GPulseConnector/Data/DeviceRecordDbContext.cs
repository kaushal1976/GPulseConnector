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
    public class DeviceRecordDbContext : DbContext
    {
        public DeviceRecordDbContext(DbContextOptions<DeviceRecordDbContext> options)
            : base(options) { }

        public DbSet<DeviceRecord> Records => Set<DeviceRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DeviceRecord>(entity =>
            {
                entity.OwnsMany(r => r.Inputs, b =>
                {
                    b.WithOwner().HasForeignKey("DeviceRecordId");
                    b.Property<int>("Id");
                    b.HasKey("Id");
                    b.Property(i => i.Name).IsRequired();
                    b.Property(i => i.Value).IsRequired();
                });
                entity.ToTable("DeviceRecords"); 
                });

        }
    }
}
