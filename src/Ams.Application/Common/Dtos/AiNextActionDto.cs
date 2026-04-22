namespace Ams.Application.Common.Dtos;

public sealed class AiNextActionDto
{
    public Guid ActionId { get; set; }
    public Guid TenantId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityName { get; set; }
    public DateTime SuggestedByUtc { get; set; }
}
