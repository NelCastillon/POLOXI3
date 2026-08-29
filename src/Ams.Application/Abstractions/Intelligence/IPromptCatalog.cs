namespace Ams.Application.Abstractions.Intelligence;

// Resolves the system prompt for an LLM feature call. Prompts are user-managed in the
// AI.PromptDefinition registry; when no approved, effective row exists for the prompt code the
// embedded default from IntelligencePromptDefaults is returned so behavior is unchanged.
public interface IPromptCatalog
{
    Task<string> GetSystemPromptAsync(Guid tenantId,string promptCode,CancellationToken cancellationToken=default);
}
