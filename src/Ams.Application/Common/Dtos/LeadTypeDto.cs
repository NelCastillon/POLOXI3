using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Common.Dtos;

public sealed class LeadTypeDto
{
    public Guid LeadTypeId { get; set; }
    public Guid TenantId { get; set; }

    [Required, StringLength(50)]
    public string TypeCode { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string TypeName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required, StringLength(50)]
    public string AccountTypeCode { get; set; } = string.Empty;

    public bool RequiresCompany { get; set; }
    public bool PersonAccountFallback { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}
