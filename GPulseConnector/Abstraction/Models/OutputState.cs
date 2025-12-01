using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPulseConnector.Abstraction.Models
{
    public record OutputState(IReadOnlyList<bool> Outputs, DateTime Timestamp);
}
