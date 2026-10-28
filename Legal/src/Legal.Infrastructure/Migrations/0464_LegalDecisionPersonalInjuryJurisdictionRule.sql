SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- 0464: Personal Injury Jurisdiction Rules catalog (50 US states + DC).
--
--   A deterministic, DB-backed reference catalog of commonly-cited state-law
--   personal-injury rules: statute of limitations, comparative-fault regime,
--   minors tolling, discovery rule, joint-and-several liability, dram-shop
--   liability, government-claim notice deadlines, damages caps, punitive-damage
--   standard, and medical-malpractice specifics.
--
--   ADVISORY ONLY. Like a Domain Pack, this supplies DOMAIN REFERENCE DATA and
--   does NOT own any POLOXI Core reasoning (candidate competition, frontier,
--   information value, readiness). Rows are commonly-cited GENERAL rules and are
--   NOT legal advice: accrual, tolling, exceptions, caps, and inflation
--   adjustments vary by claim type and change over time -- each row carries a
--   SourceNote caveat and EffectiveDate and must be independently verified.
--
--   Global (TenantId NULL) rows are seeded defaults; tenant rows override,
--   mirroring the Legal_DecisionDomainPack* convention (migration 0284).
--   All objects live in the POLOXI schema and carry base/audit fields.
-- ============================================================================

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── Legal_DecisionJurisdictionRule: one row per sovereign (US state / DC). ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionJurisdictionRule', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionJurisdictionRule
(
	DecisionJurisdictionRuleId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionJurisdictionRule PRIMARY KEY DEFAULT NEWID(),
	PracticeAreaCode                NVARCHAR(60) NOT NULL CONSTRAINT DF_Legal_DecisionJurisdictionRule_Practice DEFAULT N'PERSONAL_INJURY',
	-- Two-letter USPS sovereign code (CA, NY, DC, ...). Business key within tenant scope.
	JurisdictionCode                NVARCHAR(10) NOT NULL,
	JurisdictionName                NVARCHAR(120) NOT NULL,

	-- ── Limitations periods (years). ──
	PersonalInjuryLimitationYears   DECIMAL(4,1) NULL,
	MedicalMalpracticeLimitationYears DECIMAL(4,1) NULL,
	WrongfulDeathLimitationYears    DECIMAL(4,1) NULL,
	-- Discovery-rule / accrual caveat text (e.g. "Discovery rule applies; statute of repose 10y").
	DiscoveryRuleNote               NVARCHAR(600) NULL,
	-- Minors tolling summary (e.g. "Tolled until age 18").
	MinorsTollingNote               NVARCHAR(600) NULL,

	-- ── Negligence / fault regime. ──
	-- PURE_COMPARATIVE | MODIFIED_50 | MODIFIED_51 | CONTRIBUTORY
	NegligenceRegimeCode            NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionJurisdictionRule_Regime DEFAULT N'MODIFIED_51',
	NegligenceRegimeNote            NVARCHAR(600) NULL,
	-- Joint-and-several liability summary.
	JointSeveralLiabilityNote       NVARCHAR(600) NULL,

	-- ── Specialized liability doctrines. ──
	DramShopNote                    NVARCHAR(600) NULL,
	-- Government / tort-claims-act notice deadline summary (e.g. "6 months written notice").
	GovernmentClaimNoticeNote       NVARCHAR(600) NULL,

	-- ── Damages caps. ──
	-- Non-economic / general damages cap summary (text; many states have none or MedMal-only caps).
	NonEconomicDamagesCapNote       NVARCHAR(800) NULL,
	-- Punitive-damages standard and cap summary.
	PunitiveDamagesStandardNote     NVARCHAR(800) NULL,
	-- Medical-malpractice-specific requirements (affidavit/certificate of merit, pre-suit panel, caps).
	MedicalMalpracticeNote          NVARCHAR(800) NULL,

	-- ── Provenance / governance. ──
	SourceNote                      NVARCHAR(1000) NULL,
	EffectiveDate                   DATE NULL,
	IsAdvisoryOnly                  BIT NOT NULL CONSTRAINT DF_Legal_DecisionJurisdictionRule_Advisory DEFAULT 1,

	SortOrder                       INT NOT NULL CONSTRAINT DF_Legal_DecisionJurisdictionRule_SortOrder DEFAULT 0,
	IsActive                        BIT NOT NULL CONSTRAINT DF_Legal_DecisionJurisdictionRule_IsActive DEFAULT 1,
	TenantId                        UNIQUEIDENTIFIER NULL,
	CreatedDateUtc                  DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionJurisdictionRule_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId                 UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc                 DATETIME2 NULL,
	ModifiedByUserId                UNIQUEIDENTIFIER NULL,
	IsDeleted                       BIT NOT NULL CONSTRAINT DF_Legal_DecisionJurisdictionRule_IsDeleted DEFAULT 0
);

