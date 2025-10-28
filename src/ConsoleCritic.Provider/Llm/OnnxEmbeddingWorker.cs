using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using System.Linq;

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

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        // encode to token IDs via Tokenizers v2.0 preview
        var ids = _tokenizer.EncodeToIds(text).Select(i => (long)i).ToArray();
        var len = ids.Length;
        if (len == 0) return Array.Empty<float>();

        var inputIdsTensor = new DenseTensor<long>(ids, new[] {1, len});
        var masks = Enumerable.Repeat(1L, len).ToArray();
        var attMaskTensor = new DenseTensor<long>(masks, new[] {1, len});

        // Some BERT-style ONNX models require token_type_ids (segment IDs). Provide zeros.
        var tokenTypes = new long[len]; // defaults to 0s
        var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypes, new[] {1, len});

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attMaskTensor),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
        };

        using var result = _session.Run(inputs);
        var output = result.First().AsTensor<float>();
        var dim2 = output.Dimensions[2];
        var pooled = new float[dim2];
        for (int i = 0; i < len; i++)
        {
            for (int j = 0; j < dim2; j++)
                pooled[j] += output[0, i, j];
        }
        for (int j = 0; j < dim2; j++)
        {
            pooled[j] /= len;
        }
        // L2 normalize
        var norm = MathF.Sqrt(pooled.Sum(v => v * v));
        if (norm > 0)
        {
            for (int j = 0; j < dim2; j++) pooled[j] /= norm;
        }
        await Task.CompletedTask;
        return pooled;
    }
}