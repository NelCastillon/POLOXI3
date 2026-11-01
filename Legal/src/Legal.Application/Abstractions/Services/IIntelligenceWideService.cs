using Legal.Application.Features.Intelligence;

namespace Legal.Application.Abstractions.Services;

// Isolated "Wide" variant of the POLOXI search contract. Mirrors IIntelligenceService.SearchWithPoloxiAsync
// so /intelligence/search/poloxi_wide can evolve independently from /intelligence/search/poloxi.
public interface IIntelligenceWideService
{
    Task<PoloxiSearchResponse> SearchWithPoloxiWideAsync(PoloxiSearchRequest request,CancellationToken cancellationToken=default);
    Task<WideSearchResponse> SearchDynamicAsync(WideSearchRequest request,CancellationToken cancellationToken=default);
    // Database-backed model options for the wide-search Model dropdown (active CHAT deployments).
    Task<IReadOnlyCollection<WideModelOptionDto>> GetWideModelsAsync(Guid tenantId,CancellationToken cancellationToken=default);
    // Database-backed context options for the wide-search Context dropdown (POLOXI.Legal_SearchContext).
    Task<IReadOnlyCollection<WideSearchContextDto>> GetSearchContextsAsync(Guid tenantId,CancellationToken cancellationToken=default);
    // Database-backed example-prompt chips for the wide-search composer (POLOXI.Legal_SearchExamplePrompt).
    Task<IReadOnlyCollection<WideSearchExamplePromptDto>> GetSearchExamplePromptsAsync(Guid tenantId,CancellationToken cancellationToken=default);
    // Editable Legal Grounding settings (Core.ConfigurationSetting) surfaced on /legal/configuration.
    Task<IReadOnlyCollection<LegalGroundingSettingDto>> GetLegalGroundingSettingsAsync(CancellationToken cancellationToken=default);
    Task SaveLegalGroundingSettingAsync(SaveLegalGroundingSettingRequest request,Guid actorUserId,CancellationToken cancellationToken=default);
    Task DeleteLegalGroundingSettingAsync(string settingKey,CancellationToken cancellationToken=default);
    // Editable Epistemic Authority (POLOXI EA) settings surfaced on /legal/configuration.
    Task<IReadOnlyCollection<EpistemicSettingDto>> GetEpistemicSettingsAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task SaveEpistemicSettingAsync(SaveEpistemicSettingRequest request,Guid tenantId,Guid actorUserId,CancellationToken cancellationToken=default);
    // Editable matter evidence-scope threshold settings (Intelligence.SearchWide.MatterScope*) surfaced on /legal/configuration.
    Task<IReadOnlyCollection<MatterScopeSettingDto>> GetMatterScopeSettingsAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task SaveMatterScopeSettingAsync(SaveMatterScopeSettingRequest request,Guid tenantId,Guid actorUserId,CancellationToken cancellationToken=default);
    // Platform UI toggle controlling whether /legal/search shows the End-to-end POLOXI pipeline section.
    Task<bool> GetShowPipelineAsync(CancellationToken cancellationToken=default);
    Task SaveShowPipelineAsync(bool showPipeline,CancellationToken cancellationToken=default);
    // /legal/search Run History (per-user): paged list of the user's own past runs, newest first.
    Task<WideSearchRunHistoryPage> GetSearchRunHistoryAsync(Guid tenantId,Guid userId,int page,int pageSize,CancellationToken cancellationToken=default);
    // /legal/search Run History: full reopen payload (saved WideSearchResponse) for one of the user's runs.
    Task<WideSearchRunDetailDto?> GetSearchRunResultAsync(Guid tenantId,Guid userId,Guid wideSearchRunResultId,CancellationToken cancellationToken=default);
    // /legal/search continuous-chat thread: all runs sharing a ConversationId, oldest-first, owned by the user.
    Task<IReadOnlyList<WideConversationTurnDto>> GetSearchConversationAsync(Guid tenantId,Guid userId,Guid conversationId,CancellationToken cancellationToken=default);
}
