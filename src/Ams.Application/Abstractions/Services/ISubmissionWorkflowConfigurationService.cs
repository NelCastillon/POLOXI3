using Ams.Application.Common.Dtos;
using Ams.Application.Features.Submissions;

namespace Ams.Application.Abstractions.Services;

public interface ISubmissionWorkflowConfigurationService
{
    Task<SubmissionWorkflowConfigurationSummaryDto> GetSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubmissionIntakeTemplateDto>> GetIntakeTemplatesAsync(Guid tenantId, string? lineOfBusiness = null, CancellationToken cancellationToken = default);
    Task<Guid> UpsertIntakeTemplateAsync(Guid? intakeTemplateId, UpsertSubmissionIntakeTemplateRequest request, CancellationToken cancellationToken = default);
    Task DeleteIntakeTemplateAsync(Guid intakeTemplateId, Guid tenantId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubmissionDocumentRequirementDto>> GetDocumentRequirementsAsync(Guid tenantId, string? lineOfBusiness = null, CancellationToken cancellationToken = default);
    Task<Guid> UpsertDocumentRequirementAsync(Guid? documentRequirementId, UpsertSubmissionDocumentRequirementRequest request, CancellationToken cancellationToken = default);
    Task DeleteDocumentRequirementAsync(Guid documentRequirementId, Guid tenantId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default);
}
