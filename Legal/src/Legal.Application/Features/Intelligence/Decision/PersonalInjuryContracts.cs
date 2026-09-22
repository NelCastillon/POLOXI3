using System.ComponentModel.DataAnnotations;

namespace Legal.Application.Features.Intelligence.Decision;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// Personal Injury (Domain Pack: PERSONAL_INJURY) contracts. PI is a first-class practice-area domain
// OVER the shared DecisionMatter aggregate — these DTOs carry the structured PI matter data
// (profile + child aggregates), the Generate-New-Matter draft/provenance model, and the PI decision
// context that is transformed into the existing DecisionSearchRequest (POLOXI Core is unchanged).
// Selectable values (incident types, coverage types, decision types, …) are DB-backed; the codes
// here are field identifiers only.
// ────────────────────────────────────────────────────────────────────────────────────────────────

// ── PI Matter Profile (1:1 extension of the Matter aggregate) ──
public sealed record PersonalInjuryProfileDto(
    Guid DecisionMatterId)
{
    public string? IncidentTypeCode { get; init; }
    public DateOnly? IncidentDate { get; init; }
    public TimeOnly? IncidentTime { get; init; }
    public string? IncidentLocation { get; init; }
    public string? IncidentCity { get; init; }
    public string? IncidentCounty { get; init; }
    public string? IncidentState { get; init; }
    public string? IncidentSummary { get; init; }
    public string? LiabilitySummary { get; init; }
    public string? InjurySummary { get; init; }
    public string? TreatmentSummary { get; init; }
    public string? DamagesSummary { get; init; }
    public string? CurrentStageCode { get; init; }
    public string? LitigationStatusCode { get; init; }
    public string? DemandStatusCode { get; init; }
    public string? SettlementStatusCode { get; init; }

    public IReadOnlyCollection<PersonalInjuryInsurancePolicyDto> InsurancePolicies { get; init; } = [];
    public IReadOnlyCollection<PersonalInjuryInjuryDto> Injuries { get; init; } = [];
    public IReadOnlyCollection<PersonalInjuryTreatmentDto> Treatments { get; init; } = [];
    public IReadOnlyCollection<PersonalInjuryMedicalBillDto> MedicalBills { get; init; } = [];
    public IReadOnlyCollection<PersonalInjuryDamageDto> Damages { get; init; } = [];
    public IReadOnlyCollection<PersonalInjuryLienDto> Liens { get; init; } = [];
    public IReadOnlyCollection<PersonalInjuryDemandDto> Demands { get; init; } = [];
    public IReadOnlyCollection<PersonalInjuryNegotiationDto> Negotiations { get; init; } = [];
    public IReadOnlyCollection<PersonalInjurySettlementDto> Settlements { get; init; } = [];
    public IReadOnlyCollection<PersonalInjuryDeadlineDto> Deadlines { get; init; } = [];
    public IReadOnlyCollection<PersonalInjuryIncidentVehicleDto> IncidentVehicles { get; init; } = [];
    public IReadOnlyCollection<PersonalInjuryWitnessDto> Witnesses { get; init; } = [];
}

// ── PI Matter child DTOs ──
public sealed record PersonalInjuryInsurancePolicyDto
{
    public Guid Id { get; init; }
    public string? Carrier { get; init; }
    public string? Insured { get; init; }
    public string? Adjuster { get; init; }
    public string? ClaimNumber { get; init; }
    public string? PolicyNumber { get; init; }
    public string? CoverageTypeCode { get; init; }
    public decimal? BodilyInjuryLimitPerPerson { get; init; }
    public decimal? BodilyInjuryLimitPerOccur { get; init; }
    public string? CoverageStatusCode { get; init; }
    public string? LimitsSource { get; init; }
    public bool LimitsVerified { get; init; }
    public string? Notes { get; init; }
}

