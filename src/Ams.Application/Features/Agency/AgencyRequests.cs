namespace Ams.Application.Features.Agency;

public sealed record CreateBranchRequest(
    Guid    TenantId,
    string  BranchCode,
    string  BranchName,
    string? City,
    string? StateProvince,
    string? CountryCode);

public sealed record UpdateBranchRequest(
    string  BranchCode,
    string  BranchName,
    string? City,
    string? StateProvince,
    string? CountryCode,
    bool    IsActive);

public sealed record UpdateAgencyProfileRequest(
    string   AgencyName,
    string?  DbaName,
    string?  Npn,
    string?  Fein,
    string?  EntityType,
    string?  LicenseNumber,
    string?  DomicileState,
    string?  Phone,
    string?  Email,
    string?  Website,
    string?  AddressLine1,
    string?  AddressLine2,
    string?  City,
    string?  StateProvince,
    string?  PostalCode,
    string?  CountryCode,
    string?  EoCarrier,
    string?  EoPolicyNumber,
    decimal? EoCoverageLimit,
    DateTime? EoExpiryDate,
    string   Locale,
    string   CurrencyCode,
    string   TimeZoneId);
