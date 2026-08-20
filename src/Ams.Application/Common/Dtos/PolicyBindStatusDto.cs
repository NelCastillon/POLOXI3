namespace Ams.Application.Common.Dtos;

public sealed class PolicyBindStatusDto
{
    public Guid PolicyBindStatusId { get; set; }
    public Guid TenantId { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsTerminal { get; set; }
    public bool CreatesPolicy { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}
