using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPulseConnector.Abstraction.Models
{
    public record InputState(IReadOnlyList<bool> Inputs, DateTime Timestamp);
}
