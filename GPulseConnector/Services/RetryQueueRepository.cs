using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using GPulseConnector.Data;
using GPulseConnector.Abstraction.Models;

namespace GPulseConnector.Services
{
    public class RetryQueueRepository
    {
        private readonly IDbContextFactory<RetryQueueDbContext> _factory;

        public RetryQueueRepository(IDbContextFactory<RetryQueueDbContext> factory)
        {
            _factory = factory;
        }

        private static string ComputeHash(string payloadJson)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(payloadJson);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }

        public async Task EnqueueAsync<T>(T payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var hash = ComputeHash(json);

            await using var db = _factory.CreateDbContext();

            // dedupe by payload hash to avoid exact duplicates
            var exists = await db.RetryQueue.AsNoTracking().AnyAsync(x => x.PayloadHash == hash);
            if (exists) return;

            var item = new RetryQueueItem
            {
                PayloadType = typeof(T).AssemblyQualifiedName!,
                PayloadJson = json,
                PayloadHash = hash,
                AttemptCount = 0,
                CreatedOnUtc = DateTime.UtcNow
            };

            db.RetryQueue.Add(item);
            await db.SaveChangesAsync();
        }

        public async Task<List<RetryQueueItem>> LoadAllAsync()
        {
            await using var db = _factory.CreateDbContext();
            return await db.RetryQueue.OrderBy(x => x.Id).ToListAsync();
        }

        public async Task DeleteAsync(RetryQueueItem item)
        {
            await using var db = _factory.CreateDbContext();
            var tracked = await db.RetryQueue.FindAsync(item.Id);
            if (tracked != null)
            {
                db.RetryQueue.Remove(tracked);
                await db.SaveChangesAsync();
            }
        }

        public async Task DeleteAsync(long id)
        {
            await using var db = _factory.CreateDbContext();
            var tracked = await db.RetryQueue.FindAsync(id);
            if (tracked != null)
            {
                db.RetryQueue.Remove(tracked);
                await db.SaveChangesAsync();
            }
        }

        public async Task RecordFailureAsync(RetryQueueItem item, string? lastError = null)
        {
            await using var db = _factory.CreateDbContext();
            var tracked = await db.RetryQueue.FindAsync(item.Id);
            if (tracked == null)
            {
                // maybe already deleted
                return;
            }

            tracked.AttemptCount++;
            if (!string.IsNullOrWhiteSpace(lastError))
                tracked.LastError = lastError;

            db.RetryQueue.Update(tracked);
            await db.SaveChangesAsync();
        }

        public async Task RecordFailureAsync(long id, string? lastError = null)
        {
            await using var db = _factory.CreateDbContext();
            var tracked = await db.RetryQueue.FindAsync(id);
            if (tracked == null) return;

            tracked.AttemptCount++;
            if (!string.IsNullOrWhiteSpace(lastError))
                tracked.LastError = lastError;

            db.RetryQueue.Update(tracked);
            await db.SaveChangesAsync();
        }

        public static object? Deserialize(RetryQueueItem item)
        {
            try
            {
                var type = Type.GetType(item.PayloadType);
                if (type == null) return null;
                return JsonSerializer.Deserialize(item.PayloadJson, type, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return null;
            }
        }
    }
}
