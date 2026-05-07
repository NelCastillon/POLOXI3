using Ams.Application.Common.Dtos;

namespace Ams.Web.Components.Pages;

internal static class SubmissionPageConstants
{
    public static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly string[] Statuses = ["Draft", "New", "In Review", "Quoted", "Bound", "Declined", "Withdrawn"];
    public static readonly string[] LinesOfBusiness = ["General Liability", "Commercial Property", "Commercial Auto", "Workers Comp", "Umbrella / Excess", "Professional Liability", "Home / Dwelling", "Personal Auto"];

    public static string NormalizeStatus(string status) => status switch
    {
        "Draft" => "New",
        "Submitted" => "In Review",
        "Under Review" => "In Review",
        "Approved" => "Quoted",
        "Rejected" => "Declined",
        _ => status
    };

    public static string StatusBadge(string status)
    {
        var normalized = NormalizeStatus(status).ToLowerInvariant().Replace(" ", "-");
        return $"sr-status sr-status-{normalized}";
    }

    public static string LobLabel(string lineOfBusiness) => string.IsNullOrWhiteSpace(lineOfBusiness) ? "Unknown" : lineOfBusiness;
}

internal static class SubmissionPageData
{
    public static SubmissionRegisterRow ToRegisterRow(SubmissionDto dto) => new()
    {
        SubmissionNo = dto.SubmissionNumber,
        SubId = dto.SubmissionId,
        Account = dto.AccountName,
        Insured = dto.AccountName,
        Lob = dto.LineOfBusiness,
        Status = SubmissionPageConstants.NormalizeStatus(dto.Status),
        Producer = dto.AssignedToUserName ?? "Unassigned",
        Csr = dto.AssignedToUserName ?? "Unassigned",
        SubmitDate = dto.CreatedDateUtc,
        EffDate = dto.EffectiveDate,
        DueDate = dto.EffectiveDate.AddDays(-14),
        QuotedPremium = dto.Status is "Quoted" or "Bound" ? dto.TargetPremium ?? 0 : 0,
        Markets = dto.MarketCount,
        QuoteCount = dto.QuoteCount,
        TargetPremium = dto.TargetPremium ?? 0,
        Priority = dto.Priority
    };

    public static QuoteRegisterRow ToQuoteRow(SubmissionDto dto) => new()
    {
        QuoteId = dto.SubmissionId,
        QuoteNo = $"QUO-{dto.SubmissionNumber.Replace("SUB-", string.Empty)}",
        SubmissionNo = dto.SubmissionNumber,
        SubId = dto.SubmissionId,
        Account = dto.AccountName,
        Lob = dto.LineOfBusiness,
        Carrier = dto.QuoteCount > 0 ? "Multiple Markets" : "Pending Market",
        Status = dto.Status == "Bound" ? "Accepted" : dto.Status == "Declined" ? "Declined" : dto.QuoteCount > 0 ? "Pending" : "Expired",
        QuotedPremium = dto.TargetPremium ?? 0,
        EffDate = dto.EffectiveDate,
        ExpiryDate = dto.ExpirationDate,
        Producer = dto.AssignedToUserName ?? "Unassigned"
    };

    public static ApplicationRegisterRow ToApplicationRow(SubmissionDto dto) => new()
    {
        AppId = dto.SubmissionId,
        AppNo = $"APP-{dto.SubmissionNumber.Replace("SUB-", string.Empty)}",
        SubmissionNo = dto.SubmissionNumber,
        SubId = dto.SubmissionId,
        Account = dto.AccountName,
        Lob = dto.LineOfBusiness,
        Carrier = dto.MarketCount > 0 ? "Markets Selected" : "Not Assigned",
        Status = dto.Status switch { "Draft" => "Draft", "New" => "Submitted", "In Review" => "Under Review", "Quoted" or "Bound" => "Approved", "Declined" or "Withdrawn" => "Rejected", _ => "Submitted" },
        Completeness = dto.Status switch { "Draft" => 35, "New" => 55, "In Review" => 75, "Quoted" or "Bound" => 100, _ => 60 },
        SubmittedDate = dto.CreatedDateUtc,
        DueDate = dto.EffectiveDate.AddDays(-10),
        Producer = dto.AssignedToUserName ?? "Unassigned"
    };

    public static DeclineRegisterRow ToDeclineRow(SubmissionDto dto) => new()
    {
        DeclineId = dto.SubmissionId,
        SubmissionNo = dto.SubmissionNumber,
        SubId = dto.SubmissionId,
        Account = dto.AccountName,
        Lob = dto.LineOfBusiness,
        Carrier = dto.MarketCount > 0 ? "Market Response" : "Internal",
        DeclineType = dto.Status == "Withdrawn" ? "Withdrawn" : "Carrier",
        DeclineReason = dto.Status == "Withdrawn" ? "Client withdrawn" : "Underwriting declined",
        DeclineDate = dto.ModifiedDateUtc ?? dto.CreatedDateUtc,
        LostPremium = dto.TargetPremium ?? 0,
        RemarketingStatus = dto.Status == "Declined" ? "Pending" : "Not Started",
        Producer = dto.AssignedToUserName ?? "Unassigned"
    };
}

internal class SubmissionRegisterRow
{
    public string SubmissionNo { get; set; } = string.Empty;
    public Guid SubId { get; set; }
    public string Account { get; set; } = string.Empty;
    public string Insured { get; set; } = string.Empty;
    public string Lob { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Producer { get; set; } = string.Empty;
    public string Csr { get; set; } = string.Empty;
    public DateTime SubmitDate { get; set; }
    public DateTime EffDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal QuotedPremium { get; set; }
    public int Markets { get; set; }
    public int QuoteCount { get; set; }
    public decimal TargetPremium { get; set; }
    public string Priority { get; set; } = string.Empty;
}

internal class QuoteRegisterRow
{
    public Guid QuoteId { get; set; }
    public string QuoteNo { get; set; } = string.Empty;
    public string SubmissionNo { get; set; } = string.Empty;
    public Guid SubId { get; set; }
    public string Account { get; set; } = string.Empty;
    public string Lob { get; set; } = string.Empty;
    public string Carrier { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal QuotedPremium { get; set; }
    public DateTime EffDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string Producer { get; set; } = string.Empty;
}

internal class ApplicationRegisterRow
{
    public Guid AppId { get; set; }
    public string AppNo { get; set; } = string.Empty;
    public string SubmissionNo { get; set; } = string.Empty;
    public Guid SubId { get; set; }
    public string Account { get; set; } = string.Empty;
    public string Lob { get; set; } = string.Empty;
    public string Carrier { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Completeness { get; set; }
    public DateTime SubmittedDate { get; set; }
    public DateTime DueDate { get; set; }
    public string Producer { get; set; } = string.Empty;
}

internal class DeclineRegisterRow
{
    public Guid DeclineId { get; set; }
    public string SubmissionNo { get; set; } = string.Empty;
    public Guid SubId { get; set; }
    public string Account { get; set; } = string.Empty;
    public string Lob { get; set; } = string.Empty;
    public string Carrier { get; set; } = string.Empty;
    public string DeclineType { get; set; } = string.Empty;
    public string DeclineReason { get; set; } = string.Empty;
    public DateTime DeclineDate { get; set; }
    public decimal LostPremium { get; set; }
    public string RemarketingStatus { get; set; } = string.Empty;
    public string Producer { get; set; } = string.Empty;
}
