namespace Ams.Application.Features.Sod;

public sealed class CreateSodExceptionRequest
{
    public string    Justification     { get; set; } = string.Empty;
    public Guid?     RequestedByUserId { get; set; }
    public DateTime? ExpiresDateUtc    { get; set; }
}
