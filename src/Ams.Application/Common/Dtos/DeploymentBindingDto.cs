namespace Ams.Application.Common.Dtos;

public sealed class DeploymentBindingDto
{
    public Guid      DeploymentBindingId   { get; set; }
    public Guid      TenantId              { get; set; }
    public string    TenantName            { get; set; } = string.Empty;
    public Guid?     RegionId              { get; set; }
    public string    RegionCode            { get; set; } = string.Empty;
    public string    RegionName            { get; set; } = string.Empty;
    public string    EnvironmentCode       { get; set; } = string.Empty;
    public string?   StampCode             { get; set; }
    public string    IsolationMode         { get; set; } = string.Empty;
    public bool      IsPrimary             { get; set; }
    public string    StatusCode            { get; set; } = string.Empty;
    public string?   Notes                 { get; set; }
    public DateTime? ProvisionedDateUtc    { get; set; }
    public DateTime? DecommissionedDateUtc { get; set; }
    public DateTime  CreatedDateUtc        { get; set; }
    public DateTime? ModifiedDateUtc       { get; set; }
}
