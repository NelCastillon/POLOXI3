namespace Ams.Application.Common.Dtos;

public sealed class RecordSecurityPolicyDto
{
    public Guid PolicyId { get; set; }
    public Guid TenantId { get; set; }
    public Guid RoleId { get; set; }
    public string? RoleName { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string PolicyTypeCode { get; set; } = string.Empty;
    public string? FilterExpression { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
