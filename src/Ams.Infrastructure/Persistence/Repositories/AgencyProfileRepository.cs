using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Agency;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AgencyProfileRepository : IAgencyProfileRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public AgencyProfileRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<AgencyProfileDto?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT t.TenantId,
       t.TenantName  AS AgencyName,
       t.TenantCode  AS AgencyCode,
       t.Locale,
       t.CurrencyCode,
       t.TimeZoneId,
       p.DbaName,
       p.Npn,
       p.Fein,
       p.EntityType,
       p.LicenseNumber,
       p.DomicileState,
       p.Phone,
       p.Email,
       p.Website,
       p.AddressLine1,
       p.AddressLine2,
       p.City,
       p.StateProvince,
       p.PostalCode,
       p.CountryCode,
       p.EoCarrier,
       p.EoPolicyNumber,
       p.EoCoverageLimit,
       p.EoExpiryDate
FROM   Core.Tenant t
LEFT JOIN Agency.AgencyProfile p ON p.TenantId = t.TenantId
WHERE  t.TenantId = @TenantId AND t.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<AgencyProfileDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(Guid tenantId, UpdateAgencyProfileRequest request, CancellationToken cancellationToken = default)
    {
        const string upsertSql = @"
UPDATE Core.Tenant
SET    TenantName      = @AgencyName,
       Locale          = @Locale,
       CurrencyCode    = @CurrencyCode,
       TimeZoneId      = @TimeZoneId
WHERE  TenantId = @TenantId AND IsDeleted = 0;

MERGE Agency.AgencyProfile AS target
USING (SELECT @TenantId AS TenantId) AS src ON target.TenantId = src.TenantId
WHEN MATCHED THEN
    UPDATE SET
        DbaName         = @DbaName,
        Npn             = @Npn,
        Fein            = @Fein,
        EntityType      = @EntityType,
        LicenseNumber   = @LicenseNumber,
        DomicileState   = @DomicileState,
        Phone           = @Phone,
        Email           = @Email,
        Website         = @Website,
        AddressLine1    = @AddressLine1,
        AddressLine2    = @AddressLine2,
        City            = @City,
        StateProvince   = @StateProvince,
        PostalCode      = @PostalCode,
        CountryCode     = @CountryCode,
        EoCarrier       = @EoCarrier,
        EoPolicyNumber  = @EoPolicyNumber,
        EoCoverageLimit = @EoCoverageLimit,
        EoExpiryDate    = @EoExpiryDate,
        ModifiedDateUtc = GETUTCDATE()
WHEN NOT MATCHED THEN
    INSERT (TenantId, DbaName, Npn, Fein, EntityType, LicenseNumber, DomicileState,
            Phone, Email, Website, AddressLine1, AddressLine2, City, StateProvince,
            PostalCode, CountryCode, EoCarrier, EoPolicyNumber, EoCoverageLimit, EoExpiryDate, CreatedDateUtc)
    VALUES (@TenantId, @DbaName, @Npn, @Fein, @EntityType, @LicenseNumber, @DomicileState,
            @Phone, @Email, @Website, @AddressLine1, @AddressLine2, @City, @StateProvince,
            @PostalCode, @CountryCode, @EoCarrier, @EoPolicyNumber, @EoCoverageLimit, @EoExpiryDate, GETUTCDATE());";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(upsertSql, new
        {
            TenantId         = tenantId,
            AgencyName       = request.AgencyName,
            request.Locale,
            request.CurrencyCode,
            request.TimeZoneId,
            request.DbaName,
            request.Npn,
            request.Fein,
            request.EntityType,
            request.LicenseNumber,
            request.DomicileState,
            request.Phone,
            request.Email,
            request.Website,
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.StateProvince,
            request.PostalCode,
            request.CountryCode,
            request.EoCarrier,
            request.EoPolicyNumber,
            request.EoCoverageLimit,
            request.EoExpiryDate,
        }, cancellationToken: cancellationToken));
    }
}
