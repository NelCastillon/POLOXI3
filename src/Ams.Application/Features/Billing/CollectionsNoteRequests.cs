using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Billing;

public sealed class CreateCollectionsNoteRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid AccountId { get; set; }

    public Guid? InvoiceId { get; set; }

    [Required]
    public DateOnly NoteDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Required]
    [StringLength(2000)]
    public string NoteText { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string ActionCode { get; set; } = "Called";

    public DateOnly? NextFollowUpDate { get; set; }

    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateCollectionsNoteRequest
{
    [Required]
    public Guid CollectionsNoteId { get; set; }

    [Required]
    public Guid AccountId { get; set; }

    public Guid? InvoiceId { get; set; }

    [Required]
    public DateOnly NoteDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Required]
    [StringLength(2000)]
    public string NoteText { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string ActionCode { get; set; } = "Called";

    public DateOnly? NextFollowUpDate { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}
