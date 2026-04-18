namespace Ams.Application.Common.Dtos;

public sealed class BranchDto
{
    public Guid BranchId { get; set; }
    public Guid TenantId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? CountryCode { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
