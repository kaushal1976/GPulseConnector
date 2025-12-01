using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPulseConnector.Options
{
    public class InputDeviceOptions
    {
        public string DeviceName { get; set; } = "";
        public string Location { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public int NumberOfInputs { get; set; } = 16;
        public string[] InputNames { get; set; } = Array.Empty<string>();

    }
}
