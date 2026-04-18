namespace Ams.Application.Features.AccountNotes;

public sealed class CreateAccountNoteRequest
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public string NoteText { get; set; } = string.Empty;
    public string NoteTypeCode { get; set; } = "General";
    public Guid? CreatedByUserId { get; set; }
}
