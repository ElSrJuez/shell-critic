namespace ConsoleCritic.Provider.Llm;

public interface ILlmWorker
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    Task<string> SummariseAsync(string text, CancellationToken ct = default);
}