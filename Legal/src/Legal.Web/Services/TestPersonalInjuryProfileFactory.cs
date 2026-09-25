using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Web.Services;

// Builds a fictional-but-internally-consistent Personal Injury profile for the
// "Generate Test Matter" flow so a generated matter opens complete with a PI Profile.
// Selectable codes are taken from the DB-backed PersonalInjuryOptionsDto when available,
// with representative fallbacks so the generated profile is still complete offline.
public static class TestPersonalInjuryProfileFactory
{
    private static readonly Random _rng = new();

    private static T Pick<T>(IReadOnlyList<T> items) => items[_rng.Next(items.Count)];

    // Prefer a DB-backed option; otherwise fall back to a representative value.
    private static string PickCode(IReadOnlyCollection<string> options, string fallback)
        => options is { Count: > 0 } ? Pick(options.ToList()) : fallback;

    private static string? PickCodeOrNull(IReadOnlyCollection<string> options, string fallback)
        => options is { Count: > 0 } ? Pick(options.ToList()) : fallback;

    private static readonly string[] _carriers =
    {
        "State Farm Mutual", "GEICO General Insurance", "Progressive Casualty",
        "Allstate Insurance", "Nationwide Mutual", "Farmers Insurance", "Liberty Mutual", "USAA"
    };

    private static readonly string[] _adjusters =
    {
        "Karen Whitfield", "Daniel Ortega", "Michelle Tran", "Gregory Palmer",
        "Sandra Coleman", "Vincent Alvarez", "Beth Donovan", "Raymond Cho"
    };

    private static readonly string[] _providers =
    {
        "Riverside Emergency Department", "Summit Orthopedic Associates", "Cascade Physical Therapy",
        "Meridian Neurology Group", "Pioneer Pain Management", "Northwest Chiropractic Center",
        "Evergreen Radiology", "Harborview Surgical Center"
    };

    private static readonly string[] _bodyAreaFallbacks =
    {
        "Cervical Spine", "Lumbar Spine", "Right Shoulder", "Left Knee", "Head / Brain", "Right Wrist", "Hip"
    };

    private static readonly string[] _diagnoses =
    {
        "C5-C6 disc herniation with radiculopathy", "L4-L5 disc protrusion", "Full-thickness rotator cuff tear",
        "ACL rupture with meniscal tear", "Post-concussion syndrome", "Distal radius fracture",
        "Displaced femoral neck fracture", "Complex regional pain syndrome"
    };

    private static readonly string[] _witnessNames =
    {
        "Jordan Riley", "Alex Morgan", "Taylor Brooks", "Casey Nguyen", "Jamie Fletcher", "Morgan Ellis"
    };

