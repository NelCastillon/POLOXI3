namespace Ams.Application.Common.Dtos;

public sealed class UserPermissionScopeDto
{
    public Guid     UserPermissionScopeId { get; set; }
    public Guid     UserPermissionId      { get; set; }
    public string   ScopeTypeCode         { get; set; } = string.Empty;
    public string   ScopeValue            { get; set; } = string.Empty;
    public Guid?    CreatedByUserId       { get; set; }
    public DateTime CreatedDateUtc        { get; set; }
}
