using GPulseConnector.Abstraction.Interfaces;
using GPulseConnector.Abstraction.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPulseConnector.Services
{
    public class FailOverRecordWriter : IRecordWriter
    {
        private readonly SqlMssqlRecordWriter _sqlWriter;
        private readonly SqliteFallbackWriter _sqliteWriter;
        public FailOverRecordWriter( SqlMssqlRecordWriter sqlWriter, SqliteFallbackWriter sqliteWriter)
        {
            _sqlWriter = sqlWriter;
            _sqliteWriter = sqliteWriter;
        }

        public Task<bool> TryWriteAsync(DeviceRecord record)
        {
            throw new NotImplementedException();
        }

        async Task<bool> IRecordWriter.WriteAsync(DeviceRecord record, CancellationToken cancellationToken)
        {
            try
            {
                 await _sqlWriter.WriteAsync(record, cancellationToken);
                return true;

            }
            catch
            {
                 await _sqliteWriter.WriteAsync(record, cancellationToken);
                return false;

            }
        }
    }

}
