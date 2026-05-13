namespace Ams.Application.Common.Dtos;

public sealed class DownloadReportsRequest
{
    public Guid TenantId { get; set; }
    public List<Guid> ReportDefinitionIds { get; set; } = [];
    public string? SearchTerm { get; set; }
    public string? ModuleCode { get; set; }
    public string? Format { get; set; }
}

public sealed record ReportDownloadFile(string FileName, byte[] Content);

public sealed class ReportPreviewDto
{
    public Guid ReportDefinitionId { get; set; }
    public string ReportCode { get; set; } = string.Empty;
    public string ReportName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string ReportTypeCode { get; set; } = string.Empty;
    public string OutputFormats { get; set; } = string.Empty;
    public DateTime GeneratedDateUtc { get; set; }
    public int RowCount { get; set; }
    public List<ReportPreviewColumnDto> Columns { get; set; } = [];
    public List<Dictionary<string, string>> Rows { get; set; } = [];
}

public sealed record ReportPreviewColumnDto(string Field, string Header, string DataType = "Text");
