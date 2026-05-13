namespace Ams.Application.Common.Dtos;

public sealed class AiInsightCardDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string[] AffectedEntities { get; set; } = [];
    public string Recommendation { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public bool Dismissed { get; set; }
    public bool TaskCreated { get; set; }
}

public sealed class AiAssistantConfigDto
{
    public List<string> Starters { get; set; } = [];
    public List<AiAssistantCapabilityDto> Capabilities { get; set; } = [];
}

public sealed class AiAssistantCapabilityDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-stars";
    public string IconCss { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
}

public sealed class AiAssistantAskRequest
{
    public Guid TenantId { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string ContextType { get; set; } = "global";
    public string? ContextValue { get; set; }
}

public sealed class AiAssistantResponseDto
{
    public string Response { get; set; } = string.Empty;
    public List<AiAssistantActionDto> Actions { get; set; } = [];
}

public sealed class AiAssistantActionDto
{
    public string Label { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-arrow-right";
}