public sealed record PersonalInjuryInjuryDto
{
    public Guid Id { get; init; }
    public string? BodyAreaCode { get; init; }
    public string? InitialSymptoms { get; init; }
    public string? Diagnosis { get; init; }
    public bool IsPreexisting { get; init; }
    public bool ClaimedPermanency { get; init; }
    public bool SurgeryRecommended { get; init; }
    public bool SurgeryPerformed { get; init; }
    public string? SeverityCode { get; init; }
    public string? Notes { get; init; }
}

public sealed record PersonalInjuryTreatmentDto
{
    public Guid Id { get; init; }
    public string? Provider { get; init; }
    public string? Specialty { get; init; }
    public DateOnly? FirstTreatmentDate { get; init; }
    public DateOnly? LastTreatmentDate { get; init; }
    public string? StatusCode { get; init; }
    public int? VisitCount { get; init; }
    public string? RecordRequestStatus { get; init; }
    public string? BillRequestStatus { get; init; }
    public int? TreatmentGapDays { get; init; }
    public string? Notes { get; init; }
}

public sealed record PersonalInjuryMedicalBillDto
{
    public Guid Id { get; init; }
    public string? Provider { get; init; }
    public decimal? AmountBilled { get; init; }
    public decimal? Adjustments { get; init; }
    public decimal? AmountPaid { get; init; }
    public decimal? OutstandingBalance { get; init; }
    public string? Payer { get; init; }
    public string? LienStatusCode { get; init; }
    public string? Notes { get; init; }
}

public sealed record PersonalInjuryDamageDto
{
    public Guid Id { get; init; }
    public string? DamageTypeCode { get; init; }
    public string? Description { get; init; }
    public decimal? ClaimedAmount { get; init; }
    public bool IsEconomic { get; init; }
    public string? Notes { get; init; }
}

public sealed record PersonalInjuryLienDto
{
    public Guid Id { get; init; }
    public string? Lienholder { get; init; }
    public string? LienTypeCode { get; init; }
    public decimal? AssertedAmount { get; init; }
    public decimal? VerifiedAmount { get; init; }
    public decimal? NegotiatedAmount { get; init; }
    public decimal? FinalPayoffAmount { get; init; }
    public string? StatusCode { get; init; }
    public string? Notes { get; init; }
}

public sealed record PersonalInjuryDemandDto
{
    public Guid Id { get; init; }
    public DateOnly? DemandDate { get; init; }
    public decimal? DemandAmount { get; init; }
    public string? Recipient { get; init; }
    public DateOnly? ResponseDeadline { get; init; }
    public string? StatusCode { get; init; }
    public string? Notes { get; init; }
}

public sealed record PersonalInjuryNegotiationDto
{
    public Guid Id { get; init; }
    public DateOnly? EventDate { get; init; }
    public decimal? Amount { get; init; }
    public string? Source { get; init; }
    public bool IsOffer { get; init; }
    public string? Conditions { get; init; }
    public DateOnly? ExpirationDate { get; init; }
    public string? Notes { get; init; }
}

public sealed record PersonalInjurySettlementDto
{
    public Guid Id { get; init; }
    public decimal? GrossRecovery { get; init; }
    public decimal? Fees { get; init; }
    public decimal? Costs { get; init; }
    public decimal? Liens { get; init; }
    public decimal? NetToClient { get; init; }
    public string? StatusCode { get; init; }
    public DateOnly? SettlementDate { get; init; }
    public string? Notes { get; init; }
}

public sealed record PersonalInjuryDeadlineDto
{
    public Guid Id { get; init; }
    public string? DeadlineTypeCode { get; init; }
    public DateOnly? CandidateDate { get; init; }
    public string? RuleSourceCode { get; init; }
    public string? VerificationState { get; init; }
    public string? Notes { get; init; }
}

public sealed record PersonalInjuryIncidentVehicleDto
{
    public Guid Id { get; init; }
    public string? RoleCode { get; init; }
    public string? Description { get; init; }
    public string? Owner { get; init; }
    public string? Driver { get; init; }
    public string? ImpactType { get; init; }
    public string? Citation { get; init; }
    public string? Notes { get; init; }
}

