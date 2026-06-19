using System.ComponentModel.DataAnnotations;
using Ams.Application.Common.Validation;

namespace Ams.Application.Common.Dtos;

public sealed class ContactDto
{
    public Guid ContactId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public string? AccountName { get; set; }
    [Required, StringLength(150)]
    public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(150)]
    public string LastName { get; set; } = string.Empty;
    [AmsEmailAddress, StringLength(300)]
    public string? Email { get; set; }
    [AmsPhone, StringLength(50)]
    public string? Phone { get; set; }
    [StringLength(200)]
    public string? JobTitle { get; set; }
    [Required, StringLength(50)]
    public string ContactTypeCode { get; set; } = string.Empty;
    public bool IsBillingContact { get; set; }
    public bool IsPortalUser { get; set; }
    public bool IsKeyContact { get; set; }
    public bool IsServiceContact { get; set; }
    public Guid? ParentContactId { get; set; }
    [StringLength(50)]
    public string? PreferredContactMethod { get; set; }
    [Required, StringLength(50)]
    public string StatusCode { get; set; } = string.Empty;
    public int StatusCodeId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
