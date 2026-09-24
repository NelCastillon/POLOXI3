using Legal.Application.Features.Intelligence;

namespace Legal.Application.Abstractions.Services;

// Isolated "Wide" variant of the POLOXI search contract. Mirrors IIntelligenceService.SearchWithPoloxiAsync
// so /intelligence/search/poloxi_wide can evolve independently from /intelligence/search/poloxi.
public interface IIntelligenceWide2Service
{
    Task<PoloxiSearchResponse> SearchWithPoloxiWideAsync(PoloxiSearchRequest request,CancellationToken cancellationToken=default);
    Task<WideSearchResponse> SearchDynamicAsync(WideSearchRequest request,CancellationToken cancellationToken=default);
    // Database-backed model options for the wide-search Model dropdown (active CHAT deployments).
    Task<IReadOnlyCollection<WideModelOptionDto>> GetWideModelsAsync(Guid tenantId,CancellationToken cancellationToken=default);
    // Database-backed context options for the wide-search Context dropdown (POLOXI.Legal_SearchContext).
    Task<IReadOnlyCollection<WideSearchContextDto>> GetSearchContextsAsync(Guid tenantId,CancellationToken cancellationToken=default);
    // Editable Legal Grounding settings (Core.ConfigurationSetting) surfaced on /legal/configuration.
    Task<IReadOnlyCollection<LegalGroundingSettingDto>> GetLegalGroundingSettingsAsync(CancellationToken cancellationToken=default);
    Task SaveLegalGroundingSettingAsync(SaveLegalGroundingSettingRequest request,Guid actorUserId,CancellationToken cancellationToken=default);
    Task DeleteLegalGroundingSettingAsync(string settingKey,CancellationToken cancellationToken=default);
    // Editable Epistemic Authority (POLOXI EA) settings surfaced on /legal/configuration.
    Task<IReadOnlyCollection<EpistemicSettingDto>> GetEpistemicSettingsAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task SaveEpistemicSettingAsync(SaveEpistemicSettingRequest request,Guid tenantId,Guid actorUserId,CancellationToken cancellationToken=default);
    // Platform UI toggle controlling whether /legal/search shows the End-to-end POLOXI pipeline section.
    Task<bool> GetShowPipelineAsync(CancellationToken cancellationToken=default);
    Task SaveShowPipelineAsync(bool showPipeline,CancellationToken cancellationToken=default);
}
