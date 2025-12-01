using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using GPulseConnector.Abstraction.Models;

namespace GPulseConnector.Data
{
    public class OfflineDbContext : DbContext
    {
        public OfflineDbContext(DbContextOptions<OfflineDbContext> options)
            : base(options) { }

        public DbSet<MachineEvent> OfflineRecords { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var converter = new ValueConverter<List<DeviceInputState>, string>(
                 v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                 v => JsonSerializer.Deserialize<List<DeviceInputState>>(v, new JsonSerializerOptions()) ?? new List<DeviceInputState>()
            );

            modelBuilder.Entity<MachineEvent>(entity =>
            {
                entity.HasKey(x => x.Id);

       
            });
        }
    }
}
