using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Guards;
using Ams.Application.Common.Models;
using Ams.Application.Features.Payments;

namespace Ams.Application;

public sealed class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _repository;
    private readonly IAccountRepository _accountRepository;
    private readonly IInvoiceRepository _invoiceRepository;

    public PaymentService(IPaymentRepository repository, IAccountRepository accountRepository, IInvoiceRepository invoiceRepository)
    {
        _repository = repository;
        _accountRepository = accountRepository;
        _invoiceRepository = invoiceRepository;
    }

    public async Task<Guid> CreateAsync(CreatePaymentRequest request, CancellationToken cancellationToken = default)
    {
        // Enterprise rule: a Payment must never be orphaned. It requires a parent Account
        // within the same tenant, and when applied to an Invoice that Invoice must also be
        // same-tenant and belong to the same Account.
        await TenantGuard.EnsureParentAsync(request.AccountId, request.TenantId, _accountRepository.GetByIdAsync, a => a.TenantId, "Account", "payment", cancellationToken);

        var invoice = await TenantGuard.EnsureOptionalParentAsync(request.InvoiceId, request.TenantId, _invoiceRepository.GetByIdAsync, i => i.TenantId, "Invoice", "payment", cancellationToken);
        if (invoice is not null && invoice.AccountId != request.AccountId)
        {
            throw new InvalidOperationException("Invoice belongs to a different account and cannot receive this payment.");
        }

        return await _repository.CreateAsync(request, cancellationToken);
    }

    public Task<PaymentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<PaymentDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
}
