using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class Submission : AuditableEntity
{
    public Guid AccountId { get; private set; }
    public string SubmissionNumber { get; private set; } = string.Empty;
    public string LineOfBusiness { get; private set; } = string.Empty;
    public Guid? CarrierId { get; private set; }
    public string? CarrierName { get; private set; }
    public string StatusCode { get; private set; } = "Submitted"; // Submitted, Quoted, Bound, Declined, Expired
    public DateTime SubmittedAtUtc { get; private set; }
    public DateTime? DueDateUtc { get; private set; }
    public DateTime? QuotedAtUtc { get; private set; }
    public decimal? QuotedPremium { get; private set; }
    public DateTime? BoundAtUtc { get; private set; }
    public string? Notes { get; private set; }
    public string? DeclineReason { get; private set; }

    private Submission() { }

    public Submission(
        Guid tenantId,
        Guid accountId,
        string submissionNumber,
        string lineOfBusiness,
        DateTime submittedAtUtc,
        Guid? createdByUserId,
        Guid? carrierId = null,
        string? carrierName = null,
        DateTime? dueDateUtc = null,
        string? notes = null)
        : base(tenantId, createdByUserId)
    {
        AccountId = accountId;
        SubmissionNumber = submissionNumber;
        LineOfBusiness = lineOfBusiness;
        CarrierId = carrierId;
        CarrierName = carrierName;
        SubmittedAtUtc = submittedAtUtc;
        DueDateUtc = dueDateUtc;
        Notes = notes;
        StatusCode = "Submitted";
    }

    public void MarkQuoted(decimal quotedPremium, DateTime quotedAtUtc, Guid? modifiedByUserId)
    {
        StatusCode = "Quoted";
        QuotedPremium = quotedPremium;
        QuotedAtUtc = quotedAtUtc;
        MarkModified(modifiedByUserId);
    }

    public void MarkBound(DateTime boundAtUtc, Guid? modifiedByUserId)
    {
        StatusCode = "Bound";
        BoundAtUtc = boundAtUtc;
        MarkModified(modifiedByUserId);
    }

    public void MarkDeclined(string declineReason, Guid? modifiedByUserId)
    {
        StatusCode = "Declined";
        DeclineReason = declineReason;
        MarkModified(modifiedByUserId);
    }

    public void UpdateNotes(string? notes, Guid? modifiedByUserId)
    {
        Notes = notes;
        MarkModified(modifiedByUserId);
    }
}
