using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.CarrierConfig;

namespace Ams.Application;

public sealed class MgaWholesalerService : IMgaWholesalerService
{
    private readonly IMgaWholesalerRepository _repo;
    public MgaWholesalerService(IMgaWholesalerRepository repo) => _repo = repo;
    public Task<MgaWholesalerDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<MgaWholesalerDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateMgaWholesalerRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateMgaWholesalerRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}

public sealed class CarrierContactService : ICarrierContactService
{
    private readonly ICarrierContactRepository _repo;
    public CarrierContactService(ICarrierContactRepository repo) => _repo = repo;
    public Task<CarrierContactDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<CarrierContactDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateCarrierContactRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateCarrierContactRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}

public sealed class CarrierAppointmentService : ICarrierAppointmentService
{
    private readonly ICarrierAppointmentRepository _repo;
    public CarrierAppointmentService(ICarrierAppointmentRepository repo) => _repo = repo;
    public Task<CarrierAppointmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<CarrierAppointmentDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateCarrierAppointmentRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateCarrierAppointmentRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}

public sealed class CarrierPerformanceService : ICarrierPerformanceService
{
    private readonly ICarrierPerformanceRepository _repo;
    public CarrierPerformanceService(ICarrierPerformanceRepository repo) => _repo = repo;
    public Task<CarrierPerformanceDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<CarrierPerformanceDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateCarrierPerformanceRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateCarrierPerformanceRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}
