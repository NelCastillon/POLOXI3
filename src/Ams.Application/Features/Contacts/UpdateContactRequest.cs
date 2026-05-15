using System.ComponentModel.DataAnnotations;
using Ams.Application.Common.Validation;

namespace Ams.Application.Features.Contacts;

public sealed class UpdateContactRequest
{
    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;
    [AmsEmailAddress, StringLength(200)]
    public string? Email { get; set; }
    [AmsPhone, StringLength(50)]
    public string? Phone { get; set; }
    [StringLength(100)]
    public string? JobTitle { get; set; }
    [Required, StringLength(50)]
    public string ContactTypeCode { get; set; } = "Primary";
    public bool IsBillingContact { get; set; }
    public bool IsServiceContact { get; set; }
    public bool IsPortalUser { get; set; }
    public bool IsKeyContact { get; set; }
    [StringLength(50)]
    public string? PreferredContactMethod { get; set; }
    [Required, StringLength(50)]
    public string StatusCode { get; set; } = "Active";
    public Guid? ModifiedByUserId { get; set; }
}
