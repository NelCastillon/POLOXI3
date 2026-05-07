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
       p.DBA AS DbaName,
       CAST(NULL AS nvarchar(100)) AS Npn,
       p.FederalTaxId AS Fein,
       p.LegalEntityType AS EntityType,
       p.LicenseNumber,
       p.State AS DomicileState,
       p.ContactPhone AS Phone,
       p.ContactEmail AS Email,
       p.WebsiteUrl AS Website,
       p.StreetAddress AS AddressLine1,
       CAST(NULL AS nvarchar(255)) AS AddressLine2,
       p.City,
       p.State AS StateProvince,
       p.ZipCode AS PostalCode,
       p.Country AS CountryCode,
       p.EoCarrier,
       p.EoPolicyNumber,
       p.EoCoverageAmount AS EoCoverageLimit,
       p.EoExpiryDate
FROM   Core.Tenant t
LEFT JOIN Agency.Profile p ON p.TenantId = t.TenantId AND p.IsDeleted = 0
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

MERGE Agency.Profile AS target
USING (SELECT @TenantId AS TenantId) AS src ON target.TenantId = src.TenantId
WHEN MATCHED THEN
    UPDATE SET
        LegalName       = @AgencyName,
        DBA             = @DbaName,
        LegalEntityType = @EntityType,
        FederalTaxId    = @Fein,
        LicenseNumber   = @LicenseNumber,
        ContactFirstName = COALESCE(NULLIF(ContactFirstName, ''), 'Agency'),
        ContactLastName  = COALESCE(NULLIF(ContactLastName, ''), 'Contact'),
        ContactPhone    = @Phone,
        ContactEmail    = COALESCE(NULLIF(@Email, ''), ContactEmail, 'agency@example.com'),
        WebsiteUrl      = @Website,
        StreetAddress   = COALESCE(NULLIF(@AddressLine1, ''), StreetAddress, 'N/A'),
        City            = COALESCE(NULLIF(@City, ''), City, 'N/A'),
        State           = COALESCE(NULLIF(@StateProvince, ''), State, 'N/A'),
        ZipCode         = COALESCE(NULLIF(@PostalCode, ''), ZipCode, 'N/A'),
        Country         = COALESCE(NULLIF(@CountryCode, ''), Country, 'United States'),
        EoCarrier       = @EoCarrier,
        EoPolicyNumber  = @EoPolicyNumber,
        EoCoverageAmount = @EoCoverageLimit,
        EoExpiryDate    = @EoExpiryDate,
        ModifiedDateUtc = GETUTCDATE()
WHEN NOT MATCHED THEN
    INSERT (TenantId, LegalName, DBA, LegalEntityType, FederalTaxId, LicenseNumber,
            ContactFirstName, ContactLastName, ContactEmail, ContactPhone,
            StreetAddress, City, State, ZipCode, Country, WebsiteUrl,
            EoCarrier, EoPolicyNumber, EoCoverageAmount, EoExpiryDate, CreatedDateUtc)
    VALUES (@TenantId, @AgencyName, @DbaName, @EntityType, @Fein, @LicenseNumber,
            'Agency', 'Contact', COALESCE(NULLIF(@Email, ''), 'agency@example.com'), COALESCE(NULLIF(@Phone, ''), 'N/A'),
            COALESCE(NULLIF(@AddressLine1, ''), 'N/A'), COALESCE(NULLIF(@City, ''), 'N/A'), COALESCE(NULLIF(@StateProvince, ''), 'N/A'), COALESCE(NULLIF(@PostalCode, ''), 'N/A'), COALESCE(NULLIF(@CountryCode, ''), 'United States'), @Website,
            @EoCarrier, @EoPolicyNumber, @EoCoverageLimit, @EoExpiryDate, GETUTCDATE());";
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
