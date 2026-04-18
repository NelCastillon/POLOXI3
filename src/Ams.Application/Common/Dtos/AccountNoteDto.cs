namespace Ams.Application.Common.Dtos;

public sealed class AccountNoteDto
{
    public Guid AccountNoteId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string NoteText { get; set; } = string.Empty;
    public string NoteTypeCode { get; set; } = string.Empty;
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
