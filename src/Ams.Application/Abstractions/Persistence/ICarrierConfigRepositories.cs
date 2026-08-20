using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.CarrierConfig;

namespace Ams.Application.Abstractions.Persistence;

public interface IMgaWholesalerRepository
{
    Task<MgaWholesalerDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<MgaWholesalerDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateMgaWholesalerRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateMgaWholesalerRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ICarrierSettingRepository
{
    Task<CarrierSettingDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<CarrierSettingDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 100, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateCarrierSettingRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateCarrierSettingRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ICarrierContactRepository
{
    Task<CarrierContactDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<CarrierContactDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<IReadOnlyList<CarrierContactDto>> GetActiveByCarrierAsync(Guid tenantId, Guid carrierId, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateCarrierContactRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateCarrierContactRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ICarrierAppointmentRepository
{
    Task<CarrierAppointmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<CarrierAppointmentDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateCarrierAppointmentRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateCarrierAppointmentRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ICarrierPerformanceRepository
{
    Task<CarrierPerformanceDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<CarrierPerformanceDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateCarrierPerformanceRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateCarrierPerformanceRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
