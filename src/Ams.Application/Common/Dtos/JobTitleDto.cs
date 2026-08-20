namespace Ams.Application.Common.Dtos;

public sealed class JobTitleDto
{
    public Guid JobTitleId { get; set; }
    public Guid TenantId { get; set; }
    public string JobTitleCode { get; set; } = string.Empty;
    public string JobTitleName { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}
