namespace Ams.Application.Common.Dtos;

public sealed class FieldSecurityPolicyDto
{
    public Guid PolicyId { get; set; }
    public Guid TenantId { get; set; }
    public Guid RoleId { get; set; }
    public string? RoleName { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public bool IsHidden { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
