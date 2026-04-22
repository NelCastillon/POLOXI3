using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Carriers;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CarrierRepository : ICarrierRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public CarrierRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private const string SelectColumns = "CarrierId, TenantId, CarrierName, NaicCode, AmBestRating, IsAdmitted, AppointmentDate, IsActive, CreatedDateUtc";

    public async Task<CarrierDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = $"SELECT {SelectColumns} FROM Agency.Carrier WHERE CarrierId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CarrierDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CarrierDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql(
            "Agency.Carrier",
            SelectColumns,
            "CarrierName LIKE '%' + @SearchTerm + '%' OR NaicCode LIKE '%' + @SearchTerm + '%'",
            "CarrierName ASC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql,
            new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize },
            cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<CarrierDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<CarrierDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateCarrierRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Agency.Carrier
    (CarrierId, TenantId, CarrierName, NaicCode, AmBestRating, IsAdmitted, AppointmentDate, IsActive, CreatedDateUtc, IsDeleted)
VALUES
    (@CarrierId, @TenantId, @CarrierName, @NaicCode, @AmBestRating, @IsAdmitted, @AppointmentDate, 1, GETUTCDATE(), 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            CarrierId       = id,
            request.TenantId,
            request.CarrierName,
            request.NaicCode,
            request.AmBestRating,
            request.IsAdmitted,
            request.AppointmentDate,
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateCarrierRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Agency.Carrier
SET    CarrierName     = @CarrierName,
       NaicCode        = @NaicCode,
       AmBestRating    = @AmBestRating,
       IsAdmitted      = @IsAdmitted,
       AppointmentDate = @AppointmentDate,
       IsActive        = @IsActive,
       ModifiedDateUtc = GETUTCDATE()
WHERE  CarrierId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            request.CarrierName,
            request.NaicCode,
            request.AmBestRating,
            request.IsAdmitted,
            request.AppointmentDate,
            request.IsActive,
        }, cancellationToken: cancellationToken));
    }
}
