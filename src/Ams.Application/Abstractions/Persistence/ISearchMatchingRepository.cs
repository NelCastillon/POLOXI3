using Ams.Application.Features.SearchMatching;
using Ams.Application.Features.Intelligence;

namespace Ams.Application.Abstractions.Persistence;

public interface ISearchMatchingRepository
{
    Task<MatchPolicy?> GetPolicyAsync(Guid tenantId, string profileCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchProjection>> GetCandidatesAsync(Guid tenantId, string entityTypeCode, IReadOnlyDictionary<string, string?> fields, int maximumCandidates, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchProjection>> SearchProjectionsAsync(Guid tenantId, string query, string originalQuery, IReadOnlyCollection<string> entityTypeCodes, IReadOnlyCollection<string> grantedPermissions, int maximumResults, CancellationToken cancellationToken = default);
    Task<Guid> BeginExecutionAsync(EntityMatchRequest request, MatchPolicy policy, CancellationToken cancellationToken = default);
    Task CompleteExecutionAsync(Guid matchExecutionId, IReadOnlyList<MatchCandidate> candidates, CancellationToken cancellationToken = default);
    Task FailExecutionAsync(Guid matchExecutionId, string errorMessage, CancellationToken cancellationToken = default);
    Task<int> RefreshProjectionsAsync(CancellationToken cancellationToken = default);
    Task SaveSemanticEvidenceAsync(Guid tenantId, Guid? requestedByUserId, string correlationId, string query, IReadOnlyCollection<string> terms, IReadOnlyCollection<SemanticConceptMatchDto> concepts, CancellationToken cancellationToken = default);
    Task<MatchReviewDecision> SaveReviewDecisionAsync(MatchReviewDecisionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchReviewDecision>> GetReviewDecisionsAsync(Guid tenantId, Guid matchExecutionId, CancellationToken cancellationToken = default);
    Task<SemanticPreprocessingSettings> GetSemanticPreprocessingSettingsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<SearchMatchingAdministration> GetAdministrationAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Guid> SaveProfileAsync(Guid tenantId, Guid actorUserId, SaveMatchProfileSettingRequest request, CancellationToken cancellationToken = default);
    Task DeleteProfileAsync(Guid tenantId, Guid actorUserId, Guid matchProfileId, byte[] rowVersion, CancellationToken cancellationToken = default);
    Task<Guid> SaveFieldRuleAsync(Guid tenantId, Guid actorUserId, SaveMatchFieldRuleSettingRequest request, CancellationToken cancellationToken = default);
    Task DeleteFieldRuleAsync(Guid tenantId, Guid actorUserId, Guid matchFieldRuleId, byte[] rowVersion, CancellationToken cancellationToken = default);
    Task<Guid> SaveAlgorithmAsync(Guid tenantId, Guid actorUserId, SaveMatchAlgorithmSettingRequest request, CancellationToken cancellationToken = default);
    Task DeleteAlgorithmAsync(Guid tenantId, Guid actorUserId, Guid matchAlgorithmId, byte[] rowVersion, CancellationToken cancellationToken = default);
    Task<Guid> SaveNormalizationTermAsync(Guid tenantId, Guid actorUserId, SaveNormalizationTermSettingRequest request, CancellationToken cancellationToken = default);
    Task DeleteNormalizationTermAsync(Guid tenantId, Guid actorUserId, Guid normalizationTermId, byte[] rowVersion, CancellationToken cancellationToken = default);
}
