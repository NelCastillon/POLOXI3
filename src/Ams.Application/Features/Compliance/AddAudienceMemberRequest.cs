using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Compliance;

public sealed class AddAudienceMemberRequest
{
    [Required, StringLength(50)]
    public string  TargetTypeCode { get; set; } = string.Empty;
    public Guid?   TargetId       { get; set; }
    [Required, StringLength(200)]
    public string  TargetName     { get; set; } = string.Empty;
    public bool    IsRequired     { get; set; } = true;
    public Guid?   AddedByUserId  { get; set; }
}
