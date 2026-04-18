namespace Ams.Domain.Entities;

public sealed class ReportDefinition
{
    public Guid ReportDefinitionId { get; private set; } = Guid.NewGuid();
    public Guid? TenantId { get; private set; }
    public string ReportCode { get; private set; } = string.Empty;
    public string ReportName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string ModuleCode { get; private set; } = string.Empty;
    public string ReportTypeCode { get; private set; } = "Tabular";
    public string? QueryTemplate { get; private set; }
    public string? DefaultParameters { get; private set; }
    public string OutputFormats { get; private set; } = "PDF,Excel,CSV";
    public bool IsSystemReport { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedDateUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? ModifiedDateUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public bool IsDeleted { get; private set; }

    private ReportDefinition() { }

    public ReportDefinition(string reportCode, string reportName, string moduleCode)
    {
        ReportCode = reportCode;
        ReportName = reportName;
        ModuleCode = moduleCode;
    }
}
