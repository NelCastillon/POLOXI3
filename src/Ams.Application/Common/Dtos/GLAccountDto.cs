namespace Ams.Application.Common.Dtos;

public sealed class GLAccountDto
{
    public Guid GLAccountId { get; set; }
    public Guid TenantId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountTypeCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentGLAccountId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
