using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;

namespace Ams.Application;

public sealed class PolicyAccountingService(IPolicyAccountingRepository repository) : IPolicyAccountingService
{
    public Task<PolicyAccountingDashboardDto?> GetPolicyDashboardAsync(Guid tenantId, Guid policyId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));
        if (policyId == Guid.Empty) throw new ArgumentException("Policy is required.", nameof(policyId));
        return repository.GetPolicyDashboardAsync(tenantId, policyId, cancellationToken);
    }

    public Task<Guid> RemitCarrierPayableAsync(Guid carrierPayableId, RemitCarrierPayableRequest request, CancellationToken cancellationToken = default)
    {
        if (carrierPayableId == Guid.Empty) throw new ArgumentException("Carrier payable is required.", nameof(carrierPayableId));
        if (request.TenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(request));
        if (request.Amount <= 0) throw new ArgumentOutOfRangeException(nameof(request), "Remittance amount must be positive.");
        return repository.RemitCarrierPayableAsync(carrierPayableId, request, cancellationToken);
    }

    public Task<InvoiceDeliveryDispatchDto> EmailInvoiceAsync(Guid policyId, Guid invoiceId, EmailPolicyInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        if (policyId == Guid.Empty) throw new ArgumentException("Policy is required.", nameof(policyId));
        if (invoiceId == Guid.Empty) throw new ArgumentException("Invoice is required.", nameof(invoiceId));
        if (request.TenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Recipient) || !System.Net.Mail.MailAddress.TryCreate(request.Recipient, out _)) throw new ArgumentException("A valid recipient email is required.", nameof(request));
        return repository.EmailInvoiceAsync(policyId, invoiceId, request, cancellationToken);
    }
}
