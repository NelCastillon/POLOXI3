using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyCertificates;

namespace Ams.Application.Abstractions.Persistence;

public interface ICertificateWorkflowRepository
{
    Task<CertificateWorkflowWorkspaceDto> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CertificateAuditEventDto>> GetAuditAsync(Guid tenantId, Guid certificateId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CertificateDeliveryDto>> GetDeliveriesAsync(Guid tenantId, Guid certificateId, CancellationToken cancellationToken = default);
    Task<Guid?> GetLatestGeneratedDocumentVersionIdAsync(Guid tenantId, Guid certificateId, CancellationToken cancellationToken = default);
    Task<Guid> UpsertHolderAsync(UpsertCertificateHolderRequest request, CancellationToken cancellationToken = default);
    Task<Guid> CreateTemplateVersionAsync(CreateDocumentTemplateVersionRequest request, CancellationToken cancellationToken = default);
    Task<Guid> CreateRequestAsync(CreateCertificateWorkflowRequest request, CancellationToken cancellationToken = default);
    Task<CertificateGenerationResultDto> GenerateAsync(GenerateCertificateDocumentRequest request, CancellationToken cancellationToken = default);
    Task<Guid> QueueDeliveryAsync(QueueCertificateDeliveryRequest request, CancellationToken cancellationToken = default);
    Task<Guid> UpsertRenewalScheduleAsync(UpsertCertificateRenewalScheduleRequest request, CancellationToken cancellationToken = default);
    Task<int> ProcessDueRenewalsAsync(int batchSize, CancellationToken cancellationToken = default);
}