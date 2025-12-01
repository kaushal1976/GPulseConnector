using Microsoft.EntityFrameworkCore;
using GPulseConnector.Data;
using System.ComponentModel.DataAnnotations.Schema;
using GPulseConnector.Abstraction.Models;

namespace GPulseConnector.Services
{
    public class MachineEventDatabaseWriter
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly RetryQueueRepository _retryRepo;
        private readonly DatabaseLogger _dblogger;
        private readonly ILogger<MachineEventDatabaseWriter> _logger;

        public MachineEventDatabaseWriter(IDbContextFactory<AppDbContext> factory, RetryQueueRepository retryRepo, DatabaseLogger dblogger, ILogger<MachineEventDatabaseWriter> logger)
        {
            _factory = factory;
            _retryRepo = retryRepo;
            _dblogger = dblogger;
            _logger = logger;
        }


        
        public async Task<bool> TryWriteMachineEventAsync(MachineEvent record)
        {
            try
            {
                await WriteAsync(record);
                return true;
            }
            catch (Exception)
            {

                _logger.LogWarning("Could not write to the main database, enqueueing to retry queue");
                MachineEvent copy = record.CloneObj();

                await _retryRepo.EnqueueAsync(copy);
                return false;

            }

        }
        
        public async Task<bool> WriteAsync(MachineEvent record, CancellationToken cancellationToken = default)
        {
            using var db = await _factory.CreateDbContextAsync();
            db.ChangeTracker.Clear();

            if (record.Id == 0)
            {
                db.MachineEvents.Add(record);
            }
            else
            {
                var existing = db.MachineEvents.FindAsync(record.Id);
    
                    db.Entry(existing).CurrentValues.SetValues(record);
            }
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }


    }
}
