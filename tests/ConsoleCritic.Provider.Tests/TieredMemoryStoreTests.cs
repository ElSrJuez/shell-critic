using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Akavache;
using ConsoleCritic.Provider;
using ConsoleCritic.Provider.Config;
using ConsoleCritic.Provider.Llm;
using Splat;
using Xunit;

namespace ConsoleCritic.Provider.Tests
{
    public class TieredMemoryStoreTests
    {
        // Fake LLM worker for predictable outputs
        private class FakeLlmWorker : ConsoleCritic.Provider.Llm.ILlmWorker
        {
            public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
            {
                // return a predictable embedding
                return Task.FromResult(new float[] { 0.1f, 0.2f });
            }

            public Task<string> SummariseAsync(string text, CancellationToken ct = default)
            {
                // return a predictable summary
                return Task.FromResult("test summary");
            }
        }



        [Fact]
        public async Task AddAsync_StoresRecordWithEmbeddingAndSummary()
        {
            // Register Akavache caches
            Locator.CurrentMutable.RegisterConstant(BlobCache.InMemory, typeof(IBlobCache), "InMemory");
            Locator.CurrentMutable.RegisterConstant(BlobCache.LocalMachine, typeof(IBlobCache), "LocalMachine");
            // Initialize Akavache builder
            AkavacheInit.Initialize();

            // Override LlmWorkerProvider.Current to use our fake
            var lazyField = typeof(LlmWorkerProvider).GetField("_current", BindingFlags.NonPublic | BindingFlags.Static);
            lazyField.SetValue(null, new Lazy<ILlmWorker>(() => new FakeLlmWorker(), true));

            var config = new CriticConfig { RingBufferSize = 10 };
            var store = new TieredMemoryStore(config);

            var record = new InvocationRecord(DateTime.UtcNow, "Trigger", "Name", "Cmd", null, null, null, null);
            await store.AddAsync(record);

            // Verify RAM tier
            var recent = store.QueryRecent(1).First();
            Assert.Equal("test summary", recent.Summary);
            Assert.Equal(new float[] { 0.1f, 0.2f }, recent.Embedding);

            // Verify Disk tier
            var disk = (await store.QueryDiskAsync(1)).First();
            Assert.Equal("test summary", disk.Summary);
            Assert.Equal(new float[] { 0.1f, 0.2f }, disk.Embedding);
        }
    }
}