using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class AccountActivity : AuditableEntity
{
    public Guid AccountId { get; private set; }
    public string ActivityType { get; private set; } = string.Empty; // Call, Email, Meeting, Note, Task
    public string Subject { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string? Outcome { get; private set; }
    public int? DurationMinutes { get; private set; }

    private AccountActivity() { }

    public AccountActivity(
        Guid tenantId,
        Guid accountId,
        string activityType,
        string subject,
        DateTime occurredAtUtc,
        Guid? createdByUserId,
        string? notes = null,
        string? outcome = null,
        int? durationMinutes = null)
        : base(tenantId, createdByUserId)
    {
        AccountId = accountId;
        ActivityType = activityType;
        Subject = subject;
        Notes = notes;
        OccurredAtUtc = occurredAtUtc;
        Outcome = outcome;
        DurationMinutes = durationMinutes;
    }

    public void Update(string subject, string? notes, DateTime occurredAtUtc, string? outcome, int? durationMinutes, Guid? modifiedByUserId)
    {
        Subject = subject;
        Notes = notes;
        OccurredAtUtc = occurredAtUtc;
        Outcome = outcome;
        DurationMinutes = durationMinutes;
        MarkModified(modifiedByUserId);
    }
}
