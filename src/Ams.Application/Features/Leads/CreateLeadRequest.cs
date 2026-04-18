namespace Ams.Application.Features.Leads;

public sealed class CreateLeadRequest
{
    public Guid TenantId { get; set; }
    public string LeadNumber { get; set; } = string.Empty;
    public string? AccountName { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? InterestedService { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
