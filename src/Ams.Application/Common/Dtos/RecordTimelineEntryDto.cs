namespace Ams.Application.Common.Dtos;

public sealed class RecordTimelineEntryDto
{
    public Guid EntryId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public Guid? PerformedByUserId { get; set; }
    public DateTime OccurredDateUtc { get; set; }
}
