using Microsoft.AI.Foundry.Local;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace ConsoleCritic.Provider.Llm;

public sealed class FoundryGenerationWorker : ILlmWorker
{
    private readonly string _alias;
    private readonly Task<OpenAIClient> _clientTask;

    public FoundryGenerationWorker(string alias)
    {
        _alias = alias;
        _clientTask = InitializeAsync(alias);
    }

    private static async Task<OpenAIClient> InitializeAsync(string alias)
    {
        var manager = await FoundryLocalManager.StartModelAsync(aliasOrModelId: alias);
        var key = new ApiKeyCredential(manager.ApiKey);
        return new OpenAIClient(key, new OpenAIClientOptions { Endpoint = manager.Endpoint });
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => Task.FromResult(Array.Empty<float>());

    public Task<string> SummariseAsync(string text, CancellationToken ct = default)
    {
        // TODO: integrate with OpenAI SDK once correct request types are confirmed
        return Task.FromResult(string.Empty);
    }
}