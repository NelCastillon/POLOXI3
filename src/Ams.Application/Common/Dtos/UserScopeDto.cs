namespace Ams.Application.Common.Dtos;

public sealed class UserScopeDto
{
    public Guid UserScopeId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string? UserFullName { get; set; }
    public string ScopeTypeCode { get; set; } = string.Empty;
    public string ScopeValue { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? GrantedByFullName { get; set; }
    public DateTime GrantedDateUtc { get; set; }
    public DateTime? ExpiresDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
