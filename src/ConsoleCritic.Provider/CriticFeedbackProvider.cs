using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Subsystem;
using System.Management.Automation.Subsystem.Feedback;
using System.Threading;

namespace ConsoleCritic.Provider;

// Minimal listener: registers for all triggers and silently logs to a file.
public sealed class CriticFeedbackProvider : IFeedbackProvider
{
    private readonly Guid _guid;
    public Guid Id => _guid;
    public string Name => "ConsoleCritic";
    public string Description => "Silent logger for command invocations";
    Dictionary<string, string>? ISubsystem.FunctionsToDefine => null;

    // Listen to all triggers; we only log and return null (no UX noise)
    public FeedbackTrigger Trigger => FeedbackTrigger.All;

    private readonly string _logPath;

    public CriticFeedbackProvider(string guid)
    {
        _guid = new Guid(guid);
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(baseDir, "ConsoleCritic", "logs");
        Directory.CreateDirectory(dir);
        _logPath = Path.Combine(dir, $"critic-{DateTime.UtcNow:yyyyMMdd}.log");
    }

    public FeedbackItem? GetFeedback(FeedbackContext context, CancellationToken token)
    {
        try
        {
            var cmdLine = context.CommandLine ?? string.Empty;
            float[]? emb = null;
            try
            {
                using var cts = new CancellationTokenSource(300);
                emb = ConsoleCritic.Provider.Llm.LlmWorkerProvider.Current.EmbedAsync(cmdLine, cts.Token).Result;
            }
            catch (Exception ex)
            {
                try
                {
                    var errFile = Path.Combine(Path.GetDirectoryName(_logPath)!, "critic-errors.log");
                    File.AppendAllText(errFile, $"{DateTime.UtcNow:o}\tEmbedError\t{ex.Message}{Environment.NewLine}");
                }
                catch { /* ignore secondary failures */ }
            }

            var entry = Serialize(context, emb);
            // Restore: write feedback events directly to critic-YYYYMMDD.log (TSV)
            File.AppendAllText(_logPath, entry + Environment.NewLine);
        }
        catch
        {
            // Swallow logging errors to remain unobtrusive
        }
        // Return null so nothing is rendered to the terminal
        return null;
    }

    private static string Serialize(FeedbackContext context, float[]? emb)
    {
        var trigger = context.Trigger.ToString();
        var cmdLine = context.CommandLine ?? string.Empty;
        var time = DateTime.UtcNow.ToString("o");

        string? errorType = null;
        string? errorMessage = null;
        string? commandName = null;

        if (context.LastError is not null)
        {
            errorType = context.LastError.GetType().FullName;
            errorMessage = context.LastError.Exception?.Message ?? context.LastError.ToString();
            commandName = context.LastError.InvocationInfo?.MyCommand?.Name;
        }
        else if (context.CommandLineAst is CommandAst ca)
        {
            commandName = ca.GetCommandName();
        }

        var vectorInfo = emb is { Length: >0 } ? $"emb:{emb.Length}" : string.Empty;
        // lightweight TSV to keep it simple for now
        // time	trigger	commandName	cmdLine	errorType	errorMessage
        return string.Join("\t", new[]
        {
            time,
            trigger,
            commandName ?? string.Empty,
            cmdLine.Replace("\r", " ").Replace("\n", " "),
            errorType ?? string.Empty,
            errorMessage?.Replace("\r", " ").Replace("\n", " ") ?? string.Empty,
            vectorInfo
        });
    }


}