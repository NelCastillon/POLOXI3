namespace Ams.Domain.Entities;

public sealed class AccountNote
{
    public Guid AccountNoteId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public string NoteText { get; set; } = string.Empty;
    public string NoteTypeCode { get; set; } = "General";
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public bool IsDeleted { get; set; }
}