public sealed record PersonalInjuryWitnessDto
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public string? ContactInfo { get; init; }
    public string? StatementSummary { get; init; }
    public bool? SupportsClient { get; init; }
    public string? Notes { get; init; }
}

// ── PI Matter save request (manual wizard writes the whole PI profile + children) ──
public sealed record PersonalInjuryProfileSaveRequest
{
    [StringLength(60)] public string? IncidentTypeCode { get; init; }
    public DateOnly? IncidentDate { get; init; }
    public TimeOnly? IncidentTime { get; init; }
    [StringLength(400)] public string? IncidentLocation { get; init; }
    [StringLength(120)] public string? IncidentCity { get; init; }
    [StringLength(120)] public string? IncidentCounty { get; init; }
    [StringLength(60)] public string? IncidentState { get; init; }
    public string? IncidentSummary { get; init; }
    public string? LiabilitySummary { get; init; }
    public string? InjurySummary { get; init; }
    public string? TreatmentSummary { get; init; }
    public string? DamagesSummary { get; init; }
    [StringLength(60)] public string? CurrentStageCode { get; init; }
    [StringLength(60)] public string? LitigationStatusCode { get; init; }
    [StringLength(60)] public string? DemandStatusCode { get; init; }
    [StringLength(60)] public string? SettlementStatusCode { get; init; }

    public IReadOnlyCollection<PersonalInjuryInsurancePolicyDto> InsurancePolicies { get; init; } = [];
    public IReadOnlyCollection<PersonalInjuryInjuryDto> Injuries { get; init; } = [];
    public IReadOnlyCollection<PersonalInjuryTreatmentDto> Treatments { get; init; } = [];
    public IReadOnlyCollection<PersonalInjuryMedicalBillDto> MedicalBills { get; init; } = [];
    public IReadOnlyCollection<PersonalInjuryDamageDto> Damages { get; init; } = [];
    public IReadOnlyCollection<PersonalInjuryLienDto> Liens { get; init; } = [];
    public IReadOnlyCollection<PersonalInjuryDemandDto> Demands { get; init; } = [];
    public IReadOnlyCollection<PersonalInjuryNegotiationDto> Negotiations { get; init; } = [];
    public IReadOnlyCollection<PersonalInjurySettlementDto> Settlements { get; init; } = [];
    public IReadOnlyCollection<PersonalInjuryDeadlineDto> Deadlines { get; init; } = [];
    public IReadOnlyCollection<PersonalInjuryIncidentVehicleDto> IncidentVehicles { get; init; } = [];
    public IReadOnlyCollection<PersonalInjuryWitnessDto> Witnesses { get; init; } = [];
}

// ── DB-backed PI option sets for the wizard/dashboard/decision dropdowns ──
public sealed record PersonalInjuryOptionsDto
{
    public IReadOnlyCollection<string> IncidentTypes { get; init; } = [];
    public IReadOnlyCollection<string> BodyAreas { get; init; } = [];
    public IReadOnlyCollection<string> InjurySeverities { get; init; } = [];
    public IReadOnlyCollection<string> CoverageTypes { get; init; } = [];
    public IReadOnlyCollection<string> CoverageStatuses { get; init; } = [];
    public IReadOnlyCollection<string> TreatmentStatuses { get; init; } = [];
    public IReadOnlyCollection<string> DamageTypes { get; init; } = [];
    public IReadOnlyCollection<string> LienTypes { get; init; } = [];
    public IReadOnlyCollection<string> LienStatuses { get; init; } = [];
    public IReadOnlyCollection<string> DemandStatuses { get; init; } = [];
    public IReadOnlyCollection<string> SettlementStatuses { get; init; } = [];
    public IReadOnlyCollection<string> LitigationStatuses { get; init; } = [];
    public IReadOnlyCollection<string> MatterStages { get; init; } = [];
    public IReadOnlyCollection<string> DeadlineRuleSources { get; init; } = [];
    public IReadOnlyCollection<string> DeadlineVerificationStates { get; init; } = [];
    public IReadOnlyCollection<string> VehicleRoles { get; init; } = [];
    public IReadOnlyCollection<string> DraftSourceTypes { get; init; } = [];
    public IReadOnlyCollection<string> DraftVerificationStates { get; init; } = [];
}

