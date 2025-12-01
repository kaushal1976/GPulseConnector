using GPulseConnector.Abstraction.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPulseConnector.Abstraction.Interfaces
{
    public interface IRecordWriter
    {
        Task<bool> WriteAsync(DeviceRecord record, CancellationToken cancellationToken = default);
    }
}
