using GPulseConnector.Abstraction.Models;
using GPulseConnector.Options;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPulseConnector.Abstraction.Factories
{
    public class MachineEventFactory
    {
        private readonly MachineOptions _options;
        private readonly string _deviceName;
        private readonly string _location;
        private readonly IReadOnlyList<string> _inputNames;

        public MachineEventFactory(IOptions<MachineOptions> options)
        {
            _options = options.Value;
            _deviceName = _options.DeviceName;
            _location = _options.Location;
            _inputNames = _options.InputNames ?? Array.Empty<string>();
        }

        public MachineEvent CreateFromInputs(IReadOnlyList<bool> inputs)
        {
            return CreateFromInputs(inputs, inputNames: null);

        }

        public MachineEvent CreateFromInputs(IReadOnlyList<bool> inputs, IReadOnlyList<string>? inputNames = null)
        {
            var record = new MachineEvent
            {
                
                MachineId = _options.MachineId,
                AdditionalInformation = string.Empty,
                StatusId = null,
                TimeStamp = DateTime.UtcNow,
                Epoch = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                SpindleRunning = inputs.ElementAtOrDefault(0),
                FeedHold = inputs.ElementAtOrDefault(1),
                DryRun = inputs.ElementAtOrDefault(2),
                M00M01 = inputs.ElementAtOrDefault(3),
                InAlarm = inputs.ElementAtOrDefault(4),
                InClycle = inputs.ElementAtOrDefault(5),
                FeedRateLessThan100 = inputs.ElementAtOrDefault(6),
                FeedRateIs100 = inputs.ElementAtOrDefault(7),
                FeedRateGreaterThan100 = inputs.ElementAtOrDefault(8),
            };       

            return record;
        }

    }
}
