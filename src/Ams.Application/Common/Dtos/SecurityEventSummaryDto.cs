namespace Ams.Application.Common.Dtos;

public sealed class SecurityEventSummaryDto
{
    public int TotalHighSeverity    { get; set; }
    public int TotalFailedLogins    { get; set; }
    public int TotalMfaResets       { get; set; }
    public int TotalRoleAssignments { get; set; }
    public int TotalImpersonations  { get; set; }
    public int TotalExports         { get; set; }
    public int Total24h             { get; set; }
    public int GrandTotal           { get; set; }
}
