namespace Ams.Application.Common.Dtos;

public sealed class JournalEntryDto
{
    public Guid JournalEntryId { get; set; }
    public Guid TenantId { get; set; }
    public string JournalNumber { get; set; } = string.Empty;
    public DateOnly EntryDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public DateTime? PostedDateUtc { get; set; }
    public Guid? PostedByUserId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
