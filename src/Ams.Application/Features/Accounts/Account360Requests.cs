using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Accounts;

public sealed class UpsertAccountNamedInsuredRequest
{
    public Guid? AccountNamedInsuredId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? ContactId { get; set; }
    [Required, StringLength(50)] public string InsuredTypeCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string LegalName { get; set; } = string.Empty;
    [StringLength(200)] public string? DbaName { get; set; }
    [StringLength(50)] public string? TaxIdentifier { get; set; }
    [Required, StringLength(50)] public string RelationshipCode { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    public Guid? UserId { get; set; }
}

public sealed class UpsertAccountLocationRequest
{
    public Guid? AccountLocationId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    [Required, StringLength(50)] public string LocationNumber { get; set; } = string.Empty;
    [Required, StringLength(50)] public string LocationTypeCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string LocationName { get; set; } = string.Empty;
    [Required, StringLength(200)] public string AddressLine1 { get; set; } = string.Empty;
    [StringLength(200)] public string? AddressLine2 { get; set; }
    [Required, StringLength(100)] public string City { get; set; } = string.Empty;
    [Required, StringLength(50)] public string StateCode { get; set; } = string.Empty;
    [Required, StringLength(20)] public string PostalCode { get; set; } = string.Empty;
    [Required, StringLength(10)] public string CountryCode { get; set; } = "US";
    [StringLength(100)] public string? County { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsMailingAddress { get; set; }
    [Range(-90, 90)] public decimal? Latitude { get; set; }
    [Range(-180, 180)] public decimal? Longitude { get; set; }
    [StringLength(80)] public string? OccupancyCode { get; set; }
    [Range(0, 999999999999)] public decimal? AnnualRevenue { get; set; }
    [Range(0, 10000000)] public int? EmployeeCount { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    public Guid? UserId { get; set; }
}

public sealed class UpsertAccountVehicleRequest
{
    public Guid? AccountVehicleId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? AccountLocationId { get; set; }
    [Required, StringLength(50)] public string VehicleNumber { get; set; } = string.Empty;
    [Required, StringLength(17, MinimumLength = 17)] public string Vin { get; set; } = string.Empty;
    [Range(1886, 2200)] public int ModelYear { get; set; }
    [Required, StringLength(80)] public string Make { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Model { get; set; } = string.Empty;
    [Required, StringLength(50)] public string VehicleTypeCode { get; set; } = string.Empty;
    [Required, StringLength(50)] public string UseTypeCode { get; set; } = string.Empty;
    [StringLength(50)] public string? GaragingStateCode { get; set; }
    [StringLength(20)] public string? GaragingPostalCode { get; set; }
    [Range(0, 100000)] public int? RadiusMiles { get; set; }
    [Range(0, 1000000)] public int? AnnualMileage { get; set; }
    [Range(0, 999999999)] public decimal? CostNew { get; set; }
    [Range(0, 999999999)] public decimal? StatedValue { get; set; }
    public bool IsActive { get; set; } = true;
    [StringLength(1000)] public string? Notes { get; set; }
    public Guid? UserId { get; set; }
}

public sealed class UpsertAccountDriverRequest
{
    public Guid? AccountDriverId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? ContactId { get; set; }
    [Required, StringLength(50)] public string DriverNumber { get; set; } = string.Empty;
    [Required, StringLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)] public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    [Required, StringLength(80)] public string LicenseNumber { get; set; } = string.Empty;
    [Required, StringLength(50)] public string LicenseStateCode { get; set; } = string.Empty;
    [StringLength(50)] public string? LicenseClassCode { get; set; }
    public DateOnly? LicenseExpirationDate { get; set; }
    public DateOnly? HireDate { get; set; }
    [Range(0, 100)] public int? YearsExperience { get; set; }
    [Required, StringLength(50)] public string DriverStatusCode { get; set; } = string.Empty;
    public bool IsExcluded { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    public Guid? UserId { get; set; }
}

public sealed class UpsertAccountPropertyRequest
{
    public Guid? AccountPropertyId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid AccountLocationId { get; set; }
    [Required, StringLength(50)] public string PropertyNumber { get; set; } = string.Empty;
    [Required, StringLength(50)] public string PropertyTypeCode { get; set; } = string.Empty;
    [StringLength(50)] public string? ConstructionTypeCode { get; set; }
    [StringLength(80)] public string? OccupancyCode { get; set; }
    [Range(1000, 2200)] public int? YearBuilt { get; set; }
    [Range(0, 100000000)] public int? SquareFeet { get; set; }
    [Range(0, 1000)] public int? NumberOfStories { get; set; }
    [Range(0, 999999999999)] public decimal? BuildingValue { get; set; }
    [Range(0, 999999999999)] public decimal? ContentsValue { get; set; }
    [Range(0, 999999999999)] public decimal? BusinessIncomeValue { get; set; }
    [StringLength(50)] public string? ProtectionClassCode { get; set; }
    [StringLength(50)] public string? RoofTypeCode { get; set; }
    [Range(1000, 2200)] public int? RoofYear { get; set; }
    [Range(0, 100)] public decimal? SprinkleredPercentage { get; set; }
    public bool IsActive { get; set; } = true;
    [StringLength(1000)] public string? Notes { get; set; }
    public Guid? UserId { get; set; }
}

public sealed class UpsertAccountScheduleItemRequest
{
    public Guid? AccountScheduleItemId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? AccountLocationId { get; set; }
    [Required, StringLength(50)] public string ScheduleTypeCode { get; set; } = string.Empty;
    [Required, StringLength(50)] public string ItemNumber { get; set; } = string.Empty;
    [Required, StringLength(300)] public string ItemDescription { get; set; } = string.Empty;
    [StringLength(100)] public string? Manufacturer { get; set; }
    [StringLength(100)] public string? Model { get; set; }
    [StringLength(100)] public string? SerialNumber { get; set; }
    public DateOnly? AcquisitionDate { get; set; }
    public DateOnly? AppraisalDate { get; set; }
    [Range(0.01, 999999999999)] public decimal ScheduledValue { get; set; }
    [Range(0, 999999999)] public decimal? DeductibleAmount { get; set; }
    public bool IsActive { get; set; } = true;
    [StringLength(1000)] public string? Notes { get; set; }
    public Guid? UserId { get; set; }
}
