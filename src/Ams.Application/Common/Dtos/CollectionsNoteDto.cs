namespace Ams.Application.Common.Dtos;

public sealed class CollectionsNoteDto
{
    public Guid CollectionsNoteId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? InvoiceId { get; set; }
    public DateOnly NoteDate { get; set; }
    public string NoteText { get; set; } = string.Empty;
    public string ActionCode { get; set; } = "CallMade";
    public DateOnly? NextFollowUpDate { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
