using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akavache;
using System.Reactive.Threading.Tasks;
using ConsoleCritic.Provider.Config;
using Splat;

namespace ConsoleCritic.Provider
{
    public class TieredMemoryStore
    {
        private readonly int _ringBufferSize;
        private readonly IBlobCache _ramCache;
        private readonly IBlobCache _diskCache;
        private readonly Queue<InvocationRecord> _ringBuffer;

        public TieredMemoryStore(CriticConfig config)
        {
            _ringBufferSize = config.RingBufferSize;
            _ramCache = Locator.Current.GetService<Akavache.IBlobCache>("InMemory") ?? throw new InvalidOperationException("Akavache InMemory cache not initialized.");
            _diskCache = Locator.Current.GetService<Akavache.IBlobCache>("LocalMachine") ?? throw new InvalidOperationException("Akavache LocalMachine cache not initialized.");
            _ringBuffer = new Queue<InvocationRecord>(_ringBufferSize);
        }

        public async Task AddAsync(InvocationRecord record)
        {
            // Generate embedding and summary before storing
            var llm = ConsoleCritic.Provider.Llm.LlmWorkerProvider.Current;
            float[]? embedding = null;
            string? summary = null;
            try {
                embedding = await llm.EmbedAsync(record.CommandLine ?? string.Empty);
            } catch (Exception ex) {
                // Surface embedding errors
                throw new InvalidOperationException("Failed to generate embedding.", ex);
            }
            try {
                summary = await llm.SummariseAsync(record.CommandLine ?? string.Empty);
            } catch (Exception ex) {
                // Surface summary errors
                throw new InvalidOperationException("Failed to generate summary.", ex);
            }

            var enrichedRecord = record with { Embedding = embedding, Summary = summary };

            // RAM ring buffer
            if (_ringBuffer.Count >= _ringBufferSize)
                _ringBuffer.Dequeue();
            _ringBuffer.Enqueue(enrichedRecord);

            // Akavache RAM tier
            try {
                await _ramCache.InsertObject(Guid.NewGuid().ToString(), enrichedRecord).ToTask();
            } catch (Exception ex) {
                throw new InvalidOperationException("Failed to persist record to RAM tier.", ex);
            }
            // Akavache disk tier
            try {
                await _diskCache.InsertObject(Guid.NewGuid().ToString(), enrichedRecord).ToTask();
            } catch (Exception ex) {
                throw new InvalidOperationException("Failed to persist record to disk tier.", ex);
            }
        }

        public IEnumerable<InvocationRecord> QueryRecent(int take)
        {
            // Return most recent N from RAM ring buffer
            return new List<InvocationRecord>(_ringBuffer).GetRange(Math.Max(0, _ringBuffer.Count - take), Math.Min(take, _ringBuffer.Count));
        }

        public async Task<IEnumerable<InvocationRecord>> QueryDiskAsync(int take)
        {
            try {
                var allEnumerable = await _diskCache.GetAllObjects<InvocationRecord>().ToTask();
                var allList = allEnumerable is IEnumerable<InvocationRecord> seq ? seq.ToList() : new List<InvocationRecord>();
                // Return most recent N from disk
                return allList.Count > take ? allList.GetRange(allList.Count - take, take) : allList;
            } catch (Exception ex) {
                throw new InvalidOperationException("Failed to query disk tier.", ex);
            }
        }
    }
}
