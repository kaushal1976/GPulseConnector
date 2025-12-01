using GPulseConnector.Abstraction.Interfaces;
using GPulseConnector.Abstraction.Models;
using GPulseConnector.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GPulseConnector.Services
{
    public class SqliteFallbackWriter : IRecordWriter
    {
        private readonly IDbContextFactory<SQLiteFallbackDbContext> _factory;

        public SqliteFallbackWriter(IDbContextFactory<SQLiteFallbackDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<bool> TryWriteAsync(DeviceRecord record)
        {
            using var db = await _factory.CreateDbContextAsync();
            await db.DeviceRecords.AddAsync(record);
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> WriteAsync(DeviceRecord record, CancellationToken cancellationToken = default)
        {
            using var db = await _factory.CreateDbContextAsync(cancellationToken);
            await db.DeviceRecords.AddAsync(record, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
