using Microsoft.AI.Foundry.Local;
using System.Threading.Channels;

namespace ConsoleCritic.Provider.Llm;

public sealed class LocalFoundryWorker : ILlmWorker, IAsyncDisposable
{
    private readonly FoundryLocalManager _manager;
    private readonly string _alias;
    private bool _modelLoaded;

    // simple job queue to serialize calls (FoundryLocal currently single-thread affinity)
    private readonly Channel<Func<Task>> _jobs = Channel.CreateUnbounded<Func<Task>>();
    private readonly CancellationTokenSource _cts = new();

    public LocalFoundryWorker(string modelAlias)
    {
        _alias = modelAlias;
        _manager = new FoundryLocalManager();
        _ = RunLoopAsync();
    }

    public async Task StartAsync()
    {
        await _manager.StartServiceAsync();
        await EnsureModelAsync();
    }

    private async Task EnsureModelAsync()
    {
        if (_modelLoaded) return;
        await _manager.DownloadModelAsync(_alias);
        await _manager.LoadModelAsync(_alias);
        _modelLoaded = true;
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        // TODO: switch to real embedding call when Foundry supports it
        return Task.FromResult(Array.Empty<float>());
    }

    public Task<string> SummariseAsync(string text, CancellationToken ct = default)
    {
        // TODO: use Foundry text generation when available in Local SDK
        return Task.FromResult(string.Empty);
    }

    private async Task RunLoopAsync()
    {
        await foreach (var job in _jobs.Reader.ReadAllAsync(_cts.Token))
        {
            try { await job(); }
            catch { /* swallow, already reported */ }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_modelLoaded)
        {
            await _manager.UnloadModelAsync(_alias);
        }
        _manager.Dispose();
    }
}