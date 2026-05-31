using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Guards;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;

namespace Ams.Application;

public sealed class ApInvoiceService : IApInvoiceService
{
    private readonly IApInvoiceRepository _repository;
    private readonly IVendorRepository _vendorRepository;

    public ApInvoiceService(IApInvoiceRepository repository, IVendorRepository vendorRepository)
    {
        _repository = repository;
        _vendorRepository = vendorRepository;
    }

    public Task<ApInvoiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<ApInvoiceDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public async Task<Guid> CreateAsync(CreateApInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        // Enterprise rule: an AP Invoice must never be orphaned. It requires a parent Vendor
        // within the same tenant.
        await TenantGuard.EnsureParentAsync(request.VendorId, request.TenantId, _vendorRepository.GetByIdAsync, v => v.TenantId, "Vendor", "AP invoice", cancellationToken);

        return await _repository.CreateAsync(request, cancellationToken);
    }

    public Task UpdateAsync(Guid id, UpdateApInvoiceRequest request, CancellationToken cancellationToken = default) => _repository.UpdateAsync(id, request, cancellationToken);
}
