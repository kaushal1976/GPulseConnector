using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPulseConnector.Abstraction.Models;

namespace GPulseConnector.Abstraction.Interfaces
{

    public interface IPatternMappingCache
    {
        Task<PatternMapping?> MatchPatternAsync(IReadOnlyList<bool> inputFlags);
        Task LoadAsync();
        Task ReloadAsync();
    }

}
