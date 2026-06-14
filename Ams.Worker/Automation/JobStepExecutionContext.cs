using Ams.Application.Common.Dtos;

namespace Ams.Worker.Automation;

public sealed class JobStepExecutionContext
{
    public required JobDefinitionDto Job { get; init; }
    public required JobRunDto JobRun { get; init; }
    public required JobStepDto Step { get; init; }
    public required Guid JobStepRunId { get; init; }
    public required string ExecutionContextJson { get; init; }
    public required string InputJson { get; init; }
}
