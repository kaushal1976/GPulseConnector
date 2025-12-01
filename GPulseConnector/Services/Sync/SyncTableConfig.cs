using Microsoft.EntityFrameworkCore;
using GPulseConnector.Data;

namespace GPulseConnector.Services.Sync
{
    public class SyncTableConfig<T> : ISyncTableConfig
        where T : class, ISyncEntity, new()
    {
        public required Func<AppDbContext, IQueryable<T>> MsQuery { get; init; }
        public required Func<SQLiteFallbackDbContext, IQueryable<T>> SqliteQuery { get; init; }
        public required Func<SQLiteFallbackDbContext, DbSet<T>> SqliteSet { get; init; }
        public required Func<T, object> Key { get; init; }
        public required Func<T, string> ComputeHash { get; init; }

        public async Task SyncAsync(
            IDbContextFactory<AppDbContext> msFactory,
            IDbContextFactory<SQLiteFallbackDbContext> sqliteFactory,
            CancellationToken ct)
        {
            await using var ms = await msFactory.CreateDbContextAsync(ct);
            await using var sqlite = await sqliteFactory.CreateDbContextAsync(ct);

            var msItems = await MsQuery(ms).AsNoTracking().ToListAsync(ct);
            var sqliteItems = await SqliteQuery(sqlite).AsNoTracking().ToListAsync(ct);

            var msMap = msItems.ToDictionary(Key);
            var sqliteMap = sqliteItems.ToDictionary(Key);

            var set = SqliteSet(sqlite);

            // INSERT + UPDATE
            foreach (var msItem in msItems)
            {
                var key = Key(msItem);

                if (!sqliteMap.TryGetValue(key, out var local))
                {
                    await set.AddAsync(msItem, ct);
                }
                else
                {
                    if (ComputeHash(msItem) != ComputeHash(local))
                    {
                        sqlite.Entry(msItem).State = EntityState.Modified;
                    }
                }
            }

            // DELETE removed rows
            var deleteList = sqliteItems
                .Where(s => !msMap.ContainsKey(Key(s)))
                .ToList();

            if (deleteList.Any())
            {
                set.RemoveRange(deleteList);
            }

            await sqlite.SaveChangesAsync(ct);
        }
    }
}
