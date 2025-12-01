using GPulseConnector.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPulseConnector.Abstraction.Interfaces;
using GPulseConnector.Abstraction.Models;

namespace GPulseConnector.Services
{
    public class PatternMappingCache : IPatternMappingCache
    {

        private readonly IDbContextFactory<SQLiteFallbackDbContext> _factory;
        private List<PatternMapping> _cache = new();
        private bool _isLoaded = false;

        public PatternMappingCache(IServiceProvider provider, IDbContextFactory<SQLiteFallbackDbContext> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));

        }

        public async Task<PatternMapping?> MatchPatternAsync(IReadOnlyList<bool> inputFlags)
        {
            if (!_isLoaded)
                await LoadAsync();
                return BitPatternHelper.MatchPattern(inputFlags, _cache);
        }

        public async Task LoadAsync()
        {
            await using var db = await _factory.CreateDbContextAsync();
            _cache = await db.PatternMappings.AsNoTracking().ToListAsync();
            _isLoaded = true;

        }

        public Task ReloadAsync() => LoadAsync();
    }
}
