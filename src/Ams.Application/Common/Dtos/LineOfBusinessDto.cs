namespace Ams.Application.Common.Dtos;

public sealed class LineOfBusinessDto
{
    public Guid     LobId          { get; set; }
    public Guid     TenantId       { get; set; }
    public string   LobCode        { get; set; } = string.Empty;
    public string   LobName        { get; set; } = string.Empty;
    public string   Category       { get; set; } = string.Empty;
    public string?  Description    { get; set; }
    public bool     IsActive       { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
