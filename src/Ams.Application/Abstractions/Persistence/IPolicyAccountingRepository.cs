using Ams.Application.Common.Dtos;

namespace Ams.Application.Abstractions.Persistence;

public interface IPolicyAccountingRepository
{
    Task ProcessPolicyCreatedEventAsync(Guid eventId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<PolicyAccountingDashboardDto?> GetPolicyDashboardAsync(Guid tenantId, Guid policyId, CancellationToken cancellationToken = default);
    Task<Guid> RemitCarrierPayableAsync(Guid carrierPayableId, RemitCarrierPayableRequest request, CancellationToken cancellationToken = default);
    Task<InvoiceDeliveryDispatchDto> EmailInvoiceAsync(Guid policyId, Guid invoiceId, EmailPolicyInvoiceRequest request, CancellationToken cancellationToken = default);
}
