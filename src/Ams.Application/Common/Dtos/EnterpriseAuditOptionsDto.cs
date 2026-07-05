namespace Ams.Application.Common.Dtos;

public sealed class EnterpriseAuditOptionsDto
{
    public IReadOnlyList<string> Categories { get; set; } = [];
    public IReadOnlyList<string> Modules { get; set; } = [];
    public IReadOnlyList<string> Severities { get; set; } = [];
    public IReadOnlyList<string> Statuses { get; set; } = [];
    public IReadOnlyList<string> SourceSystems { get; set; } = [];
    public IReadOnlyList<string> ActorTypes { get; set; } = [];
}
