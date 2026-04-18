using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class Lead : AuditableEntity
{
    public string LeadNumber { get; private set; } = string.Empty;
    public string? AccountName { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? InterestedService { get; private set; }
    public int? Score { get; private set; }
    public string? PriorityCode { get; private set; }
    public Guid? AssignedToUserId { get; private set; }
    public LeadStatus Status { get; private set; }

    private Lead() { }

    public Lead(Guid tenantId, string leadNumber, string firstName, string lastName, string? email, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        LeadNumber = leadNumber;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Status = LeadStatus.New;
    }
}
