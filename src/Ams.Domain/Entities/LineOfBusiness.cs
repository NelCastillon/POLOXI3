using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class LineOfBusiness : AuditableEntity
{
    public string LobCode       { get; private set; } = string.Empty;
    public string LobName       { get; private set; } = string.Empty;
    public string Category      { get; private set; } = string.Empty;
    public string? Description  { get; private set; }
    public bool   IsActive      { get; private set; } = true;

    private LineOfBusiness() { }

    public LineOfBusiness(Guid tenantId, string lobCode, string lobName, string category, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        LobCode  = lobCode;
        LobName  = lobName;
        Category = category;
    }
}
