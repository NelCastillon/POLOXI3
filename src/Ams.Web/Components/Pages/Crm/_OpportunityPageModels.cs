using System.ComponentModel.DataAnnotations;
using Ams.Application.Common.Dtos;

namespace Ams.Web.Components.Pages.Crm;

internal static class OpportunityPageConstants
{
    public const string DefaultStageName = "Qualification";
    public const string DefaultForecastCategory = "Pipeline";

    public static string StageName(int statusCode) => statusCode switch
    {
        1 => "Qualification",
        2 => "Needs Analysis",
        3 => "Proposal",
        4 => "Negotiation",
        5 => "Closed Won",
        6 => "Closed Lost",
        _ => DefaultStageName
    };

    public static int StageCode(string stage) => stage switch
    {
        "Qualification" => 1,
        "Needs Analysis" => 2,
        "Proposal" => 3,
        "Negotiation" => 4,
        "Closed Won" => 5,
        "Closed Lost" => 6,
        _ => 1
    };

    public static int StageOrder(string stage) => stage switch
    {
        "Qualification" => 0,
        "Needs Analysis" => 1,
        "Proposal" => 2,
        "Negotiation" => 3,
        "Closed Won" => 4,
        "Closed Lost" => 5,
        _ => 99
    };

    public static string StageBadge(string stage) => stage switch
    {
        "Qualification" => "um-badge um-badge-info",
        "Needs Analysis" => "um-badge um-badge-warning",
        "Proposal" => "um-badge um-badge-info",
        "Negotiation" => "um-badge um-badge-warning",
        "Closed Won" => "um-badge um-badge-success",
        "Closed Lost" => "um-badge um-badge-danger",
        _ => "um-badge um-badge-neutral"
    };

    public static string ForecastBadge(string category) => category switch
    {
        "Commit" => "um-badge um-badge-success",
        "Best Case" => "um-badge um-badge-info",
        "Closed" => "um-badge um-badge-neutral",
        _ => "um-badge um-badge-warning"
    };

    public static IReadOnlyList<string> ActiveStageNames(IEnumerable<OpportunityStageDto> stages)
    {
        return stages
            .Where(s => s.IsActive && !string.IsNullOrWhiteSpace(s.StageName))
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.StageName)
            .Select(s => s.StageName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<string> ForecastCategoryNames(IEnumerable<OpportunityForecastCategoryDto> categories)
    {
        return categories
            .Where(c => c.IsActive && !string.IsNullOrWhiteSpace(c.CategoryName))
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.CategoryName)
            .Select(c => c.CategoryName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string NormalizeForecast(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultForecastCategory;
        }

        return value.Trim().Equals("Closed Won", StringComparison.OrdinalIgnoreCase) || value.Trim().Equals("Closed Lost", StringComparison.OrdinalIgnoreCase)
            ? "Closed"
            : value.Trim();
    }

    public static string Initials(string name)
    {
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}" : name.Length > 0 ? name[..1] : "?";
    }
}

internal sealed class OpportunityFormModel
{
    [StringLength(50, ErrorMessage = "Opportunity Number cannot exceed 50 characters.")]
    public string OpportunityNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Account is required.")]
    public Guid? AccountId { get; set; }

    [Required(ErrorMessage = "Opportunity Name is required.")]
    [StringLength(200, ErrorMessage = "Opportunity Name cannot exceed 200 characters.")]
    public string OpportunityName { get; set; } = string.Empty;

    [Range(0, 999999999999, ErrorMessage = "Estimated Amount must be 0 or greater.")]
    public decimal EstimatedAmount { get; set; }

    [Range(0, 100, ErrorMessage = "Win Probability must be between 0 and 100.")]
    public decimal WinProbability { get; set; } = 50;

    public DateTime? CloseDate { get; set; }

    [Required(ErrorMessage = "Forecast Category is required.")]
    [StringLength(50, ErrorMessage = "Forecast Category cannot exceed 50 characters.")]
    public string ForecastCategoryCode { get; set; } = OpportunityPageConstants.DefaultForecastCategory;

    public Guid? OwnerUserId { get; set; }
    public Guid? LeadId { get; set; }
}

internal sealed record OpportunityRow(Guid OpportunityId, string OpportunityNumber, string OpportunityName, Guid AccountId, string AccountName, decimal EstimatedAmount, decimal WeightedAmount, string Stage, decimal WinProbability, string ForecastCategory, DateTime? CloseDate, Guid? OwnerUserId);

internal static class OpportunityPageData
{
    public static OpportunityRow FromDto(OpportunityDto dto)
    {
        var stage = OpportunityPageConstants.StageName(dto.StatusCode);
        return new(dto.OpportunityId, dto.OpportunityNumber, dto.OpportunityName, dto.AccountId, dto.AccountName ?? string.Empty, dto.EstimatedAmount, dto.EstimatedAmount * dto.WinProbability / 100m, stage, dto.WinProbability, OpportunityPageConstants.NormalizeForecast(dto.ForecastCategoryCode), dto.CloseDate, dto.OwnerUserId);
    }
}
