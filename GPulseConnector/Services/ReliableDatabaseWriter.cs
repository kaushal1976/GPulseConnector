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
        private readonly DatabaseLogger _dblogger;
        private readonly ILogger<ReliableDatabaseWriter> _logger;

        public ReliableDatabaseWriter(
            IDbContextFactory<AppDbContext> factory,
            RetryQueueRepository retryRepo,
            DatabaseLogger dblogger,
            ILogger<ReliableDatabaseWriter> logger)
        {
            _factory = factory;
            _retryRepo = retryRepo;
            _dblogger = dblogger;
            _logger = logger;
        }

        public static T CloneObject<T>(T source) where T : class
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var target = (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;
            var type = typeof(T);

            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance))
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
            var copy = CloneObject(item); // Clone once to reuse
            try
            {
                await WriteAsync(item);
                return true;
            }
            catch (SqlException)
            {
                // transient DB failure → enqueue silently
                await _retryRepo.EnqueueAsync(copy);
                return false;
            }
            catch (ObjectDisposedException)
            {
                // transient network/socket issue → enqueue silently
                await _retryRepo.EnqueueAsync(copy);
                return false;
            }
            catch (Exception ex)
            {
                // only log unexpected errors
                _logger.LogError(ex, "ReliableDatabaseWriter encountered a non-transient failure");
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
                var idProperty = entry.Metadata.FindPrimaryKey()?.Properties.FirstOrDefault();

                if (idProperty == null)
                    throw new InvalidOperationException(
                        $"Type {typeof(T).Name} does not have a primary key. Cannot write."
                    );

                object? id = idProperty.PropertyInfo != null
                    ? idProperty.PropertyInfo.GetValue(item)
                    : entry.Property(idProperty.Name).CurrentValue;

                bool isNew = id == null || (id is int i && i == 0);

                var dbSet = db.Set<T>();

                if (isNew)
                {
                    dbSet.Add(item);
                }
                else
                {
                    var existing = await dbSet.FindAsync(id);
                    if (existing == null)
                        dbSet.Add(item);
                    else
                        db.Entry(existing).CurrentValues.SetValues(item);
                }

                await db.SaveChangesAsync();
            });
        }
    }
}
