using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class MarketingCampaignEnrollment : AuditableEntity
{
    public Guid AccountId { get; private set; }
    public Guid CampaignId { get; private set; }
    public string CampaignName { get; private set; } = string.Empty;
    public string StatusCode { get; private set; } = "Active"; // Active, Completed, Paused, Optedout
    public DateTime EnrolledAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public int EmailsSent { get; private set; }
    public int EmailsOpened { get; private set; }
    public int EmailsClicked { get; private set; }
    public DateTime? LastContactUtc { get; private set; }

    private MarketingCampaignEnrollment() { }

    public MarketingCampaignEnrollment(
        Guid tenantId,
        Guid accountId,
        Guid campaignId,
        string campaignName,
        DateTime enrolledAtUtc,
        Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        AccountId = accountId;
        CampaignId = campaignId;
        CampaignName = campaignName;
        EnrolledAtUtc = enrolledAtUtc;
        StatusCode = "Active";
    }

    public void RecordEmailSent(DateTime sentAtUtc)
    {
        EmailsSent++;
        LastContactUtc = sentAtUtc;
    }

    public void RecordEmailOpened()
    {
        EmailsOpened++;
    }

    public void RecordEmailClicked()
    {
        EmailsClicked++;
    }

    public void MarkCompleted(DateTime completedAtUtc, Guid? modifiedByUserId)
    {
        StatusCode = "Completed";
        CompletedAtUtc = completedAtUtc;
        MarkModified(modifiedByUserId);
    }

    public void Pause(Guid? modifiedByUserId)
    {
        StatusCode = "Paused";
        MarkModified(modifiedByUserId);
    }

    public void Resume(Guid? modifiedByUserId)
    {
        StatusCode = "Active";
        MarkModified(modifiedByUserId);
    }

    public void OptOut(Guid? modifiedByUserId)
    {
        StatusCode = "OptedOut";
        MarkModified(modifiedByUserId);
    }
}
