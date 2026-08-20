using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyCertificates;

namespace Ams.Application;

public sealed class CertificateWorkflowService : ICertificateWorkflowService
{
    private readonly ICertificateWorkflowRepository _repository;

    public CertificateWorkflowService(ICertificateWorkflowRepository repository) => _repository = repository;

    public Task<CertificateWorkflowWorkspaceDto> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => tenantId == Guid.Empty ? throw new ArgumentException("Tenant is required.", nameof(tenantId)) : _repository.GetWorkspaceAsync(tenantId, cancellationToken);

    public Task<IReadOnlyList<CertificateAuditEventDto>> GetAuditAsync(Guid tenantId, Guid certificateId, CancellationToken cancellationToken = default)
        => RequireTenantAndCertificate(tenantId, certificateId, () => _repository.GetAuditAsync(tenantId, certificateId, cancellationToken));

    public Task<IReadOnlyList<CertificateDeliveryDto>> GetDeliveriesAsync(Guid tenantId, Guid certificateId, CancellationToken cancellationToken = default)
        => RequireTenantAndCertificate(tenantId, certificateId, () => _repository.GetDeliveriesAsync(tenantId, certificateId, cancellationToken));

    public Task<Guid?> GetLatestGeneratedDocumentVersionIdAsync(Guid tenantId, Guid certificateId, CancellationToken cancellationToken = default)
        => RequireTenantAndCertificate(tenantId, certificateId, () => _repository.GetLatestGeneratedDocumentVersionIdAsync(tenantId, certificateId, cancellationToken));

    public Task<Guid> UpsertHolderAsync(UpsertCertificateHolderRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.HolderCode) || string.IsNullOrWhiteSpace(request.LegalName)) throw new ArgumentException("Holder code and legal name are required.", nameof(request));
        return _repository.UpsertHolderAsync(request, cancellationToken);
    }

    public Task<Guid> CreateTemplateVersionAsync(CreateDocumentTemplateVersionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty || request.DocumentTemplateDefinitionId == Guid.Empty) throw new ArgumentException("Tenant and template definition are required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ContentFormatCode) || string.IsNullOrWhiteSpace(request.MergeFieldSchemaJson)) throw new ArgumentException("Content format and merge field schema are required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.TemplateContent) && string.IsNullOrWhiteSpace(request.StoragePath)) throw new ArgumentException("Template content or a managed storage path is required.", nameof(request));
        return _repository.CreateTemplateVersionAsync(request, cancellationToken);
    }

    public Task<Guid> CreateRequestAsync(CreateCertificateWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty || request.CertificateHolderId == Guid.Empty) throw new ArgumentException("Tenant and certificate holder are required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.RequestedDocumentTypeCode) || string.IsNullOrWhiteSpace(request.SourceCode) || string.IsNullOrWhiteSpace(request.PriorityCode)) throw new ArgumentException("Document type, source, and priority are required.", nameof(request));
        if (request.NeededByDateUtc is { } neededBy && neededBy < DateTime.UtcNow.AddMinutes(-5)) throw new ArgumentException("Needed-by date cannot be in the past.", nameof(request));
        return _repository.CreateRequestAsync(request, cancellationToken);
    }

    public Task<CertificateGenerationResultDto> GenerateAsync(GenerateCertificateDocumentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty || request.CertificateId == Guid.Empty || request.DocumentTemplateDefinitionId == Guid.Empty) throw new ArgumentException("Tenant, certificate, and template are required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.MergeDataJson)) throw new ArgumentException("Merge data is required.", nameof(request));
        return _repository.GenerateAsync(request, cancellationToken);
    }

    public Task<Guid> QueueDeliveryAsync(QueueCertificateDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty || request.CertificateId == Guid.Empty) throw new ArgumentException("Tenant and certificate are required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.DeliveryMethodCode) || string.IsNullOrWhiteSpace(request.RecipientAddress)) throw new ArgumentException("Delivery method and recipient are required.", nameof(request));
        return _repository.QueueDeliveryAsync(request, cancellationToken);
    }

    public Task<Guid> UpsertRenewalScheduleAsync(UpsertCertificateRenewalScheduleRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty || request.CertificateId == Guid.Empty) throw new ArgumentException("Tenant and certificate are required.", nameof(request));
        if (request.RenewalLeadDays is < 1 or > 365) throw new ArgumentOutOfRangeException(nameof(request), "Renewal lead days must be between 1 and 365.");
        return _repository.UpsertRenewalScheduleAsync(request, cancellationToken);
    }

    public Task<int> ProcessDueRenewalsAsync(int batchSize, CancellationToken cancellationToken = default)
        => _repository.ProcessDueRenewalsAsync(Math.Clamp(batchSize, 1, 250), cancellationToken);

    private static Task<T> RequireTenantAndCertificate<T>(Guid tenantId, Guid certificateId, Func<Task<T>> action)
    {
        if (tenantId == Guid.Empty || certificateId == Guid.Empty) throw new ArgumentException("Tenant and certificate are required.");
        return action();
    }
}
