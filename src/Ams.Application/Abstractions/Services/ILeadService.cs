using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Leads;

namespace Ams.Application.Abstractions.Services;

public interface ILeadService
{
    Task<Guid> CreateAsync(Ams.Application.Features.Leads.CreateLeadRequest request, CancellationToken cancellationToken = default);
    Task<LeadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LeadConversionResultDto> ConvertAsync(ConvertLeadRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeadScoreFactorDto>> GetScoreFactorsAsync(Guid leadId, CancellationToken cancellationToken = default);
    Task<LeadEngagementSummaryDto?> GetEngagementSummaryAsync(Guid leadId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeadEngagementOptionDto>> GetEngagementOptionsAsync(Guid tenantId, string? optionType = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeadCampaignOptionDto>> GetCampaignOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<PagedResult<LeadDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task UpdateAsync(UpdateLeadRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeadContactDto>> GetContactsAsync(Guid leadId, CancellationToken cancellationToken = default);
    Task<Guid> CreateContactAsync(CreateLeadContactRequest request, CancellationToken cancellationToken = default);
    Task UpdateContactAsync(UpdateLeadContactRequest request, CancellationToken cancellationToken = default);
    Task DeleteContactAsync(Guid contactId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeadInterestLineDto>> GetInterestLinesAsync(Guid leadId, CancellationToken cancellationToken = default);
    Task<Guid> CreateInterestLineAsync(CreateLeadInterestLineRequest request, CancellationToken cancellationToken = default);
    Task UpdateInterestLineAsync(UpdateLeadInterestLineRequest request, CancellationToken cancellationToken = default);
    Task DeleteInterestLineAsync(Guid interestLineId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeadCommunicationDto>> GetCommunicationsAsync(Guid leadId, CancellationToken cancellationToken = default);
    Task<Guid> CreateCommunicationAsync(CreateLeadCommunicationRequest request, CancellationToken cancellationToken = default);
    Task UpdateCommunicationAsync(UpdateLeadCommunicationRequest request, CancellationToken cancellationToken = default);
    Task DeleteCommunicationAsync(Guid communicationId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeadCampaignEnrollmentDto>> GetCampaignEnrollmentsAsync(Guid leadId, CancellationToken cancellationToken = default);
    Task<Guid> CreateCampaignEnrollmentAsync(CreateLeadCampaignEnrollmentRequest request, CancellationToken cancellationToken = default);
    Task UpdateCampaignEnrollmentAsync(UpdateLeadCampaignEnrollmentRequest request, CancellationToken cancellationToken = default);
    Task DeleteCampaignEnrollmentAsync(Guid enrollmentId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeadDocumentDto>> GetDocumentsAsync(Guid leadId, CancellationToken cancellationToken = default);
    Task<Guid> CreateDocumentAsync(CreateLeadDocumentRequest request, CancellationToken cancellationToken = default);
    Task UpdateDocumentAsync(UpdateLeadDocumentRequest request, CancellationToken cancellationToken = default);
    Task DeleteDocumentAsync(Guid documentId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeadScoringRuleDto>> GetScoringRulesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Guid?> GetScoringRuleTenantIdAsync(Guid scoringRuleId, CancellationToken cancellationToken = default);
    Task<Guid> CreateScoringRuleAsync(CreateLeadScoringRuleRequest request, CancellationToken cancellationToken = default);
    Task UpdateScoringRuleAsync(UpdateLeadScoringRuleRequest request, CancellationToken cancellationToken = default);
    Task DeleteScoringRuleAsync(Guid scoringRuleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeadEngagementFactorDto>> GetEngagementFactorsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Guid> CreateEngagementFactorAsync(CreateLeadEngagementFactorRequest request, CancellationToken cancellationToken = default);
    Task UpdateEngagementFactorAsync(UpdateLeadEngagementFactorRequest request, CancellationToken cancellationToken = default);
    Task DeleteEngagementFactorAsync(Guid engagementFactorId, CancellationToken cancellationToken = default);
}