// ── Generate-New-Matter draft / provenance model (scaffold; extraction wired later) ──
// A field either has adequate provenance or it does not — no meaningless overall confidence.
public sealed record GeneratedMatterFieldDto(
    string FieldCode,
    string? ProposedValue,
    string SourceType,          // PI_DRAFT_SOURCE_TYPE (DB-backed)
    string VerificationState)   // PI_DRAFT_VERIFICATION_STATE (DB-backed)
{
    public Guid? SourceDocumentId { get; init; }
    public string? SourcePassage { get; init; }
    public string? ConflictReason { get; init; }
    // Competing values when SourceType/VerificationState indicates a Conflict.
    public IReadOnlyCollection<GeneratedMatterFieldCandidateDto> Conflicts { get; init; } = [];
}

public sealed record GeneratedMatterFieldCandidateDto(string Value, string Source);

// A persisted PI matter draft produced by the Generate flow, pending attorney review/confirmation.
public sealed record PersonalInjuryMatterDraftDto(
    Guid DecisionMatterDraftId,
    string StatusCode,          // PENDING_REVIEW | CONFIRMED | DISCARDED
    string? Prompt,
    DateTime CreatedDateUtc)
{
    public IReadOnlyCollection<GeneratedMatterFieldDto> Fields { get; init; } = [];
}

// Request to create a draft (prompt + optional document references). Extraction deferred.
public sealed record PersonalInjuryMatterDraftCreateRequest
{
    [StringLength(8000)] public string? Prompt { get; init; }
    public IReadOnlyCollection<Guid> SourceDocumentIds { get; init; } = [];
    // Optional pre-extracted fields (populated by a later extraction stage / manual entry).
    public IReadOnlyCollection<GeneratedMatterFieldDto> Fields { get; init; } = [];
}

// Draft field statuses / provenance well-known codes (mirror DB seeds in 0286).
public static class PersonalInjuryDraftSourceTypes
{
    public const string UserSupplied = "User Supplied";
    public const string DocumentExtracted = "Document Extracted";
    public const string Inferred = "Inferred";
    public const string Missing = "Missing";
}

public static class PersonalInjuryDraftVerificationStates
{
    public const string Confirmed = "Confirmed";
    public const string Review = "Review";
    public const string Missing = "Missing";
    public const string Conflict = "Conflict";
    public const string Invalid = "Invalid";
}

public static class PersonalInjuryMatterDraftStatusCodes
{
    public const string PendingReview = "PENDING_REVIEW";
    public const string Confirmed = "CONFIRMED";
    public const string Discarded = "DISCARDED";
}

// ── PI Decision Intelligence: DB-backed decision types + stage default map ──
public sealed record PersonalInjuryDecisionTypeDto(
    string DecisionTypeCode,
    string Name,
    string? Description);

public sealed record PersonalInjuryStageDecisionDto(
    string StageCode,
    string DefaultDecisionTypeCode);

// PI decision context assembled from the matter profile; transformed into a DecisionSearchRequest.
// POLOXI Core stays unchanged — this only enriches the query text and sets DomainPackCode.
public sealed record PersonalInjuryDecisionContext(
    Guid DecisionMatterId,
    [Required, StringLength(60)] string DecisionTypeCode)
{
    [StringLength(4000)] public string? Question { get; init; }
    [StringLength(120)] public string? ModelCode { get; init; }
    [StringLength(120)] public string? ContextCode { get; init; }
}
