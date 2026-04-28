namespace Ams.Application.Common.Dtos;

public sealed class JournalEntryDto
{
    public Guid JournalEntryId { get; set; }
    public Guid TenantId { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public DateOnly EntryDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
