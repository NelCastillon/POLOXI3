namespace Ams.Application.Features.Compliance;

public sealed class AddAudienceMemberRequest
{
    public string  TargetTypeCode { get; set; } = string.Empty;
    public Guid?   TargetId       { get; set; }
    public string  TargetName     { get; set; } = string.Empty;
    public bool    IsRequired     { get; set; } = true;
    public Guid?   AddedByUserId  { get; set; }
}
