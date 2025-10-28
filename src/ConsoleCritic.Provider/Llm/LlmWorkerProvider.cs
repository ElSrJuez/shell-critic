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
        // Fail fast: load configuration and construct workers; exceptions will surface.
        var cfg = ConsoleCritic.Provider.Config.CriticConfig.Load();

        var summariserModel = cfg.SummarizerModelAlias ?? "phi3-mini-4k-instruct-cuda-gpu";
        var summariser = new FoundryGenerationWorker(summariserModel);
        ConsoleCritic.Provider.Logging.CriticLog.Event($"FoundryGenerationWorker initialized with model '{summariserModel}'");

        var embModelDir = cfg.EmbeddingModelDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ConsoleCritic", "models", "all-MiniLM-L12-v2");
        var embedder = new OnnxEmbeddingWorker(embModelDir);
        ConsoleCritic.Provider.Logging.CriticLog.Event($"OnnxEmbeddingWorker initialized using model directory '{embModelDir}'");

        var worker = new CompositeWorker(summariser, embedder);
        ConsoleCritic.Provider.Logging.CriticLog.Event($"CompositeWorker created (summariser={summariser.GetType().Name}, embedder={embedder.GetType().Name})");
        return worker;
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
}