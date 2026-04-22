namespace Ams.Application.Common.Dtos;

public sealed class CarrierDto
{
    public Guid      CarrierId       { get; set; }
    public Guid      TenantId        { get; set; }
    public string    CarrierName     { get; set; } = string.Empty;
    public string    NaicCode        { get; set; } = string.Empty;
    public string    AmBestRating    { get; set; } = "NR";
    public bool      IsAdmitted      { get; set; }
    public DateTime? AppointmentDate { get; set; }
    public bool      IsActive        { get; set; }
    public DateTime  CreatedDateUtc  { get; set; }
}
