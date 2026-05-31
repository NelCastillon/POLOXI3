using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Guards;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;

namespace Ams.Application;

public sealed class ApPaymentService : IApPaymentService
{
    private readonly IApPaymentRepository _repository;
    private readonly IVendorRepository _vendorRepository;
    private readonly IApInvoiceRepository _apInvoiceRepository;

    public ApPaymentService(IApPaymentRepository repository, IVendorRepository vendorRepository, IApInvoiceRepository apInvoiceRepository)
    {
        _repository = repository;
        _vendorRepository = vendorRepository;
        _apInvoiceRepository = apInvoiceRepository;
    }

    public Task<ApPaymentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<ApPaymentDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public async Task<Guid> CreateAsync(CreateApPaymentRequest request, CancellationToken cancellationToken = default)
    {
        // Enterprise rule: an AP Payment must never be orphaned. It requires a parent Vendor
        // within the same tenant.
        await TenantGuard.EnsureParentAsync(request.VendorId, request.TenantId, _vendorRepository.GetByIdAsync, v => v.TenantId, "Vendor", "AP payment", cancellationToken);

        // When an AP payment is applied to an AP invoice, the invoice must exist, share the tenant,
        // and belong to the same vendor.
        var invoice = await TenantGuard.EnsureOptionalParentAsync(request.ApInvoiceId, request.TenantId, _apInvoiceRepository.GetByIdAsync, i => i.TenantId, "AP invoice", "AP payment", cancellationToken);
        if (invoice is not null && invoice.VendorId != request.VendorId)
        {
            throw new InvalidOperationException("AP invoice belongs to a different vendor and cannot be paid by this AP payment.");
        }

        return await _repository.CreateAsync(request, cancellationToken);
    }

    public Task UpdateAsync(Guid id, UpdateApPaymentRequest request, CancellationToken cancellationToken = default) => _repository.UpdateAsync(id, request, cancellationToken);
}
