using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Common.Dtos;

public sealed class PolicyCreationSourceDto
{
    public Guid PolicyCreationSourceId { get; set; }
    public Guid TenantId { get; set; }

    [Required, StringLength(50)]
    public string SourceCode { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string SourceName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool RequiresQuote { get; set; }
    public bool RequiresSubmission { get; set; }
    public bool RequiresAccount { get; set; }
    public bool RequiresReason { get; set; }
    public bool RequiresPolicyNumber { get; set; }
    public bool AllowsDirectPolicyEntry { get; set; }
    public bool IsImportSource { get; set; }
    public bool IsConversionSource { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}
