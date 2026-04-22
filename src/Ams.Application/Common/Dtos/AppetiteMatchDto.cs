namespace Ams.Application.Common.Dtos;

public sealed class AppetiteMatchDto
{
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string LineOfBusiness { get; set; } = string.Empty;
    public int MatchScore { get; set; }
    public string MatchLevel { get; set; } = string.Empty;
    public string[] MatchedCriteria { get; set; } = [];
    public string[] UnmatchedCriteria { get; set; } = [];
    public string? Notes { get; set; }
}
