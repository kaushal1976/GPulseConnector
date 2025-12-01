using GPulseConnector.Abstraction.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace GPulseConnector.Services
{
    public class BufferedRecordWriter
    {

        private readonly Channel<DeviceRecord> _channel;

        public BufferedRecordWriter(Channel<DeviceRecord> channel )
        {
            _channel = channel;
        }

        public bool TryQueue(DeviceRecord record) =>
            _channel.Writer.TryWrite(record);

        public ChannelReader<DeviceRecord> Reader => _channel.Reader;

    }
}
