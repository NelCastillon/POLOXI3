namespace Ams.Application.Common.Dtos;

public sealed class Account360Dto
{
    public AccountDto Account { get; set; } = new();
    public IReadOnlyList<ContactDto> Contacts { get; set; } = [];
    public IReadOnlyList<AccountStakeholderDto> Stakeholders { get; set; } = [];
    public IReadOnlyList<AccountCommunicationPreferenceDto> CommunicationPreferences { get; set; } = [];
    public IReadOnlyList<AccountServiceAssignmentDto> ServiceAssignments { get; set; } = [];
    public IReadOnlyList<AccountNamedInsuredDto> NamedInsureds { get; set; } = [];
    public IReadOnlyList<AccountLocationDto> Locations { get; set; } = [];
    public IReadOnlyList<AccountVehicleDto> Vehicles { get; set; } = [];
    public IReadOnlyList<AccountDriverDto> Drivers { get; set; } = [];
    public IReadOnlyList<AccountPropertyDto> Properties { get; set; } = [];
    public IReadOnlyList<AccountScheduleItemDto> ScheduleItems { get; set; } = [];
    public IReadOnlyList<Account360ActivityDto> Activities { get; set; } = [];
    public IReadOnlyList<Account360RelationshipDto> Relationships { get; set; } = [];
    public IReadOnlyList<Account360TimelineItemDto> Timeline { get; set; } = [];
    public IReadOnlyList<AccountNoteDto> Notes { get; set; } = [];
    public IReadOnlyList<TaskItemDto> Tasks { get; set; } = [];
    public IReadOnlyList<DocumentDto> Documents { get; set; } = [];
    public IReadOnlyList<SubmissionDto> Submissions { get; set; } = [];
    public IReadOnlyList<OpportunityDto> Opportunities { get; set; } = [];
    public IReadOnlyList<ClaimDto> Claims { get; set; } = [];
    public IReadOnlyList<AccountReferenceOptionDto> ReferenceOptions { get; set; } = [];
    public Account360MetricsDto Metrics { get; set; } = new();
}

public sealed class AccountStakeholderDto
{
    public Guid AccountStakeholderId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid ContactId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string StakeholderRoleCode { get; set; } = string.Empty;
    public decimal? OwnershipPercentage { get; set; }
    public bool IsPrimary { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public string? Notes { get; set; }
}

public sealed class AccountCommunicationPreferenceDto
{
    public Guid AccountCommunicationPreferenceId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? ContactId { get; set; }
    public string? ContactName { get; set; }
    public string CommunicationPurposeCode { get; set; } = string.Empty;
    public string ChannelCode { get; set; } = string.Empty;
    public string PreferenceStatusCode { get; set; } = string.Empty;
    public string? PreferredTimeZoneCode { get; set; }
    public TimeOnly? PreferredStartTime { get; set; }
    public TimeOnly? PreferredEndTime { get; set; }
    public string? ConsentSourceCode { get; set; }
    public DateTime? ConsentDateUtc { get; set; }
    public string? Notes { get; set; }
}

public sealed class AccountServiceAssignmentDto
{
    public Guid AccountServiceAssignmentId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string AssignmentRoleCode { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public string? Notes { get; set; }
}

public sealed class AccountNamedInsuredDto
{
    public Guid AccountNamedInsuredId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? ContactId { get; set; }
    public string InsuredTypeCode { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string? DbaName { get; set; }
    public string? TaxIdentifier { get; set; }
    public string RelationshipCode { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class AccountLocationDto
{
    public Guid AccountLocationId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public string LocationNumber { get; set; } = string.Empty;
    public string LocationTypeCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string StateCode { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string? County { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsMailingAddress { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? OccupancyCode { get; set; }
    public decimal? AnnualRevenue { get; set; }
    public int? EmployeeCount { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class AccountVehicleDto
{
    public Guid AccountVehicleId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? AccountLocationId { get; set; }
    public string VehicleNumber { get; set; } = string.Empty;
    public string Vin { get; set; } = string.Empty;
    public int ModelYear { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string VehicleTypeCode { get; set; } = string.Empty;
    public string UseTypeCode { get; set; } = string.Empty;
    public string? GaragingStateCode { get; set; }
    public string? GaragingPostalCode { get; set; }
    public int? RadiusMiles { get; set; }
    public int? AnnualMileage { get; set; }
    public decimal? CostNew { get; set; }
    public decimal? StatedValue { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class AccountDriverDto
{
    public Guid AccountDriverId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? ContactId { get; set; }
    public string DriverNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public string LicenseStateCode { get; set; } = string.Empty;
    public string? LicenseClassCode { get; set; }
    public DateOnly? LicenseExpirationDate { get; set; }
    public DateOnly? HireDate { get; set; }
    public int? YearsExperience { get; set; }
    public string DriverStatusCode { get; set; } = string.Empty;
    public bool IsExcluded { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class AccountPropertyDto
{
    public Guid AccountPropertyId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid AccountLocationId { get; set; }
    public string PropertyNumber { get; set; } = string.Empty;
    public string PropertyTypeCode { get; set; } = string.Empty;
    public string? ConstructionTypeCode { get; set; }
    public string? OccupancyCode { get; set; }
    public int? YearBuilt { get; set; }
    public int? SquareFeet { get; set; }
    public int? NumberOfStories { get; set; }
    public decimal? BuildingValue { get; set; }
    public decimal? ContentsValue { get; set; }
    public decimal? BusinessIncomeValue { get; set; }
    public string? ProtectionClassCode { get; set; }
    public string? RoofTypeCode { get; set; }
    public int? RoofYear { get; set; }
    public decimal? SprinkleredPercentage { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class AccountScheduleItemDto
{
    public Guid AccountScheduleItemId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? AccountLocationId { get; set; }
    public string ScheduleTypeCode { get; set; } = string.Empty;
    public string ItemNumber { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public DateOnly? AcquisitionDate { get; set; }
    public DateOnly? AppraisalDate { get; set; }
    public decimal ScheduledValue { get; set; }
    public decimal? DeductibleAmount { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class Account360ActivityDto
{
    public Guid ActivityId { get; set; }
    public string ActivityTypeCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public DateTime OccurredDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class Account360RelationshipDto
{
    public Guid RelationshipId { get; set; }
    public Guid RelatedAccountId { get; set; }
    public string RelatedAccountName { get; set; } = string.Empty;
    public string RelationshipTypeCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public sealed class Account360TimelineItemDto
{
    public string EventTypeCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public DateTime EventDateUtc { get; set; }
}

public sealed class Account360MetricsDto
{
    public int NamedInsuredCount { get; set; }
    public int LocationCount { get; set; }
    public int VehicleCount { get; set; }
    public int DriverCount { get; set; }
    public int PropertyCount { get; set; }
    public int ScheduleItemCount { get; set; }
    public decimal TotalScheduledValue { get; set; }
    public decimal TotalPropertyValue { get; set; }
    public int OpenTaskCount { get; set; }
    public int ActiveSubmissionCount { get; set; }
    public int OpenClaimCount { get; set; }
    public int CertificateCount { get; set; }
    public int DocumentCount { get; set; }
}
