using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPulseConnector.Abstraction.Models
{
    public class DeviceIORecord
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string DeviceId { get; set; } ="";
        public string DeviceName { get; set; } = "";
        public string Location { get; set; } = "";
    }
}
