namespace Ams.Worker.Automation;

public sealed class JobStepExecutionResult
{
    public bool Succeeded { get; init; }
    public bool CompletedWithWarnings { get; init; }
    public string OutputJson { get; init; } = "{}";
    public string? ErrorMessage { get; init; }

    public static JobStepExecutionResult Success(string outputJson = "{}") => new() { Succeeded = true, OutputJson = outputJson };

    public static JobStepExecutionResult Warning(string outputJson, string message) => new() { Succeeded = true, CompletedWithWarnings = true, OutputJson = outputJson, ErrorMessage = message };

    public static JobStepExecutionResult Failure(string message, string outputJson = "{}") => new() { Succeeded = false, OutputJson = outputJson, ErrorMessage = message };
}
