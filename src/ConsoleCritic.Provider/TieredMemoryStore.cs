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
            // RAM ring buffer
            if (_ringBuffer.Count >= _ringBufferSize)
                _ringBuffer.Dequeue();
            _ringBuffer.Enqueue(record);

            // Akavache RAM tier
            await _ramCache.InsertObject(Guid.NewGuid().ToString(), record).ToTask();
            // Akavache disk tier
            await _diskCache.InsertObject(Guid.NewGuid().ToString(), record).ToTask();
        }

        public IEnumerable<InvocationRecord> QueryRecent(int take)
        {
            // Return most recent N from RAM ring buffer
            return new List<InvocationRecord>(_ringBuffer).GetRange(Math.Max(0, _ringBuffer.Count - take), Math.Min(take, _ringBuffer.Count));
        }

        public async Task<IEnumerable<InvocationRecord>> QueryDiskAsync(int take)
        {
            var allEnumerable = await _diskCache.GetAllObjects<InvocationRecord>().ToTask();
            var allList = allEnumerable is IEnumerable<InvocationRecord> seq ? seq.ToList() : new List<InvocationRecord>();
            // Return most recent N from disk
            return allList.Count > take ? allList.GetRange(allList.Count - take, take) : allList;
        }
    }
}