    // Builds a complete fictional PI profile. Context (state/city/county/incidentSummary/etc.)
    // is optional and, when supplied, aligns the profile with the generated matter narrative.
    public static PersonalInjuryProfileSaveRequest Build(
        PersonalInjuryOptionsDto options,
        string? state = null,
        string? city = null,
        string? county = null,
        string? incidentSummary = null,
        string? liabilitySummary = null,
        string? injurySummary = null,
        string? treatmentSummary = null,
        string? damagesSummary = null,
        string? plaintiff = null,
        string? defendant = null)
    {
        options ??= new();

        var incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-_rng.Next(210, 420)));
        var firstTreat = incidentDate.AddDays(_rng.Next(0, 3));
        var lastTreat = incidentDate.AddDays(_rng.Next(120, 300));

        // Coherent monetary figures: bills < damages < demand.
        var billedA = _rng.Next(18_000, 45_000);
        var billedB = _rng.Next(6_000, 20_000);
        var totalBilled = billedA + billedB;
        var wageLoss = _rng.Next(8_000, 32_000);
        var painSuffering = totalBilled * _rng.Next(2, 4);
        var demandAmount = totalBilled + wageLoss + painSuffering + _rng.Next(10_000, 40_000);
        var firstOffer = (decimal)(demandAmount * 0.25);

        var carrier = Pick(_carriers);
        var providerA = Pick(_providers);
        var providerB = Pick(_providers);

        return new PersonalInjuryProfileSaveRequest
        {
            IncidentTypeCode = PickCodeOrNull(options.IncidentTypes, "Motor Vehicle Accident"),
            IncidentDate = incidentDate,
            IncidentTime = new TimeOnly(_rng.Next(6, 20), _rng.Next(0, 60)),
            IncidentLocation = $"Intersection of Main St and 5th Ave, {city ?? "Springfield"}",
            IncidentCity = city ?? "Springfield",
            IncidentCounty = county,
            IncidentState = state,
            IncidentSummary = incidentSummary
                ?? $"{plaintiff ?? "The plaintiff"} was injured in a collision allegedly caused by {defendant ?? "the defendant"}.",
            LiabilitySummary = liabilitySummary
                ?? "Liability is supported by the police report and an independent witness; defendant disputes comparative fault.",
            InjurySummary = injurySummary
                ?? "Plaintiff sustained orthopedic injuries requiring imaging, specialist care, and ongoing therapy.",
            TreatmentSummary = treatmentSummary
                ?? "Emergency care followed by orthopedic evaluation, imaging, and a course of physical therapy.",
            DamagesSummary = damagesSummary
                ?? $"Past medical specials of approximately ${totalBilled:N0}, wage loss, and general damages.",
            CurrentStageCode = PickCodeOrNull(options.MatterStages, "Pre-Litigation Demand"),
            LitigationStatusCode = PickCodeOrNull(options.LitigationStatuses, "Pre-Litigation"),
            DemandStatusCode = PickCodeOrNull(options.DemandStatuses, "Sent"),
            SettlementStatusCode = PickCodeOrNull(options.SettlementStatuses, "Open"),

            InsurancePolicies =
            [
                new PersonalInjuryInsurancePolicyDto
                {
                    Id = Guid.NewGuid(),
                    Carrier = carrier,
                    Insured = defendant,
                    Adjuster = Pick(_adjusters),
                    ClaimNumber = $"CLM-{_rng.Next(100000, 999999)}",
                    PolicyNumber = $"POL-{_rng.Next(1000000, 9999999)}",
                    CoverageTypeCode = PickCodeOrNull(options.CoverageTypes, "Bodily Injury Liability"),
                    BodilyInjuryLimitPerPerson = 100_000m,
                    BodilyInjuryLimitPerOccur = 300_000m,
                    CoverageStatusCode = PickCodeOrNull(options.CoverageStatuses, "Confirmed"),
                    LimitsSource = "Adjuster disclosure letter",
                    LimitsVerified = true,
                    Notes = "Liability carrier acknowledged coverage; policy limits disclosed."
                }
            ],
            Injuries =
            [
                new PersonalInjuryInjuryDto
                {
                    Id = Guid.NewGuid(),
                    BodyAreaCode = PickCode(options.BodyAreas, Pick(_bodyAreaFallbacks)),
                    InitialSymptoms = "Pain, stiffness, and reduced range of motion at the scene and in the days following.",
                    Diagnosis = Pick(_diagnoses),
                    IsPreexisting = false,
                    ClaimedPermanency = true,
                    SurgeryRecommended = true,
                    SurgeryPerformed = _rng.Next(2) == 0,
                    SeverityCode = PickCodeOrNull(options.InjurySeverities, "Moderate"),
                    Notes = "Confirmed on MRI; specialist attributes injury to the incident."
                }
            ],
            Treatments =
            [
                new PersonalInjuryTreatmentDto
                {
                    Id = Guid.NewGuid(),
                    Provider = providerA,
                    Specialty = "Orthopedic Surgery",
                    FirstTreatmentDate = firstTreat,
                    LastTreatmentDate = lastTreat,
                    StatusCode = PickCodeOrNull(options.TreatmentStatuses, "Completed"),
                    VisitCount = _rng.Next(6, 24),
                    RecordRequestStatus = "Received",
                    BillRequestStatus = "Received",
                    TreatmentGapDays = _rng.Next(0, 21),
                    Notes = "Consistent treatment with documented improvement and residual limitations."
                }
            ],
            MedicalBills =
            [
                new PersonalInjuryMedicalBillDto
                {
                    Id = Guid.NewGuid(),
                    Provider = providerA,
                    AmountBilled = billedA,
                    Adjustments = billedA * 0.15m,
                    AmountPaid = 0m,
                    OutstandingBalance = billedA * 0.85m,
                    Payer = "Health insurance / lien",
                    LienStatusCode = PickCodeOrNull(options.LienStatuses, "Asserted"),
                    Notes = "Records and itemized bill obtained."
                },
                new PersonalInjuryMedicalBillDto
                {
                    Id = Guid.NewGuid(),
                    Provider = providerB,
                    AmountBilled = billedB,
                    Adjustments = billedB * 0.10m,
                    AmountPaid = 0m,
                    OutstandingBalance = billedB * 0.90m,
                    Payer = "Health insurance / lien",
                    LienStatusCode = PickCodeOrNull(options.LienStatuses, "Asserted"),
                    Notes = "Therapy billing reconciled."
                }
            ],
            Damages =
            [
                new PersonalInjuryDamageDto
                {
                    Id = Guid.NewGuid(),
                    DamageTypeCode = PickCodeOrNull(options.DamageTypes, "Medical Expenses"),
                    Description = "Past medical specials",
                    ClaimedAmount = totalBilled,
                    IsEconomic = true,
                    Notes = "Supported by itemized bills and records."
                },
                new PersonalInjuryDamageDto
                {
                    Id = Guid.NewGuid(),
                    DamageTypeCode = PickCodeOrNull(options.DamageTypes, "Lost Wages"),
                    Description = "Wage loss during recovery",
                    ClaimedAmount = wageLoss,
                    IsEconomic = true,
                    Notes = "Supported by employer wage verification."
                },
                new PersonalInjuryDamageDto
                {
                    Id = Guid.NewGuid(),
                    DamageTypeCode = PickCodeOrNull(options.DamageTypes, "Pain and Suffering"),
                    Description = "General damages",
                    ClaimedAmount = painSuffering,
                    IsEconomic = false,
                    Notes = "Non-economic damages for pain, suffering, and loss of enjoyment."
                }
            ],
            Liens =
            [
                new PersonalInjuryLienDto
                {
                    Id = Guid.NewGuid(),
                    Lienholder = "Regional Health Plan",
                    LienTypeCode = PickCodeOrNull(options.LienTypes, "Health Insurance"),
                    AssertedAmount = totalBilled * 0.4m,
                    VerifiedAmount = totalBilled * 0.4m,
                    NegotiatedAmount = null,
                    FinalPayoffAmount = null,
                    StatusCode = PickCodeOrNull(options.LienStatuses, "Asserted"),
                    Notes = "Statutory reduction to be negotiated at resolution."
                }
            ],
            Demands =
            [
                new PersonalInjuryDemandDto
                {
                    Id = Guid.NewGuid(),
                    DemandDate = lastTreat.AddDays(14),
                    DemandAmount = demandAmount,
                    Recipient = $"{carrier} (Claims)",
                    ResponseDeadline = lastTreat.AddDays(44),
                    StatusCode = PickCodeOrNull(options.DemandStatuses, "Sent"),
                    Notes = "Policy-limits demand with supporting records and wage documentation."
                }
            ],
            Negotiations =
            [
                new PersonalInjuryNegotiationDto
                {
                    Id = Guid.NewGuid(),
                    EventDate = lastTreat.AddDays(30),
                    Amount = firstOffer,
                    Source = $"{carrier} adjuster",
                    IsOffer = true,
                    Conditions = "Initial offer; full release required.",
                    ExpirationDate = lastTreat.AddDays(60),
                    Notes = "Below case value; counter anticipated."
                }
            ],
            Settlements = [],
            Deadlines =
            [
                new PersonalInjuryDeadlineDto
                {
                    Id = Guid.NewGuid(),
                    DeadlineTypeCode = "Statute of Limitations",
                    CandidateDate = incidentDate.AddYears(2),
                    RuleSourceCode = PickCodeOrNull(options.DeadlineRuleSources, "Statute"),
                    VerificationState = PickCodeOrNull(options.DeadlineVerificationStates, "Unverified"),
                    Notes = "Two-year personal injury limitations period; verify jurisdiction-specific tolling."
                }
            ],
            IncidentVehicles =
            [
                new PersonalInjuryIncidentVehicleDto
                {
                    Id = Guid.NewGuid(),
                    RoleCode = PickCodeOrNull(options.VehicleRoles, "Plaintiff Vehicle"),
                    Description = "2019 Honda Accord",
                    Owner = plaintiff,
                    Driver = plaintiff,
                    ImpactType = "Rear",
                    Citation = "None",
                    Notes = "Moderate rear-end damage documented in photographs."
                },
                new PersonalInjuryIncidentVehicleDto
                {
                    Id = Guid.NewGuid(),
                    RoleCode = PickCodeOrNull(options.VehicleRoles, "Defendant Vehicle"),
                    Description = "2015 Ford F-150",
                    Owner = defendant,
                    Driver = defendant,
                    ImpactType = "Front",
                    Citation = "Cited for following too closely",
                    Notes = "At-fault vehicle per police report."
                }
            ],
            Witnesses =
            [
                new PersonalInjuryWitnessDto
                {
                    Id = Guid.NewGuid(),
                    Name = Pick(_witnessNames),
                    ContactInfo = $"(555) {_rng.Next(200, 999)}-{_rng.Next(1000, 9999)}",
                    StatementSummary = "Independent witness observed the defendant strike the plaintiff's vehicle.",
                    SupportsClient = true,
                    Notes = "Willing to provide a recorded statement."
                }
            ]
        };
    }
}
