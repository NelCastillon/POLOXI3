namespace Ams.Application.Common.Dtos;

public sealed class PendingAcknowledgementDto
{
    public Guid      AudienceId       { get; set; }
    public Guid      PolicyDocumentId { get; set; }
    public string    PolicyCode       { get; set; } = string.Empty;
    public string    PolicyTitle      { get; set; } = string.Empty;
    public string    PolicyTypeCode   { get; set; } = string.Empty;
    public string    Version          { get; set; } = string.Empty;
    public DateTime? EffectiveDateUtc { get; set; }
    public DateTime? PublishedDateUtc { get; set; }
    public string    TargetTypeCode   { get; set; } = string.Empty;
    public Guid?     TargetUserId     { get; set; }
    public string    TargetName       { get; set; } = string.Empty;
    public bool      IsRequired       { get; set; }
    public int       DaysOverdue      { get; set; }
}
