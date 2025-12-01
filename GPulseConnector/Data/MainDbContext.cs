using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPulseConnector.Abstraction.Models;

namespace GPulseConnector.Data
{
    public class MainDbContext : DbContext
    {
        public MainDbContext(DbContextOptions<MainDbContext> options)
            : base(options) { }

        public DbSet<MachineEvent> Records => Set<MachineEvent>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var converter = new ValueConverter<List<DeviceInputState>, string>(
                  v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                  v => JsonSerializer.Deserialize<List<DeviceInputState>>(v, new JsonSerializerOptions()) ?? new List<DeviceInputState>()
             );

            modelBuilder.Entity<DeviceRecord>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.DeviceId)
                    .IsRequired();

                entity.Property(x => x.Timestamp)
                    .IsRequired();

                entity.Property(x => x.Inputs)
                    .HasConversion(converter)
                    .HasColumnType("TEXT");
            });
        }
    }
}
