using System;

namespace ConsoleCritic.Provider
{
    public record InvocationRecord(
        DateTime Timestamp,
        string Trigger,
        string CommandName,
        string CommandLine,
        string? ErrorType,
        string? ErrorMessage,
        float[]? Embedding,
        string? Summary
    );
}
