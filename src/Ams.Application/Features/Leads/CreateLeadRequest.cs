using System.ComponentModel.DataAnnotations;
using Ams.Application.Common.Validation;

namespace Ams.Application.Features.Leads;

public sealed class CreateLeadRequest
{
    public Guid TenantId { get; set; }

    [Required, StringLength(50)]
    public string LeadNumber { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string? AccountName { get; set; }

    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [AmsEmailAddress, StringLength(200)]
    public string? Email { get; set; }

    [AmsPhone]
    [StringLength(30)]
    public string? Phone { get; set; }
    [StringLength(1000)]
    public string? InterestedService { get; set; }
    [Range(0, 999999999999)]
    public decimal? AnnualRevenue { get; set; }
    [Range(0, 100)]
    public int? Score { get; set; }
    [StringLength(50)]
    public string? PriorityCode { get; set; }
    [StringLength(50)]
    public string? SourceCode { get; set; }
    [StringLength(50)]
    public string? NurturingStageCode { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
