using GPulseConnector.Abstraction.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPulseConnector.Services;
    public static class BitPatternHelper
{
    public static PatternMapping? MatchPattern(IReadOnlyList<bool> inputFlags, IReadOnlyList<PatternMapping> patterns)
    {
        if (inputFlags == null || inputFlags.Count < 9)
            throw new ArgumentException("Input flags must have at least 9 items.", nameof(inputFlags));

        if (patterns == null || patterns.Count == 0)
            return null;

        // Precompute pattern masks
       var patternMasks = patterns.Select(p =>
        {
            int value = 0;
            int mask = 0;

            bool?[] bits =
            {
                p.ID0, p.ID1, p.ID2,
                p.ID3, p.ID4, p.ID5,
                p.ID6, p.ID7, p.ID8
            };

            for (int i = 0; i < bits.Length; i++)
            {
                bool? bit = bits[i];   // 👈 capture locally

                if (!bit.HasValue)
                    continue;

                mask |= 1 << i;

                if (bit.Value)
                    value |= 1 << i;
            }

            return (pattern: p, mask, value);
        }).ToList();


        // Convert inputFlags to a sliding 9-bit integer window
        for (int start = 0; start <= inputFlags.Count - 9; start++)
        {
            int window = 0;
            for (int i = 0; i < 9; i++)
            {
                if (inputFlags[start + i])
                    window |= 1 << i;
            }

            foreach (var (pattern, mask, value) in patternMasks)
            {
                if ((window & mask) == value)  // fast match using bitwise AND
                    return pattern;
            }
        }

        return null;
    }
}

