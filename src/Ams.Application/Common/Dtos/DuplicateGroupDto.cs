namespace Ams.Application.Common.Dtos;

public sealed class DuplicateGroupDto
{
    public Guid GroupId { get; set; }
    public Guid TenantId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string MatchKey { get; set; } = string.Empty;
    public string MatchReasons { get; set; } = string.Empty;
    public int ConfidenceScore { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public Guid? PrimaryRecordId { get; set; }
    public string PrimaryName { get; set; } = string.Empty;
    public DateTime DetectedDateUtc { get; set; }
    public DateTime? ResolvedDateUtc { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolutionNotes { get; set; }
    public List<DuplicateRecordDto> Records { get; set; } = [];
}

public sealed class DuplicateRecordDto
{
    public Guid DuplicateRecordId { get; set; }
    public Guid GroupId { get; set; }
    public Guid RecordId { get; set; }
    public string RecordName { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public DateTime? CreatedDateUtc { get; set; }
    public Dictionary<string, string> FieldValues { get; set; } = [];
}
