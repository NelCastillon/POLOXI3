using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Leads;

namespace Ams.Application;

public sealed class LeadService : ILeadService
{
    private readonly ILeadRepository _repository;

    public LeadService(ILeadRepository repository)
    {
        _repository = repository;
    }

    public Task<Guid> CreateAsync(CreateLeadRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task<LeadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<LeadConversionResultDto> ConvertAsync(ConvertLeadRequest request, CancellationToken cancellationToken = default)
        => _repository.ConvertAsync(request, cancellationToken);

    public Task<IReadOnlyList<LeadScoreFactorDto>> GetScoreFactorsAsync(Guid leadId, CancellationToken cancellationToken = default)
        => _repository.GetScoreFactorsAsync(leadId, cancellationToken);

    public Task<LeadEngagementSummaryDto?> GetEngagementSummaryAsync(Guid leadId, CancellationToken cancellationToken = default)
        => _repository.GetEngagementSummaryAsync(leadId, cancellationToken);

    public Task<IReadOnlyList<LeadEngagementOptionDto>> GetEngagementOptionsAsync(Guid tenantId, string? optionType = null, CancellationToken cancellationToken = default)
        => _repository.GetEngagementOptionsAsync(tenantId, optionType, cancellationToken);

    public Task<IReadOnlyList<LeadCampaignOptionDto>> GetCampaignOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetCampaignOptionsAsync(tenantId, cancellationToken);

    public Task<PagedResult<LeadDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task UpdateAsync(UpdateLeadRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(request, cancellationToken);

    public Task<IReadOnlyList<LeadContactDto>> GetContactsAsync(Guid leadId, CancellationToken cancellationToken = default)
        => _repository.GetContactsAsync(leadId, cancellationToken);

    public Task<Guid> CreateContactAsync(CreateLeadContactRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateContactAsync(request, cancellationToken);

    public Task UpdateContactAsync(UpdateLeadContactRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateContactAsync(request, cancellationToken);

    public Task DeleteContactAsync(Guid contactId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
        => _repository.DeleteContactAsync(contactId, modifiedByUserId, cancellationToken);

    public Task<IReadOnlyList<LeadInterestLineDto>> GetInterestLinesAsync(Guid leadId, CancellationToken cancellationToken = default)
        => _repository.GetInterestLinesAsync(leadId, cancellationToken);

    public Task<Guid> CreateInterestLineAsync(CreateLeadInterestLineRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateInterestLineAsync(request, cancellationToken);

    public Task UpdateInterestLineAsync(UpdateLeadInterestLineRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateInterestLineAsync(request, cancellationToken);

    public Task DeleteInterestLineAsync(Guid interestLineId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
        => _repository.DeleteInterestLineAsync(interestLineId, modifiedByUserId, cancellationToken);

    public Task<IReadOnlyList<LeadCommunicationDto>> GetCommunicationsAsync(Guid leadId, CancellationToken cancellationToken = default)
        => _repository.GetCommunicationsAsync(leadId, cancellationToken);

    public Task<Guid> CreateCommunicationAsync(CreateLeadCommunicationRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateCommunicationAsync(request, cancellationToken);

    public Task UpdateCommunicationAsync(UpdateLeadCommunicationRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateCommunicationAsync(request, cancellationToken);

    public Task DeleteCommunicationAsync(Guid communicationId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
        => _repository.DeleteCommunicationAsync(communicationId, modifiedByUserId, cancellationToken);

    public Task<IReadOnlyList<LeadCampaignEnrollmentDto>> GetCampaignEnrollmentsAsync(Guid leadId, CancellationToken cancellationToken = default)
        => _repository.GetCampaignEnrollmentsAsync(leadId, cancellationToken);

    public Task<Guid> CreateCampaignEnrollmentAsync(CreateLeadCampaignEnrollmentRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateCampaignEnrollmentAsync(request, cancellationToken);

    public Task UpdateCampaignEnrollmentAsync(UpdateLeadCampaignEnrollmentRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateCampaignEnrollmentAsync(request, cancellationToken);

    public Task DeleteCampaignEnrollmentAsync(Guid enrollmentId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
        => _repository.DeleteCampaignEnrollmentAsync(enrollmentId, modifiedByUserId, cancellationToken);

    public Task<IReadOnlyList<LeadDocumentDto>> GetDocumentsAsync(Guid leadId, CancellationToken cancellationToken = default)
        => _repository.GetDocumentsAsync(leadId, cancellationToken);

    public Task<Guid> CreateDocumentAsync(CreateLeadDocumentRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateDocumentAsync(request, cancellationToken);

    public Task UpdateDocumentAsync(UpdateLeadDocumentRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateDocumentAsync(request, cancellationToken);

    public Task DeleteDocumentAsync(Guid documentId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
        => _repository.DeleteDocumentAsync(documentId, modifiedByUserId, cancellationToken);

    public Task<IReadOnlyList<LeadScoringRuleDto>> GetScoringRulesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetScoringRulesAsync(tenantId, cancellationToken);

    public Task<Guid?> GetScoringRuleTenantIdAsync(Guid scoringRuleId, CancellationToken cancellationToken = default)
        => _repository.GetScoringRuleTenantIdAsync(scoringRuleId, cancellationToken);

    public Task<Guid> CreateScoringRuleAsync(CreateLeadScoringRuleRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateScoringRuleAsync(request, cancellationToken);

    public Task UpdateScoringRuleAsync(UpdateLeadScoringRuleRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateScoringRuleAsync(request, cancellationToken);

    public Task DeleteScoringRuleAsync(Guid scoringRuleId, CancellationToken cancellationToken = default)
        => _repository.DeleteScoringRuleAsync(scoringRuleId, cancellationToken);

    public Task<IReadOnlyList<LeadEngagementFactorDto>> GetEngagementFactorsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetEngagementFactorsAsync(tenantId, cancellationToken);

    public Task<Guid> CreateEngagementFactorAsync(CreateLeadEngagementFactorRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateEngagementFactorAsync(request, cancellationToken);

    public Task UpdateEngagementFactorAsync(UpdateLeadEngagementFactorRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateEngagementFactorAsync(request, cancellationToken);

    public Task DeleteEngagementFactorAsync(Guid engagementFactorId, CancellationToken cancellationToken = default)
        => _repository.DeleteEngagementFactorAsync(engagementFactorId, cancellationToken);
}
