using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Leads;

public sealed class ConvertLeadRequest
{
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid? ExistingAccountId { get; set; }

    [StringLength(200)]
    public string? AccountName { get; set; }

    [Required, StringLength(200)]
    public string OpportunityName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? LineOfBusiness { get; set; }

    [Range(0, 999999999999)]
    public decimal? EstimatedAmount { get; set; }

    public DateTime? CloseDate { get; set; }
    public Guid? OwnerUserId { get; set; }
    public Guid ConvertedByUserId { get; set; }
    public bool CreateSubmissionDraft { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}
