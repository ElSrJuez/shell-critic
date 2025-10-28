using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace ConsoleCritic.Provider.Llm;

public sealed class OnnxEmbeddingWorker : ILlmWorker, IDisposable
{
    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;

    public OnnxEmbeddingWorker(string modelDir)
    {
        _session = new InferenceSession(Path.Combine(modelDir, "onnx", "model.onnx"));
        _tokenizer = BertTokenizer.Create(Path.Combine(modelDir, "vocab.txt"));
    }

    public ValueTask DisposeAsync() => default;
    public void Dispose() => _session.Dispose();

    public Task<string> SummariseAsync(string text, CancellationToken ct = default) => Task.FromResult(text);

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        // TODO: Implement real embedding logic using ONNX Runtime & tokenizer API
        return Task.FromResult(Array.Empty<float>());
    }
}