using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Submissions;

namespace Ams.Application;

public sealed class SubmissionWorkflowConfigurationService : ISubmissionWorkflowConfigurationService
{
    private readonly ISubmissionWorkflowConfigurationRepository _repository;

    public SubmissionWorkflowConfigurationService(ISubmissionWorkflowConfigurationRepository repository)
    {
        _repository = repository;
    }

    public Task<SubmissionWorkflowConfigurationSummaryDto> GetSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetSummaryAsync(tenantId, cancellationToken);

    public Task<IReadOnlyList<SubmissionIntakeTemplateDto>> GetIntakeTemplatesAsync(Guid tenantId, string? lineOfBusiness = null, CancellationToken cancellationToken = default)
        => _repository.GetIntakeTemplatesAsync(tenantId, lineOfBusiness, cancellationToken);

    public Task<Guid> UpsertIntakeTemplateAsync(Guid? intakeTemplateId, UpsertSubmissionIntakeTemplateRequest request, CancellationToken cancellationToken = default)
        => _repository.UpsertIntakeTemplateAsync(intakeTemplateId, request, cancellationToken);

    public Task DeleteIntakeTemplateAsync(Guid intakeTemplateId, Guid tenantId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
        => _repository.DeleteIntakeTemplateAsync(intakeTemplateId, tenantId, modifiedByUserId, cancellationToken);

    public Task<IReadOnlyList<SubmissionDocumentRequirementDto>> GetDocumentRequirementsAsync(Guid tenantId, string? lineOfBusiness = null, CancellationToken cancellationToken = default)
        => _repository.GetDocumentRequirementsAsync(tenantId, lineOfBusiness, cancellationToken);

    public Task<Guid> UpsertDocumentRequirementAsync(Guid? documentRequirementId, UpsertSubmissionDocumentRequirementRequest request, CancellationToken cancellationToken = default)
        => _repository.UpsertDocumentRequirementAsync(documentRequirementId, request, cancellationToken);

    public Task DeleteDocumentRequirementAsync(Guid documentRequirementId, Guid tenantId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
        => _repository.DeleteDocumentRequirementAsync(documentRequirementId, tenantId, modifiedByUserId, cancellationToken);
}
