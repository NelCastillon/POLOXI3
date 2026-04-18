namespace Ams.Application.Features.Sod;

public sealed class ResolveSodConflictRequest
{
    public Guid?   ResolvedByUserId { get; set; }
    public string? ResolutionNote   { get; set; }
}
