using GPulseConnector.Abstraction.Models;
using GPulseConnector.Options;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPulseConnector.Abstraction.Factories
{
    public class DeviceRecordFactory
    {
        private readonly DeviceOptions _options;
        private readonly string _deviceName;
        private readonly string _location;
        private readonly IReadOnlyList<string> _inputNames;

        public DeviceRecordFactory(IOptions<DeviceOptions> options)
        {
            _options = options.Value;
            _deviceName = _options.DeviceName;
            _location = _options.Location;
            _inputNames = _options.InputDevices.InputNames ?? Array.Empty<string>();
        }

        public DeviceRecord CreateFromInputs(IReadOnlyList<bool> inputs)
        {
            return CreateFromInputs(inputs, inputNames: null);
           
        }

        public DeviceRecord CreateFromInputs(IReadOnlyList<bool> inputs, IReadOnlyList<string>? inputNames = null)
        {
            var record = new DeviceRecord
            {
                Timestamp = DateTime.UtcNow,
                DeviceName = _deviceName,
                Location = _location,

            };

            for (int i = 0; i < inputs.Count; i++)
            {
                string name = i < _inputNames.Count
                    ? _inputNames[i]
                    : $"Input{i}";

                record.Inputs.Add(new DeviceInputState
                {
                    Name = name,
                    Value = inputs[i]
                });
            }

            return record;
        }

    }
}
