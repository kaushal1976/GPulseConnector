using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPulseConnector.Abstraction.Models
{
    public class DeviceInputState
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool Value { get; set; }
        public int DeviceRecordId { get; set; }
    }
}
