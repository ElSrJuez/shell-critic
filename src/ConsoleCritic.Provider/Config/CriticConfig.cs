using System;
using System.IO;
using System.Text.Json;

namespace ConsoleCritic.Provider.Config;

public sealed class CriticConfig
{
    public string? EmbeddingModelDir { get; set; }
    public string? SummarizerModelAlias { get; set; }
    public int RingBufferSize { get; set; } = 200;

    public static CriticConfig Load()
    {
        var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ConsoleCritic");
        var path = Path.Combine(baseDir, "config.json");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Missing config file: {path}");
        }

        var json = File.ReadAllText(path);
        var cfg = JsonSerializer.Deserialize<CriticConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (cfg is null)
        {
            throw new InvalidOperationException($"Invalid config JSON in {path}");
        }
        return cfg;
    }
}
