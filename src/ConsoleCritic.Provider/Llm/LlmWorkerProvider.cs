using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleCritic.Provider.Llm;

/// <summary>
/// Provides a singleton <see cref="ILlmWorker"/> instance that combines a Foundry-based
/// generation worker (for summaries) and an ONNX embedding worker (for semantic search).
/// Falls back to a no-op implementation if models are missing.
/// </summary>
public static class LlmWorkerProvider
{
    private static readonly Lazy<ILlmWorker> _current = new(Create, true);

    /// <summary>
    /// Gets the global LLM worker instance.
    /// </summary>
    public static ILlmWorker Current => _current.Value;

    private static ILlmWorker Create()
    {
        try
        {
            // Model aliases / paths could come from config; hard-coded for prototype.
            var summariser = new FoundryGenerationWorker("phi3-mini-4k-instruct-cuda-gpu");

            var embModelDir = Environment.GetEnvironmentVariable("CONSOLE_CRITIC_EMB_MODEL_DIR")
                               ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                               "ConsoleCritic", "models", "all-MiniLM-L12-v2");
            ILlmWorker embedder;
            try { embedder = new OnnxEmbeddingWorker(embModelDir); }
            catch { embedder = new NoOpWorker(); }

            return new CompositeWorker(summariser, embedder);
        }
        catch
        {
            return new NoOpWorker();
        }
    }

    private sealed class CompositeWorker : ILlmWorker
    {
        private readonly ILlmWorker _summariser;
        private readonly ILlmWorker _embedder;

        public CompositeWorker(ILlmWorker summariser, ILlmWorker embedder)
        {
            _summariser = summariser;
            _embedder = embedder;
        }

        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) =>
            _embedder.EmbedAsync(text, ct);

        public Task<string> SummariseAsync(string text, CancellationToken ct = default) =>
            _summariser.SummariseAsync(text, ct);
    }

    private sealed class NoOpWorker : ILlmWorker
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) =>
            Task.FromResult(Array.Empty<float>());

        public Task<string> SummariseAsync(string text, CancellationToken ct = default) =>
            Task.FromResult(string.Empty);
    }
}