namespace Ams.Application.Common.Dtos;

public sealed class ReportDefinitionDto
{
    public Guid ReportDefinitionId { get; set; }
    public Guid? TenantId { get; set; }
    public string ReportCode { get; set; } = string.Empty;
    public string ReportName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string ReportTypeCode { get; set; } = string.Empty;
    public string OutputFormats { get; set; } = string.Empty;
    public bool IsSystemReport { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
