using Microsoft.EntityFrameworkCore;
using GPulseConnector.Data;
using GPulseConnector.Abstraction.Models;
using System.Reflection;
using Microsoft.Data.SqlClient;

namespace GPulseConnector.Services
{
    public class ReliableDatabaseWriter
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly RetryQueueRepository _retryRepo;
        private readonly ILogger<ReliableDatabaseWriter> _logger;

        public ReliableDatabaseWriter(
            IDbContextFactory<AppDbContext> factory,
            RetryQueueRepository retryRepo,
            ILogger<ReliableDatabaseWriter> logger)
        {
            _factory = factory;
            _retryRepo = retryRepo;
            _logger = logger;
        }

        public static T CloneObject<T>(T source) where T : class
        {
            if (source == null)
                return null!;

            var type = typeof(T);
            var target = (T)Activator.CreateInstance(type, true)!;

            foreach (var member in type.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                switch (member)
                {
                    case PropertyInfo prop when prop.CanRead && prop.CanWrite:
                        prop.SetValue(target, prop.GetValue(source));
                        break;

                    case FieldInfo field:
                        field.SetValue(target, field.GetValue(source));
                        break;
                }
            }

            return target;
        }

        public async Task<bool> WriteSafeAsync<T>(T item) where T : class
        {
            var copy = CloneObject(item);

            try
            {
                await WriteAsync(item);
                return true;
            }
            catch (SqlException ex) when (ex.Number == 10061)
            {
                // SQL server offline → silently queue retry
                await _retryRepo.EnqueueAsync(copy);
                return false;
            }
            catch (SqlException)
            {
                // Other transient SQL errors → silently queue retry
                await _retryRepo.EnqueueAsync(copy);
                return false;
            }
            catch (ObjectDisposedException)
            {
                // Context disposed/network issues → silently queue retry
                await _retryRepo.EnqueueAsync(copy);
                return false;
            }
            catch (Exception)
            {
                // Only truly unexpected errors are logged
                _logger.LogError("ReliableDatabaseWriter encountered a non-transient failure");
                await _retryRepo.EnqueueAsync(copy);
                return false;
            }
        }



        public async Task WriteAsync<T>(T item) where T : class
        {
            await using var db = _factory.CreateDbContext();
            var strategy = db.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();

                var entry = db.Entry(item);
                var pk = entry.Metadata.FindPrimaryKey()?.Properties.FirstOrDefault();

                if (pk == null)
                {
                    _logger.LogWarning("No primary key defined for type {TypeName}", typeof(T).Name);
                }

                object? id = pk?.PropertyInfo != null
                    ? pk.PropertyInfo.GetValue(item)
                    : pk != null ? entry.Property(pk.Name).CurrentValue : null;

                bool isNew = id == null || (id is int i && i == 0);

                var set = db.Set<T>();

                if (isNew)
                {
                    set.Add(item);
                }
                else
                {
                    var existing = await set.FindAsync(id);
                    if (existing == null)
                    {
                        set.Add(item);
                    }
                    else
                    {
                        db.Entry(existing).CurrentValues.SetValues(item);
                    }
                }

                await db.SaveChangesAsync();
            });
        }
    }
}
