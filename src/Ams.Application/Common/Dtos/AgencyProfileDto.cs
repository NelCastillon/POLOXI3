namespace Ams.Application.Common.Dtos;

public sealed class AgencyProfileDto
{
    public Guid    TenantId        { get; set; }
    public string  AgencyName      { get; set; } = string.Empty;
    public string  AgencyCode      { get; set; } = string.Empty;
    public string? DbaName         { get; set; }
    public string? Npn             { get; set; }
    public string? Fein            { get; set; }
    public string? EntityType      { get; set; }
    public string? LicenseNumber   { get; set; }
    public string? DomicileState   { get; set; }
    public string? Phone           { get; set; }
    public string? Email           { get; set; }
    public string? Website         { get; set; }
    public string? AddressLine1    { get; set; }
    public string? AddressLine2    { get; set; }
    public string? City            { get; set; }
    public string? StateProvince   { get; set; }
    public string? PostalCode      { get; set; }
    public string? CountryCode     { get; set; }
    public string? EoCarrier       { get; set; }
    public string? EoPolicyNumber  { get; set; }
    public decimal? EoCoverageLimit { get; set; }
    public DateTime? EoExpiryDate  { get; set; }
    public string  Locale          { get; set; } = "en-US";
    public string  CurrencyCode    { get; set; } = "USD";
    public string  TimeZoneId      { get; set; } = "UTC";
}
