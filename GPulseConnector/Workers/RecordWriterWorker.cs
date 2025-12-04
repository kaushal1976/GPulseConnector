using GPulseConnector.Abstraction.Factories;
using GPulseConnector.Abstraction.Interfaces;
using GPulseConnector.Abstraction.Models;
using GPulseConnector.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace GPulseConnector.Workers
{
    public class RecordWriterWorker : BackgroundService
    {
        private readonly ILogger<RecordWriterWorker> _logger;
        private readonly Channel<IReadOnlyList<bool>> _inputChannel;
        private readonly BufferedRecordWriter _bufferedWriter;
        private readonly IRecordWriter _primaryWriter;
        private readonly IRecordWriter _fallbackWriter;
        private readonly DeviceRecordFactory _factory;
        private readonly MachineEventFactory _eventFactory;
        private readonly MachineEventDatabaseWriter _eventDatabaseWriter;
        private readonly ReliableDatabaseWriter _reliableDatabaseWriter;
        private readonly OutputUpdateWorker _outputUpdateWorker;

        private readonly IPatternMappingCache _patternCache;    

        public RecordWriterWorker(
        ILogger<RecordWriterWorker> logger,
        Channel<IReadOnlyList<bool>> inputChannel,
        BufferedRecordWriter bufferedWriter,
        IRecordWriter primaryWriter,
        IRecordWriter fallbackWriter,
        MachineEventFactory eventFactory,
        MachineEventDatabaseWriter eventDatabaseWriter,
        ReliableDatabaseWriter reliableDatabaseWriter,
        OutputUpdateWorker outputUpdateWorker,
        IPatternMappingCache patternCache,
        DeviceRecordFactory factory)
        {
            _logger = logger;
            _inputChannel = inputChannel;
            _bufferedWriter = bufferedWriter;
            _primaryWriter = primaryWriter;
            _fallbackWriter = fallbackWriter;
            _factory = factory;
            _eventFactory = eventFactory;
            _eventDatabaseWriter = eventDatabaseWriter;
            _reliableDatabaseWriter = reliableDatabaseWriter;
            _outputUpdateWorker = outputUpdateWorker;
            _patternCache = patternCache;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var inputs in _inputChannel.Reader.ReadAllAsync(stoppingToken))
            {
                var matched = await _patternCache.MatchPatternAsync(inputs);
                try
                {
                    DeviceRecord rec = _factory.CreateFromInputs(inputs);
                    bool ok = await _primaryWriter.WriteAsync(rec, stoppingToken);
                    if (!ok)
                    {
                        await _fallbackWriter.WriteAsync(rec, stoppingToken);
                        _logger.LogWarning("Primary DB write failed, fallback used for record at {T}", rec.Timestamp);
                    }
                    else
                    {
                        _logger.LogInformation("Record written to primary DB at {T}", rec.Timestamp);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error converting input snapshot to record");
                }

                try
                {
                    MachineEvent record = _eventFactory.CreateFromInputs(inputs);
                    record.StatusId = matched?.Id;
                    bool ok = await _eventDatabaseWriter.TryWriteMachineEventAsync(record);
                    _logger.LogInformation("Event Record written to primary DB at {T}", record.TimeStamp);
                    

                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Event written to the DB");
                }

                try
                {
                    if (matched != null)
                    {
                        _logger.LogInformation("Matched pattern with ID {Id} for inputs {Inputs}", matched.Id, string.Join(", ", inputs));
                        await _outputUpdateWorker.UpdateOutputAsync(matched, stoppingToken);
                    }
                    else
                    {
                        _logger.LogInformation("No pattern matched for inputs {Inputs}", string.Join(", ", inputs));
                    }
                    
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not update the outputs" );
                }

            }
        }

    }
}

    
