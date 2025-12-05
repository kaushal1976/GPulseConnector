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
        private readonly MachineEventFactory _eventFactory;
        private readonly MachineEventDatabaseWriter _eventDatabaseWriter;
        private readonly ReliableDatabaseWriter _reliableDatabaseWriter;
        private readonly OutputUpdateWorker _outputUpdateWorker;
        private readonly IPatternMappingCache _patternCache;    

        public RecordWriterWorker(
        ILogger<RecordWriterWorker> logger,
        Channel<IReadOnlyList<bool>> inputChannel,
        MachineEventFactory eventFactory,
        MachineEventDatabaseWriter eventDatabaseWriter,
        ReliableDatabaseWriter reliableDatabaseWriter,
        OutputUpdateWorker outputUpdateWorker,
        IPatternMappingCache patternCache)
        {
            _logger = logger;
            _inputChannel = inputChannel;
            _eventFactory = eventFactory;
            _eventDatabaseWriter = eventDatabaseWriter;
            _reliableDatabaseWriter = reliableDatabaseWriter;
            _outputUpdateWorker = outputUpdateWorker;
            _patternCache = patternCache;
        }

        private object? _lastInputsRaw;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var inputs in _inputChannel.Reader.ReadAllAsync(stoppingToken))
            {
                bool isDuplicate = false;

                if (_lastInputsRaw != null)
                    isDuplicate = AreInputsSameGeneric((IReadOnlyList<object>)_lastInputsRaw, ToObjectList(inputs));

                if (isDuplicate)
                {
                    _logger.LogInformation(
                        "Skipping processing because inputs are identical to previous: {Inputs}",
                        string.Join(", ", inputs));

                    continue;
                }

                // save last inputs as object list
                _lastInputsRaw = ToObjectList(inputs);

                PatternMapping? matched = null;

                try
                {
                    matched = await _patternCache.MatchPatternAsync(inputs);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Pattern matching failed for inputs {Inputs}", string.Join(", ", inputs));
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
                    _logger.LogWarning(ex, "Event could not be written to the DB");
                }

                try
                {
                    if (matched != null)
                    {
                        _logger.LogInformation(
                            "Matched pattern with ID {Id} for inputs {Inputs}",
                            matched.Id, string.Join(", ", inputs));

                        await _outputUpdateWorker.UpdateOutputAsync(matched, stoppingToken);
                    }
                    else
                    {
                        _logger.LogInformation("No pattern matched for inputs {Inputs}", string.Join(", ", inputs));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not update the outputs");
                }
            }
        }

        private static bool AreInputsSameGeneric(IReadOnlyList<object> a, IReadOnlyList<object> b)
        {
            if (a.Count != b.Count)
                return false;

            for (int i = 0; i < a.Count; i++)
            {
                if (!Equals(a[i], b[i]))
                    return false;
            }

            return true;
        }

        private static IReadOnlyList<object> ToObjectList<T>(IReadOnlyList<T> list)
        {
            var result = new object[list.Count];
            for (int i = 0; i < list.Count; i++)
                result[i] = list[i]!;
            return result;
        }

    }
}

    