GO

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionJurisdictionRule') AND name = N'IX_Legal_DecisionJurisdictionRule_Lookup')
	CREATE INDEX IX_Legal_DecisionJurisdictionRule_Lookup
		ON POLOXI.Legal_DecisionJurisdictionRule (PracticeAreaCode, JurisdictionCode, IsActive, SortOrder) WHERE IsDeleted = 0;

GO

-- ── Global seed: 50 US states + DC (TenantId NULL, commonly-cited general rules). ──
-- Idempotent: inserts only global rows whose JurisdictionCode is not already present.
-- Values are commonly-cited general rules and ADVISORY ONLY (not legal advice); accrual,
-- tolling, exceptions, and inflation-adjusted caps vary by claim type and change over time.
;WITH Seed AS
(
	SELECT * FROM (VALUES
	-- Code, Name, PI_SOL, MedMal_SOL, WD_SOL, Discovery, Minors, Regime, RegimeNote, JointSeveral, DramShop, GovtNotice, NonEconCap, PunitiveStd, MedMalNote
	 (N'AL', N'Alabama',               2.0, 2.0, 2.0, N'Discovery rule limited; 2y from accrual.', N'Tolled for minors under 19 (limited).', N'CONTRIBUTORY', N'Pure contributory negligence bars recovery if plaintiff at fault.', N'Several liability generally.', N'Dram shop liability recognized by statute.', N'Government claims under Board of Adjustment; short notice periods.', N'No general cap; punitive caps apply.', N'Clear and convincing; punitive caps (generally greater of 3x or $1.5M).', N'Affidavit not required; expert testimony standards apply.')
	,(N'AK', N'Alaska',                2.0, 2.0, 2.0, N'Discovery rule applies.', N'Tolled during minority.', N'PURE_COMPARATIVE', N'Pure comparative fault.', N'Several liability (apportioned).', N'Dram shop liability recognized.', N'Tort Claims Act notice requirements apply.', N'Non-economic cap (generally $400K or formula).', N'Clear and convincing; statutory limits.', N'Expert affidavit may be required.')
	,(N'AZ', N'Arizona',               2.0, 2.0, 2.0, N'Discovery rule applies.', N'Tolled until age 18.', N'PURE_COMPARATIVE', N'Pure comparative fault.', N'Several liability only (no joint liability generally).', N'Dram shop liability recognized.', N'180-day notice of claim against public entities.', N'No cap (Arizona Constitution bars damages caps).', N'Clear and convincing; no statutory cap.', N'Preliminary expert opinion affidavit required.')
	,(N'AR', N'Arkansas',              3.0, 2.0, 3.0, N'Discovery rule limited.', N'Tolled during minority.', N'MODIFIED_50', N'Modified comparative (barred if 50% or more at fault).', N'Several liability.', N'Dram shop liability limited.', N'Government notice requirements apply.', N'No general cap.', N'Clear and convincing; statutory caps.', N'Affidavit of merit not generally required.')
	,(N'CA', N'California',            2.0, 3.0, 2.0, N'Discovery rule applies; MedMal 1y from discovery/3y outer (MICRA).', N'Tolled until age 18 (6-month post-majority limits in some cases).', N'PURE_COMPARATIVE', N'Pure comparative fault (Li v. Yellow Cab).', N'Joint for economic; several for non-economic (Prop 51).', N'Civil Code 1714 limits social-host/commercial liability.', N'Government Claims Act: 6-month notice for injury claims.', N'MICRA non-economic cap (phasing up from $350K/$500K per 2023 reform).', N'Clear and convincing evidence required.', N'MICRA governs; 90-day notice of intent to sue before MedMal filing.')
	,(N'CO', N'Colorado',             2.0, 2.0, 2.0, N'Discovery rule applies; 3y for motor vehicle.', N'Tolled during minority.', N'MODIFIED_50', N'Modified comparative (barred if 50% or more).', N'Several liability (pro rata).', N'Dram shop liability capped by statute.', N'Governmental Immunity Act: 182-day notice.', N'Non-economic damages capped by statute (inflation-adjusted).', N'Punitive limited to actual damages; clear and convincing.', N'Certificate of review required for professional negligence.')
	,(N'CT', N'Connecticut',          2.0, 2.0, 2.0, N'2y from injury, 3y repose.', N'Tolled during minority.', N'MODIFIED_51', N'Modified comparative (barred if more than 50%).', N'Joint and several with reallocation.', N'Dram Shop Act with statutory cap.', N'Notice requirements for municipal/state claims.', N'No general cap.', N'Punitive limited to litigation expenses (common law).', N'Good-faith certificate and written opinion required.')
	,(N'DE', N'Delaware',             2.0, 2.0, 2.0, N'Discovery rule limited.', N'Tolled during minority.', N'MODIFIED_51', N'Modified comparative (barred if more than 50%).', N'Joint and several liability.', N'No dram shop civil liability (generally).', N'Government claims notice requirements apply.', N'No general cap.', N'Clear and convincing standard.', N'Affidavit of merit required for medical negligence.')
	,(N'FL', N'Florida',              2.0, 2.0, 2.0, N'2y PI (reduced from 4y by 2023 reform); discovery for MedMal.', N'Tolled during minority (limits apply).', N'MODIFIED_51', N'Modified comparative since 2023 (barred if more than 50%).', N'Several liability (Fabre).', N'Dram shop liability limited (willful service to minors/habituals).', N'Pre-suit notice for government claims.', N'No general cap (MedMal cap struck down).', N'Clear and convincing; 3x/$500K caps (with exceptions).', N'Pre-suit investigation and corroborating expert affidavit required.')
	,(N'GA', N'Georgia',              2.0, 2.0, 2.0, N'Discovery rule limited; 5y MedMal repose.', N'Tolled until age 18 (limits).', N'MODIFIED_50', N'Modified comparative (barred if 50% or more).', N'Several liability (apportionment).', N'Dram shop liability recognized.', N'Ante litem notice (6 months municipal / 12 months state).', N'No general cap (MedMal cap struck down).', N'Clear and convincing; $250K cap (exceptions).', N'Expert affidavit required with complaint.')
	,(N'HI', N'Hawaii',               2.0, 2.0, 2.0, N'Discovery rule applies.', N'Tolled during minority.', N'MODIFIED_51', N'Modified comparative (barred if more than 50%).', N'Joint and several (limited by statute).', N'Dram shop liability recognized.', N'Government claims notice requirements apply.', N'Non-economic cap in some cases ($375K pain and suffering).', N'Clear and convincing standard.', N'Medical inquiry and conciliation panel pre-suit process.')
	,(N'ID', N'Idaho',                2.0, 2.0, 2.0, N'Discovery rule limited.', N'Tolled during minority.', N'MODIFIED_50', N'Modified comparative (barred if 50% or more).', N'Several liability.', N'Dram shop liability limited.', N'Tort Claims Act: 180-day notice.', N'Non-economic damages cap (inflation-adjusted).', N'Clear and convincing; punitive capped.', N'Pre-litigation screening panel required.')
	,(N'IL', N'Illinois',             2.0, 2.0, 2.0, N'Discovery rule applies; 4y MedMal repose.', N'Tolled until age 18 (8y/age 22 for MedMal).', N'MODIFIED_51', N'Modified comparative (barred if more than 50%).', N'Joint and several for economic; several for non-economic.', N'Dram Shop Act with caps.', N'1-year notice/limitations for local government.', N'No cap (MedMal cap struck down 2010).', N'Punitive not allowed in MedMal/healing-arts; clear and convincing.', N'Affidavit and physician report (Section 2-622) required.')
	,(N'IN', N'Indiana',              2.0, 2.0, 2.0, N'Discovery rule limited.', N'Tolled until age 18 (MedMal to age 8).', N'MODIFIED_51', N'Modified comparative (barred if more than 50%); govt is contributory.', N'Several liability.', N'Dram shop liability recognized.', N'Tort Claims Act: 180-day/270-day notice.', N'Medical Malpractice Act total cap on damages.', N'Clear and convincing; punitive capped (3x or $50K).', N'Proposed complaint to Medical Review Panel required.')
	,(N'IA', N'Iowa',                 2.0, 2.0, 2.0, N'Discovery rule applies; 6y repose.', N'Tolled during minority (age 10 for MedMal).', N'MODIFIED_51', N'Modified comparative (barred if more than 50%).', N'Joint and several (economic, if 50%+).', N'Dram shop liability with notice requirement.', N'Government claims procedures apply.', N'MedMal non-economic cap ($250K, exceptions).', N'Clear and convincing; apportionment rules.', N'Certificate of merit affidavit required.')
	,(N'KS', N'Kansas',               2.0, 2.0, 2.0, N'Discovery rule applies; 4y repose.', N'Tolled during minority (limits).', N'MODIFIED_50', N'Modified comparative (barred if 50% or more).', N'Several liability only.', N'Dram shop liability not generally recognized.', N'Notice requirements for government claims.', N'Statutory non-economic cap (upheld/modified).', N'Clear and convincing; punitive caps.', N'Screening panel may be requested.')
	,(N'KY', N'Kentucky',             1.0, 1.0, 1.0, N'1y PI; discovery rule applies.', N'Tolled during minority.', N'PURE_COMPARATIVE', N'Pure comparative fault.', N'Several liability (apportionment).', N'Dram shop liability recognized.', N'Government claims notice requirements apply.', N'No cap (constitutional bar on damages caps).', N'Clear and convincing standard.', N'No certificate of merit required (MedMal review repealed).')
	,(N'LA', N'Louisiana',            1.0, 1.0, 1.0, N'1y liberative prescription; discovery (contra non valentem).', N'Prescription suspended during minority.', N'PURE_COMPARATIVE', N'Pure comparative fault.', N'Several liability (CC art. 2324).', N'Dram shop liability limited by statute.', N'Government claims procedures apply.', N'Medical Malpractice Act total cap ($500K + future care).', N'Punitive only where authorized by statute.', N'Medical Review Panel required before suit.')
	,(N'ME', N'Maine',                6.0, 3.0, 2.0, N'6y general PI; 3y MedMal.', N'Tolled during minority.', N'MODIFIED_50', N'Modified comparative (reduced; barred if equal to/greater than defendant fault).', N'Joint and several liability.', N'Dram shop (Liquor Liability Act) with caps.', N'Tort Claims Act: 180-day notice.', N'Governmental/MedMal caps in some cases.', N'Clear and convincing standard.', N'Pre-litigation screening panel required.')
	,(N'MD', N'Maryland',             3.0, 5.0, 3.0, N'3y general; MedMal 5y/3y-from-discovery.', N'Tolled during minority.', N'CONTRIBUTORY', N'Pure contributory negligence bars recovery.', N'Joint and several liability.', N'No dram shop civil liability (generally).', N'Local Government Tort Claims Act notice (1 year).', N'Statutory non-economic cap (annually adjusted).', N'Actual malice; clear and convincing.', N'Certificate of qualified expert required.')
	,(N'MA', N'Massachusetts',        3.0, 3.0, 3.0, N'Discovery rule applies; 7y MedMal repose.', N'Tolled during minority (MedMal limits).', N'MODIFIED_51', N'Modified comparative (barred if more than 50%).', N'Joint and several with contribution.', N'Dram shop liability recognized.', N'Tort Claims Act: 2-year presentment.', N'MedMal non-economic cap ($500K, exceptions).', N'Punitive only by statute (e.g., wrongful death).', N'Tribunal review; offer of proof required.')
	,(N'MI', N'Michigan',             3.0, 2.0, 3.0, N'3y general; MedMal 2y/6-month discovery.', N'Tolled during minority (1-year grace).', N'MODIFIED_51', N'Modified comparative (non-economic barred if more than 50%).', N'Several liability (fair-share).', N'Dram shop liability with name-and-retain rule.', N'Government notice requirements apply.', N'MedMal non-economic cap (inflation-adjusted tiers).', N'Punitive (exemplary) limited; clear standard.', N'Affidavit of merit and notice of intent required.')
	,(N'MN', N'Minnesota',            2.0, 4.0, 3.0, N'2y general PI; 4y MedMal.', N'Tolled during minority (limits).', N'MODIFIED_51', N'Modified comparative (barred if greater than 50%).', N'Joint and several (reallocation thresholds).', N'Dram shop (Civil Damages Act) with notice.', N'Government notice requirements apply.', N'No general cap.', N'Clear and convincing; separate motion to add claim.', N'Expert affidavit of merit required.')
	,(N'MS', N'Mississippi',          3.0, 2.0, 3.0, N'3y general; MedMal 2y/7y repose.', N'Tolled during minority (limits).', N'PURE_COMPARATIVE', N'Pure comparative fault.', N'Several liability only.', N'Dram shop liability limited.', N'Tort Claims Act: 90-day notice.', N'Non-economic cap ($1M general; MedMal $500K).', N'Clear and convincing; punitive caps by net worth.', N'Pre-suit notice (60 days) and consultation certificate.')
	,(N'MO', N'Missouri',             5.0, 2.0, 3.0, N'5y general PI; MedMal 2y.', N'Tolled during minority (limits).', N'PURE_COMPARATIVE', N'Pure comparative fault.', N'Several liability (except certain conduct).', N'Dram shop liability with clear-and-convincing proof.', N'Sovereign immunity notice requirements apply.', N'MedMal non-economic cap (inflation-adjusted).', N'Clear and convincing; statutory caps.', N'Affidavit that legally qualified expert consulted.')
	,(N'MT', N'Montana',              3.0, 3.0, 3.0, N'Discovery rule applies; 5y MedMal repose.', N'Tolled during minority (MedMal to age 8).', N'MODIFIED_51', N'Modified comparative (barred if greater than 50%).', N'Several liability (apportionment).', N'Dram shop liability recognized.', N'Government claims procedures apply.', N'MedMal non-economic cap ($250K).', N'Clear and convincing; punitive caps.', N'Medical Legal Panel application required before suit.')
	,(N'NE', N'Nebraska',             4.0, 2.0, 2.0, N'4y general PI; MedMal 2y/10y repose.', N'Tolled during minority (limits).', N'MODIFIED_50', N'Modified comparative (barred if equal to/greater than defendant fault).', N'Several liability for non-economic.', N'Dram shop liability limited.', N'Political Subdivisions Tort Claims Act notice.', N'Hospital-Medical Liability Act total cap.', N'Punitive damages generally not allowed.', N'Review panel optional; qualified provider cap applies.')
	,(N'NV', N'Nevada',               2.0, 3.0, 2.0, N'2y general; MedMal 3y/1y discovery.', N'Tolled during minority (MedMal limits).', N'MODIFIED_51', N'Modified comparative (barred if more than 50%).', N'Several liability (some exceptions joint).', N'Dram shop liability not generally recognized.', N'Government claims notice requirements apply.', N'MedMal non-economic cap ($350K, 2023 indexing).', N'Clear and convincing; punitive caps (3x/$300K).', N'Affidavit of medical expert required with complaint.')
	,(N'NH', N'New Hampshire',        3.0, 3.0, 3.0, N'Discovery rule applies.', N'Tolled during minority.', N'MODIFIED_51', N'Modified comparative (barred if greater than 50%).', N'Several liability (if less than 50%).', N'Dram shop liability recognized.', N'Government claims notice requirements apply.', N'No general cap (cap struck down).', N'Clear and convincing; punitive generally barred (enhanced compensatory).', N'No certificate of merit; expert disclosure rules.')
	,(N'NJ', N'New Jersey',           2.0, 2.0, 2.0, N'Discovery rule applies.', N'Tolled during minority.', N'MODIFIED_51', N'Modified comparative (barred if more than 50%).', N'Joint and several (60%+ threshold).', N'Dram shop (Licensed Alcoholic Beverage Server Fair Liability Act).', N'Tort Claims Act: 90-day notice, $3,600 verbal-threshold for public entity pain/suffering.', N'No general cap.', N'Punitive Damages Act: clear and convincing; 5x/$350K cap.', N'Affidavit of merit required within 60/120 days.')
	,(N'NM', N'New Mexico',           3.0, 3.0, 3.0, N'Discovery rule applies.', N'Tolled during minority (MedMal to age 6).', N'PURE_COMPARATIVE', N'Pure comparative fault.', N'Several liability only.', N'Dram shop (Liquor Liability Act) with caps.', N'Tort Claims Act: 90-day notice.', N'MedMal cap under Medical Malpractice Act (excluding certain care).', N'Clear and convincing standard.', N'Medical Review Commission application required.')
	,(N'NY', N'New York',             3.0, 2.5, 2.0, N'3y general PI; MedMal 2.5y; continuous treatment & foreign-object discovery.', N'Tolled during infancy (10-year cap).', N'PURE_COMPARATIVE', N'Pure comparative fault (CPLR 1411).', N'Joint and several (CPLR Art. 16 limits non-economic to several if 50% or less).', N'Dram Shop Act (General Obligations Law 11-101).', N'Notice of claim 90 days for municipalities.', N'No general cap.', N'No statutory cap; common-law reasonableness.', N'Certificate of merit required for MedMal.')
	,(N'NC', N'North Carolina',       3.0, 3.0, 2.0, N'3y general; MedMal 4y repose.', N'Tolled during minority (limits).', N'CONTRIBUTORY', N'Pure contributory negligence bars recovery.', N'Joint and several liability.', N'Dram shop liability recognized.', N'Government claims notice/procedures apply.', N'MedMal non-economic cap (inflation-adjusted).', N'Clear and convincing; punitive cap (3x/$250K).', N'Rule 9(j) expert review certification required.')
	,(N'ND', N'North Dakota',         6.0, 2.0, 2.0, N'6y general PI; MedMal 2y/6y repose.', N'Tolled during minority (MedMal to age 12).', N'MODIFIED_50', N'Modified comparative (barred if 50% or more).', N'Several liability only.', N'Dram shop liability recognized.', N'Political subdivision notice requirements apply.', N'MedMal non-economic cap ($500K).', N'Clear and convincing; punitive caps (2x/$250K).', N'Expert affidavit required within 3 months.')
	,(N'OH', N'Ohio',                 2.0, 1.0, 2.0, N'2y general; MedMal 1y/4y repose.', N'Tolled during minority (limits).', N'MODIFIED_51', N'Modified comparative (barred if more than 50%).', N'Joint and several for economic (50%+).', N'Dram shop (Liquor Control) liability.', N'Political subdivision notice/immunity rules.', N'Non-economic damages cap (greater of $250K or 3x economic to $350K/$500K).', N'Clear and convincing; punitive cap (2x compensatory).', N'Affidavit of merit required with complaint.')
	,(N'OK', N'Oklahoma',             2.0, 2.0, 2.0, N'Discovery rule applies.', N'Tolled during minority (limits).', N'MODIFIED_51', N'Modified comparative (barred if greater than 50%).', N'Several liability only.', N'Dram shop liability recognized.', N'Governmental Tort Claims Act: 1-year notice.', N'No general cap (non-economic cap struck down 2019).', N'Clear and convincing; punitive caps by category.', N'Affidavit of merit requirements have varied (struck/reenacted).')
	,(N'OR', N'Oregon',               2.0, 2.0, 3.0, N'Discovery rule applies; 5y MedMal repose.', N'Tolled during minority (limits).', N'MODIFIED_51', N'Modified comparative (barred if greater than 50%).', N'Several liability (apportionment).', N'Dram shop liability with notice.', N'Tort Claims Act: 180-day notice.', N'Non-economic cap in some cases (wrongful death/OTCA).', N'Clear and convincing; 70% punitive to state fund.', N'No certificate of merit; expert rules apply.')
	,(N'PA', N'Pennsylvania',         2.0, 2.0, 2.0, N'Discovery rule applies; 7y MedMal repose.', N'Tolled during minority (MedMal to age 20).', N'MODIFIED_51', N'Modified comparative (barred if more than 50%).', N'Several liability (Fair Share Act; joint if 60%+).', N'Dram shop (Liquor Code) liability.', N'6-month notice for Commonwealth/local claims.', N'No general cap (MCARE limits punitive).', N'Clear and convincing; punitive cap (200% in MedMal).', N'Certificate of merit required (Rule 1042.3).')
	,(N'RI', N'Rhode Island',         3.0, 3.0, 3.0, N'Discovery rule applies.', N'Tolled during minority.', N'PURE_COMPARATIVE', N'Pure comparative fault.', N'Joint and several liability.', N'Dram shop (Liquor Liability Act) with caps.', N'Government claims procedures apply.', N'No general cap.', N'Clear and convincing standard.', N'No certificate of merit; expert rules apply.')
	,(N'SC', N'South Carolina',       3.0, 3.0, 3.0, N'Discovery rule applies; 6y MedMal repose.', N'Tolled during minority (limits).', N'MODIFIED_51', N'Modified comparative (barred if more than 50%).', N'Joint and several (50%+ threshold).', N'Dram shop liability recognized.', N'Tort Claims Act notice/limits apply.', N'MedMal non-economic cap (inflation-adjusted).', N'Clear and convincing; punitive cap (3x/$500K).', N'Notice of intent and expert affidavit required.')
	,(N'SD', N'South Dakota',         3.0, 2.0, 3.0, N'3y general PI; MedMal 2y repose.', N'Tolled during minority (limits).', N'MODIFIED_SLIGHT', N'Modified comparative: recovery only if plaintiff negligence is slight.', N'Joint and several liability.', N'Dram shop liability limited.', N'Government claims notice requirements apply.', N'MedMal total cap ($500K).', N'Clear and convincing standard.', N'No certificate of merit; expert rules apply.')
	,(N'TN', N'Tennessee',            1.0, 1.0, 1.0, N'1y general PI; MedMal 1y/3y repose.', N'Tolled during minority (limits).', N'MODIFIED_50', N'Modified comparative (barred if 50% or more).', N'Several liability only.', N'Dram shop liability limited (proximate cause).', N'Government Tort Liability Act notice/limits.', N'Non-economic cap ($750K/$1M catastrophic).', N'Clear and convincing; punitive cap (2x/$500K).', N'Pre-suit notice (60 days) and certificate of good faith.')
	,(N'TX', N'Texas',                2.0, 2.0, 2.0, N'Discovery rule limited; MedMal 2y/10y repose.', N'Tolled during minority (MedMal to age 12).', N'MODIFIED_51', N'Modified comparative (barred if more than 50%).', N'Several liability (joint if 50%+).', N'Dram Shop Act with safe-harbor.', N'Tort Claims Act: 6-month notice (shorter by charter).', N'MedMal non-economic cap ($250K/provider tiers).', N'Clear and convincing; punitive cap (2x econ + $750K / $200K).', N'Expert report required within 120 days (Ch. 74).')
	,(N'UT', N'Utah',                 4.0, 2.0, 2.0, N'4y general PI; MedMal 2y/4y repose.', N'Tolled during minority (limits).', N'MODIFIED_50', N'Modified comparative (barred if 50% or more).', N'Several liability only.', N'Dram shop (Alcoholic Beverage) liability with caps.', N'Governmental Immunity Act: 1-year notice.', N'MedMal non-economic cap (inflation-adjusted).', N'Clear and convincing standard.', N'Pre-litigation panel review and affidavit of merit required.')
	,(N'VT', N'Vermont',              3.0, 3.0, 2.0, N'3y general; MedMal 3y/7y repose.', N'Tolled during minority.', N'MODIFIED_51', N'Modified comparative (barred if greater than defendant).', N'Several liability (apportionment).', N'Dram Shop Act liability.', N'Government claims procedures apply.', N'No general cap.', N'Clear and convincing standard.', N'Certificate of merit required.')
	,(N'VA', N'Virginia',             2.0, 2.0, 2.0, N'2y; MedMal discovery limited; 10y repose.', N'Tolled during minority (limits).', N'CONTRIBUTORY', N'Pure contributory negligence bars recovery.', N'Joint and several liability.', N'No dram shop civil liability (generally).', N'Government claims notice (1 year for localities/state).', N'MedMal total cap (annually increasing statutory amount).', N'$350K punitive cap.', N'Expert certification of merit required at service.')
	,(N'WA', N'Washington',           3.0, 3.0, 3.0, N'Discovery rule applies; 8y MedMal repose.', N'Tolled during minority (MedMal limits).', N'PURE_COMPARATIVE', N'Pure comparative fault.', N'Several liability (joint if acting in concert / plaintiff fault-free).', N'Dram shop liability (apparent intoxication).', N'60-day pre-suit notice for government claims.', N'No cap (cap struck down; punitive generally not allowed).', N'Punitive damages generally not permitted.', N'Certificate of merit requirement struck down; expert rules apply.')
	,(N'WV', N'West Virginia',        2.0, 2.0, 2.0, N'Discovery rule applies; 10y MedMal repose.', N'Tolled during minority (MedMal to age 10).', N'MODIFIED_51', N'Modified comparative (barred if more than 50%).', N'Several liability (joint if 30%+).', N'Dram shop liability recognized.', N'Government claims procedures apply.', N'MedMal non-economic cap ($250K/$500K indexed).', N'Clear and convincing; punitive cap (4x/$500K).', N'Pre-suit notice (30 days) and screening certificate of merit.')
	,(N'WI', N'Wisconsin',            3.0, 3.0, 3.0, N'3y general; MedMal 3y/1y discovery/5y repose.', N'Tolled during minority (MedMal to age 10).', N'MODIFIED_51', N'Modified comparative (barred if greater than defendant).', N'Joint and several (51%+ threshold).', N'Dram shop liability limited (minors).', N'120-day notice of injury for government claims.', N'MedMal non-economic cap ($750K, upheld).', N'Clear and convincing; punitive cap (2x/$200K).', N'No certificate of merit; mediation panel request available.')
	,(N'WY', N'Wyoming',              4.0, 2.0, 2.0, N'4y general PI; MedMal 2y.', N'Tolled during minority (limits).', N'MODIFIED_51', N'Modified comparative (barred if more than 50%).', N'Several liability only.', N'Dram shop liability limited.', N'Governmental Claims Act: 2-year/notice requirements.', N'No cap (constitutional bar on damages caps).', N'Clear and convincing standard.', N'Medical Review Panel (optional/waivable).')
	,(N'DC', N'District of Columbia', 3.0, 3.0, 2.0, N'3y general PI; discovery rule applies.', N'Tolled during minority.', N'CONTRIBUTORY', N'Pure contributory negligence bars recovery (modified comparative for cyclists/pedestrians).', N'Joint and several liability.', N'Dram shop liability recognized.', N'Notice to Mayor within 6 months for DC claims.', N'No general cap.', N'Clear and convincing standard.', N'90-day pre-suit notice required for MedMal.')
	) AS v(JurisdictionCode, JurisdictionName, PILimit, MedMalLimit, WDLimit, DiscoveryRuleNote, MinorsTollingNote, NegligenceRegimeCode, NegligenceRegimeNote, JointSeveralLiabilityNote, DramShopNote, GovernmentClaimNoticeNote, NonEconomicDamagesCapNote, PunitiveDamagesStandardNote, MedicalMalpracticeNote)
)
INSERT INTO POLOXI.Legal_DecisionJurisdictionRule
	(PracticeAreaCode, JurisdictionCode, JurisdictionName,
	 PersonalInjuryLimitationYears, MedicalMalpracticeLimitationYears, WrongfulDeathLimitationYears,
	 DiscoveryRuleNote, MinorsTollingNote, NegligenceRegimeCode, NegligenceRegimeNote,
	 JointSeveralLiabilityNote, DramShopNote, GovernmentClaimNoticeNote,
	 NonEconomicDamagesCapNote, PunitiveDamagesStandardNote, MedicalMalpracticeNote,
	 SourceNote, EffectiveDate, IsAdvisoryOnly, SortOrder, IsActive, TenantId)
SELECT N'PERSONAL_INJURY', s.JurisdictionCode, s.JurisdictionName,
	s.PILimit, s.MedMalLimit, s.WDLimit,
	s.DiscoveryRuleNote, s.MinorsTollingNote, s.NegligenceRegimeCode, s.NegligenceRegimeNote,
	s.JointSeveralLiabilityNote, s.DramShopNote, s.GovernmentClaimNoticeNote,
	s.NonEconomicDamagesCapNote, s.PunitiveDamagesStandardNote, s.MedicalMalpracticeNote,
	N'Commonly-cited general rule. ADVISORY ONLY and NOT legal advice: accrual, tolling, exceptions, and inflation-adjusted caps vary by claim type and change over time. Independently verify against current statutes and case law.',
	'2024-01-01', 1, 0, 1, NULL
FROM Seed s
WHERE NOT EXISTS
(
	SELECT 1 FROM POLOXI.Legal_DecisionJurisdictionRule r
	WHERE r.PracticeAreaCode = N'PERSONAL_INJURY' AND r.JurisdictionCode = s.JurisdictionCode AND r.TenantId IS NULL
);

GO

COMMIT TRANSACTION;
GO
