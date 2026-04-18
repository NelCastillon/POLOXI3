namespace Ams.Application.Features.Governance;

public sealed class ProcessAccessRequestRequest
{
    public string  ActionCode        { get; set; } = string.Empty; // Approve | Reject | Return | Comment
    public string? Comment           { get; set; }
    public Guid    ProcessedByUserId { get; set; }
}
