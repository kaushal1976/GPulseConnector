using GPulseConnector.Abstraction.Interfaces;
using GPulseConnector.Abstraction.Models;
using GPulseConnector.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPulseConnector.Services
{
    public class SqlMssqlRecordWriter : IRecordWriter
    {
        private readonly IDbContextFactory<DeviceRecordDbContext> _factory;

        public SqlMssqlRecordWriter(IDbContextFactory<DeviceRecordDbContext> factory)
        {
            _factory = factory;
        }

        public Task<bool> TryWriteAsync(DeviceRecord record)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> WriteAsync(DeviceRecord record, CancellationToken cancellationToken = default)
        {
            using var db = await _factory.CreateDbContextAsync(cancellationToken);
            await db.Records.AddAsync(record, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
