using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.Locations;
using Dapper;
using Microsoft.Extensions.Caching.Memory;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AddressLocationRepository : IAddressLocationRepository
{
    private static readonly TimeSpan ConfigurationCacheDuration = TimeSpan.FromMinutes(5);

    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly IMemoryCache _cache;

    public AddressLocationRepository(ISqlConnectionFactory connectionFactory, IMemoryCache cache)
    {
        _connectionFactory = connectionFactory;
        _cache = cache;
    }

    private static string ConfigurationCacheKey(Guid tenantId) => $"address-provider-configuration:{tenantId:N}";

    public async Task<AddressProviderConfigurationDto?> GetDefaultProviderAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(ConfigurationCacheKey(tenantId), out AddressProviderConfigurationDto? cached) && cached is not null)
        {
            return cached;
        }

        const string sql = """
            SELECT AddressProviderConfigurationId, TenantId, ProviderCode, DisplayName,
                   ServiceEndpoint, AutocompletePath, GeocodePath, ApiVersion, AuthenticationScope,
                   MapsClientId, DefaultCountrySet, DefaultLanguage, MinimumQueryLength,
                   DebounceMilliseconds, MaximumSuggestions, RequestTimeoutSeconds, IsDefault, IsEnabled
            FROM Location.AddressProviderConfiguration
            WHERE TenantId = @TenantId AND IsDefault = 1 AND IsDeleted = 0;
            """;

        const string ensureAndSelectSql = """
            EXEC Location.EnsureAddressProviderConfiguration @TenantId;

            """ + sql;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var configuration = await connection.QuerySingleOrDefaultAsync<AddressProviderConfigurationDto>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        if (configuration is null)
        {
            configuration = await connection.QuerySingleOrDefaultAsync<AddressProviderConfigurationDto>(
                new CommandDefinition(ensureAndSelectSql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        }

        if (configuration is not null)
        {
            _cache.Set(ConfigurationCacheKey(tenantId), configuration, ConfigurationCacheDuration);
        }

        return configuration;
    }

    public async Task UpdateProviderAsync(UpdateAddressProviderConfigurationRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
            EXEC Location.EnsureAddressProviderConfiguration @TenantId, @ModifiedByUserId;

            UPDATE Location.AddressProviderConfiguration
            SET MapsClientId = NULLIF(LTRIM(RTRIM(@MapsClientId)), N''),
                DefaultCountrySet = NULLIF(LTRIM(RTRIM(@DefaultCountrySet)), N''),
                DefaultLanguage = NULLIF(LTRIM(RTRIM(@DefaultLanguage)), N''),
                MinimumQueryLength = @MinimumQueryLength,
                DebounceMilliseconds = @DebounceMilliseconds,
                MaximumSuggestions = @MaximumSuggestions,
                RequestTimeoutSeconds = @RequestTimeoutSeconds,
                IsEnabled = @IsEnabled,
                ModifiedDateUtc = SYSUTCDATETIME(),
                ModifiedByUserId = @ModifiedByUserId
            WHERE TenantId = @TenantId
              AND ProviderCode = @ProviderCode
              AND IsDeleted = 0;

            IF @@ROWCOUNT = 0
                THROW 51000, 'The address provider configuration was not found.', 1;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
        _cache.Remove(ConfigurationCacheKey(request.TenantId));
    }

    public async Task<Guid> UpsertResolutionAsync(PersistAddressResolutionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
            DECLARE @AddressResolutionId UNIQUEIDENTIFIER;

            SELECT @AddressResolutionId = AddressResolutionId
            FROM Location.AddressResolution WITH (UPDLOCK, HOLDLOCK)
            WHERE TenantId = @TenantId
              AND EntityTypeCode = @EntityTypeCode
              AND EntityId = @EntityId
              AND AddressFieldCode = @AddressFieldCode
              AND IsDeleted = 0;

            IF @AddressResolutionId IS NULL
            BEGIN
                SET @AddressResolutionId = NEWID();
                INSERT Location.AddressResolution
                (
                    AddressResolutionId, TenantId, EntityTypeCode, EntityId, AddressFieldCode,
                    ProviderCode, ProviderPlaceId, QueryText, FormattedAddress, AddressLine1,
                    AddressLine2, City, StateCode, PostalCode, CountryCode, County,
                    Latitude, Longitude, ResolutionStatusCode, ConfidenceCode,
                    IsProviderValidated, ResolvedDateUtc, CreatedByUserId, IsDeleted
                )
                VALUES
                (
                    @AddressResolutionId, @TenantId, @EntityTypeCode, @EntityId, @AddressFieldCode,
                    @ProviderCode, @ProviderPlaceId, @QueryText, @FormattedAddress, @AddressLine1,
                    @AddressLine2, @City, @StateCode, @PostalCode, @CountryCode, @County,
                    @Latitude, @Longitude, @ResolutionStatusCode, @ConfidenceCode,
                    @IsProviderValidated, @ResolvedDateUtc, @UserId, 0
                );
            END
            ELSE
            BEGIN
                UPDATE Location.AddressResolution
                SET ProviderCode = @ProviderCode,
                    ProviderPlaceId = @ProviderPlaceId,
                    QueryText = @QueryText,
                    FormattedAddress = @FormattedAddress,
                    AddressLine1 = @AddressLine1,
                    AddressLine2 = @AddressLine2,
                    City = @City,
                    StateCode = @StateCode,
                    PostalCode = @PostalCode,
                    CountryCode = @CountryCode,
                    County = @County,
                    Latitude = @Latitude,
                    Longitude = @Longitude,
                    ResolutionStatusCode = @ResolutionStatusCode,
                    ConfidenceCode = @ConfidenceCode,
                    IsProviderValidated = @IsProviderValidated,
                    ResolvedDateUtc = @ResolvedDateUtc,
                    ModifiedDateUtc = SYSUTCDATETIME(),
                    ModifiedByUserId = @UserId
                WHERE AddressResolutionId = @AddressResolutionId;
            END;

            SELECT @AddressResolutionId;
            """;

        var address = request.Address;
        var parameters = new
        {
            request.TenantId,
            request.EntityTypeCode,
            request.EntityId,
            request.AddressFieldCode,
            address.ProviderCode,
            address.ProviderPlaceId,
            address.QueryText,
            address.FormattedAddress,
            address.AddressLine1,
            address.AddressLine2,
            address.City,
            address.StateCode,
            address.PostalCode,
            address.CountryCode,
            address.County,
            address.Latitude,
            address.Longitude,
            ResolutionStatusCode = address.IsProviderValidated ? "ProviderValidated" : "Manual",
            address.ConfidenceCode,
            address.IsProviderValidated,
            ResolvedDateUtc = address.IsProviderValidated ? DateTime.UtcNow : (DateTime?)null,
            request.UserId
        };

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        try
        {
            var id = await connection.ExecuteScalarAsync<Guid>(
                new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));

            if (address.IsProviderValidated
                && !string.IsNullOrWhiteSpace(address.City)
                && !string.IsNullOrWhiteSpace(address.StateCode))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "Location.LearnGeoResolution",
                    new
                    {
                        CountryCode = address.CountryCode ?? "US",
                        address.StateCode,
                        CityName = address.City,
                        address.County,
                        address.PostalCode,
                        address.Latitude,
                        address.Longitude,
                        request.UserId
                    },
                    transaction,
                    commandType: System.Data.CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));
            }

            transaction.Commit();
            return id;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<IReadOnlyList<GeoStateDto>> GetStatesAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT CountryCode, StateCode, StateName
            FROM Location.GeoState
            WHERE CountryCode = @CountryCode AND IsActive = 1 AND IsDeleted = 0
            ORDER BY DisplayOrder, StateName;
            """;

        var cacheKey = $"geo-states:{countryCode.ToUpperInvariant()}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<GeoStateDto>? cached) && cached is not null) return cached;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var states = (await connection.QueryAsync<GeoStateDto>(
            new CommandDefinition(sql, new { CountryCode = countryCode }, cancellationToken: cancellationToken))).AsList();

        _cache.Set(cacheKey, (IReadOnlyList<GeoStateDto>)states, TimeSpan.FromHours(6));
        return states;
    }

    public async Task<IReadOnlyList<GeoCityDto>> SearchCitiesAsync(string countryCode, string query, int limit, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TOP (@Limit) GeoCityId, CountryCode, StateCode, CityName, County
            FROM Location.GeoCity
            WHERE CountryCode = @CountryCode AND CityName LIKE @Query + N'%' AND IsActive = 1 AND IsDeleted = 0
            ORDER BY CityName, StateCode;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<GeoCityDto>(
            new CommandDefinition(sql, new { CountryCode = countryCode, Query = query, Limit = limit }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<GeoCityDto>> GetCityFuzzyCandidatesAsync(string countryCode, string query, int maxCandidates, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TOP (@MaxCandidates) GeoCityId, CountryCode, StateCode, CityName, County
            FROM Location.GeoCity
            WHERE CountryCode = @CountryCode
              AND IsActive = 1 AND IsDeleted = 0
              AND (CityName LIKE N'%' + @Query + N'%' OR SOUNDEX(CityName) = SOUNDEX(@Query))
            ORDER BY CityName, StateCode;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<GeoCityDto>(
            new CommandDefinition(sql, new { CountryCode = countryCode, Query = query, MaxCandidates = maxCandidates }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<GeoPostalCodeDto>> GetPostalCodesAsync(string countryCode, string stateCode, string cityName, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT postal.PostalCode, postal.Latitude, postal.Longitude
            FROM Location.GeoPostalCode postal
            INNER JOIN Location.GeoCity city ON city.GeoCityId = postal.GeoCityId
            WHERE city.CountryCode = @CountryCode
              AND city.StateCode = @StateCode
              AND city.CityName = @CityName
              AND city.IsDeleted = 0
              AND postal.IsActive = 1
              AND postal.IsDeleted = 0
            ORDER BY postal.PostalCode;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<GeoPostalCodeDto>(
            new CommandDefinition(sql, new { CountryCode = countryCode, StateCode = stateCode, CityName = cityName }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task LearnGeoAsync(string countryCode, string stateCode, string cityName, string? county, string? postalCode, decimal? latitude, decimal? longitude, Guid? userId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "Location.LearnGeoResolution",
            new
            {
                CountryCode = countryCode,
                StateCode = stateCode,
                CityName = cityName,
                County = county,
                PostalCode = postalCode,
                Latitude = latitude,
                Longitude = longitude,
                UserId = userId
            },
            commandType: System.Data.CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }
}
