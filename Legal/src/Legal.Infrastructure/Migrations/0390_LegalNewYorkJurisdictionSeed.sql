-- ============================================================================
-- 0390: New York jurisdiction seed data.
--
-- WHY: The platform only shipped a single flat "New York" jurisdiction dropdown
-- value (0211) and a federal/UCC-only concept->authority map (0184). Matters
-- venued in New York (for example the Personal Injury test matter) therefore had
-- no New-York-specific court options to pick from and no New York statutory
-- authorities for the LEGAL grounding pipeline to resolve concepts against.
--
-- This migration seeds, into the existing DB-backed reference tables (DB is the
-- source of truth; no hardcoded arrays), the New York jurisdiction data:
--   1. POLOXI.Legal_DecisionMatterOption  - New York court/venue dropdown values.
--   2. POLOXI.Legal_LegalConceptAuthority  - New York statutory authorities
--      (CPLR / General Obligations Law / Insurance Law) for concept grounding.
--
-- All rows are platform-scope (TenantId NULL) and tenant-overridable. Both
-- MERGEs key on natural business keys so the migration is idempotent / re-runnable.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'POLOXI') EXEC(N'CREATE SCHEMA POLOXI');

-- ───────────────────────────────────────────────────────────────────────────
-- 1. New York court / venue dropdown options (JURISDICTION field).
--    Keyed on FieldCode + Value (matches UQ_Legal_DecisionMatterOption_Field_Value).
--    SortOrder 200+ keeps them grouped after the existing generic jurisdictions.
-- ───────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_DecisionMatterOption', N'U') IS NOT NULL
BEGIN
	MERGE POLOXI.Legal_DecisionMatterOption AS target
	USING (VALUES
		(N'JURISDICTION', N'New York - Court of Appeals',                     200),
		(N'JURISDICTION', N'New York - Appellate Division, First Department',  210),
		(N'JURISDICTION', N'New York - Appellate Division, Second Department', 220),
		(N'JURISDICTION', N'New York - Appellate Division, Third Department',  230),
		(N'JURISDICTION', N'New York - Appellate Division, Fourth Department', 240),
		(N'JURISDICTION', N'New York - Supreme Court, New York County',        250),
		(N'JURISDICTION', N'New York - Supreme Court, Kings County',           260),
		(N'JURISDICTION', N'New York - Supreme Court, Queens County',          270),
		(N'JURISDICTION', N'New York - Supreme Court, Bronx County',           280),
		(N'JURISDICTION', N'New York - Supreme Court, Nassau County',          290),
		(N'JURISDICTION', N'New York - Supreme Court, Suffolk County',         300),
		(N'JURISDICTION', N'New York - Supreme Court, Westchester County',     310),
		(N'JURISDICTION', N'E.D.N.Y.',                                         320),
		(N'JURISDICTION', N'N.D.N.Y.',                                         330),
		(N'JURISDICTION', N'W.D.N.Y.',                                         340)
	) AS source (FieldCode, Value, SortOrder)
	ON target.FieldCode = source.FieldCode AND target.Value = source.Value
	WHEN NOT MATCHED BY TARGET THEN
		INSERT (FieldCode, Value, DisplayName, SortOrder)
		VALUES (source.FieldCode, source.Value, source.Value, source.SortOrder);
END;

-- ───────────────────────────────────────────────────────────────────────────
-- 2. New York statutory authorities for concept-driven LEGAL grounding.
--    Keyed on TenantId IS NULL + ConceptKeywords + CitationText (matches
--    UX_Legal_LegalConceptAuthority_TenantConceptCitation). Covers the common
--    personal-injury / civil-practice doctrines that otherwise ground to nothing
--    in a New York matter. VerificationTokens are distinctive numeric tokens a
--    retrieved source must contain so a wrong mapping fails identity verification.
-- ───────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_LegalConceptAuthority', N'U') IS NOT NULL
BEGIN
	MERGE POLOXI.Legal_LegalConceptAuthority AS target
	USING (VALUES
		-- CPLR - Civil Practice Law and Rules
		-- CitationText spells out the state + full code name (exactly like the working
		-- California rows: "California Code of Civil Procedure § 998") so the jurisdiction
		-- detector resolves "New York" -> NAME:NEW YORK and the Section 3 descriptor
		-- CitationPatterns match. VerificationTokens is the distinctive section number.
		(N'statute limitations personal injury', N'New York Civil Practice Law and Rules § 214',   N'Statute', N'214',    N'NY CPLR',  N'Three-year limitation for personal injury actions'),
		(N'statute limitations negligence',      N'New York Civil Practice Law and Rules § 214',   N'Statute', N'214',    N'NY CPLR',  N'Three-year limitation for negligence actions'),
		(N'statute limitations medical malpractice', N'New York Civil Practice Law and Rules § 214-a', N'Statute', N'214-a', N'NY CPLR', N'Two-and-a-half-year limitation for medical, dental or podiatric malpractice'),
		(N'notice of claim',                     N'New York Civil Practice Law and Rules § 217',   N'Statute', N'217',    N'NY CPLR',  N'Four-month limitation for Article 78 proceedings'),
		(N'comparative negligence',              N'New York Civil Practice Law and Rules § 1411',  N'Statute', N'1411',   N'NY CPLR',  N'Pure comparative negligence; damages diminished by claimant fault'),
		(N'comparative fault',                   N'New York Civil Practice Law and Rules § 1411',  N'Statute', N'1411',   N'NY CPLR',  N'Pure comparative fault reduces but does not bar recovery'),
		(N'joint several liability',             N'New York Civil Practice Law and Rules § 1601',  N'Statute', N'1601',   N'NY CPLR',  N'Limited liability of persons jointly liable (non-economic loss)'),
		(N'summary judgment',                    N'New York Civil Practice Law and Rules § 3212',  N'Statute', N'3212',   N'NY CPLR',  N'Motion for summary judgment'),
		(N'serious injury threshold',            N'New York Insurance Law § 5102',                 N'Statute', N'5102',   N'NY Ins',   N'Definition of serious injury (no-fault threshold)'),
		(N'no fault threshold',                  N'New York Insurance Law § 5104',                 N'Statute', N'5104',   N'NY Ins',   N'Causes of action for personal injury (no-fault limitation on recovery)'),
		(N'general obligations release',         N'New York General Obligations Law § 15-108',     N'Statute', N'15-108', N'NY GOL',   N'Release or covenant not to sue; effect on joint tortfeasors'),
		(N'dram shop',                           N'New York General Obligations Law § 11-101',     N'Statute', N'11-101', N'NY GOL',   N'Dram shop liability for unlawful sale of alcoholic beverages'),
		(N'labor law scaffold',                  N'New York Labor Law § 240',                      N'Statute', N'240',    N'NY Labor', N'Scaffolding and elevation-related construction safety (absolute liability)'),
		(N'labor law construction safety',       N'New York Labor Law § 241',                      N'Statute', N'241',    N'NY Labor', N'Construction, excavation and demolition work safety requirements'),
		(N'wrongful death',                      N'New York Estates, Powers and Trusts Law § 5-4.1', N'Statute', N'5-4.1', N'NY EPTL', N'Action by personal representative for wrongful death')
	) AS source (ConceptKeywords, CitationText, AuthorityKindCode, VerificationTokens, SourceLabel, DisplayName)
	ON target.TenantId IS NULL AND target.ConceptKeywords = source.ConceptKeywords AND target.CitationText = source.CitationText AND target.IsDeleted = 0
	WHEN MATCHED THEN
		UPDATE SET
			target.AuthorityKindCode = source.AuthorityKindCode,
			target.VerificationTokens = source.VerificationTokens,
			target.SourceLabel = source.SourceLabel,
			target.DisplayName = source.DisplayName,
			target.IsActive = 1,
			target.ModifiedDateUtc = SYSUTCDATETIME()
	WHEN NOT MATCHED THEN
		INSERT (LegalConceptAuthorityId, TenantId, ConceptKeywords, CitationText, AuthorityKindCode, VerificationTokens, SourceLabel, DisplayName, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
		VALUES (NEWID(), NULL, source.ConceptKeywords, source.CitationText, source.AuthorityKindCode, source.VerificationTokens, source.SourceLabel, source.DisplayName, 1, 0, SYSUTCDATETIME(), 0);
END;

-- ───────────────────────────────────────────────────────────────────────────
-- 3. New York authority SOURCE descriptors — THE RETRIEVAL FIX.
--
--    ROOT CAUSE of "California retrieves but New York does not": California was
--    re-seeded in 0328 with ENABLED, per-code Legal_AuthoritySource descriptors
--    (CA_LEGINFO_*) whose CitationPattern + DocumentUrlTemplate let the pipeline
--    fetch the actual statute text from leginfo.legislature.ca.gov. New York only
--    has the single NY_CONS_LAWS row from 0327, which is IsEnabled = 0 (disabled)
--    and carries a generic, non-resolvable nysenate URL template — so NY citations
--    never retrieve a source and nothing reaches the admitted-evidence panel.
--
--    This section does for New York exactly what 0328/0329/0331 did for California:
--    seed ENABLED, per-law-code descriptors that MIRROR the working California
--    CA_FINDLAW_* rows — Justia static-HTML routes (law.justia.com/codes/new-york/…)
--    plus CitationPatterns that accept both the spelled-out and abbreviated citation
--    forms. The CitationText in Section 2 is spelled out ("New York Civil Practice
--    Law and Rules § 214") exactly like California ("California Code of Civil
--    Procedure § 998") so the jurisdiction detector resolves New York -> NAME:NEW
--    YORK and these descriptors match. Pipeline mechanics, admission gates, POLOXI
--    Core scoring, and the UI are UNCHANGED — this is DATA only.
--
--    Global (TenantId NULL). Idempotent via the Provider/Jurisdiction/Kind guard.
-- ───────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource', N'U') IS NOT NULL
BEGIN
	DECLARE @NySystemUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

	-- Retire the stale, disabled generic NY row so it can never win a broken match.
	UPDATE POLOXI.Legal_AuthoritySource
		SET IsEnabled = 0, ModifiedDateUtc = SYSUTCDATETIME()
		WHERE ProviderCode = N'NY_CONS_LAWS' AND JurisdictionCode = N'NAME:NEW YORK'
		  AND TenantId IS NULL AND IsDeleted = 0 AND IsEnabled = 1;

	DECLARE @NySeed TABLE
	(
		ProviderCode        NVARCHAR(80)   NOT NULL,
		JurisdictionCode    NVARCHAR(40)   NOT NULL,
		AuthorityKindCode   NVARCHAR(40)   NOT NULL,
		CitationPattern     NVARCHAR(1000) NOT NULL,
		BaseUrl             NVARCHAR(1000) NOT NULL,
		DocumentUrlTemplate NVARCHAR(2000) NOT NULL,
		SectionAnchorTemplate NVARCHAR(500) NULL,
		ExtractionStrategyCode NVARCHAR(40) NOT NULL,
		IsEnabled           BIT            NOT NULL
	);

	-- Per-law-code descriptors, mirroring the WORKING California CA_FINDLAW_* rows
	-- (migrations 0329/0331): Justia static-HTML routes and CitationPatterns that
	-- accept BOTH the spelled-out form ("New York Civil Practice Law and Rules §")
	-- seeded in Section 2 AND the abbreviated Bluebook form ("N.Y. C.P.L.R. §"),
	-- via the same \bN(?:ew\s+York|\.?Y\.?) alternation prefix style California uses.
	-- Justia NY law slugs: cvp (CPLR), isc (Insurance), gob (Gen. Oblig.),
	-- lab (Labor), ept (EPTL).
	INSERT @NySeed VALUES
	(N'NY_JUSTIA_CVP', N'NAME:NEW YORK', N'STATUTE', N'\bN(?:ew\s+York|\.?Y\.?)\s+(?:Civ(?:il)?\.?\s+Prac(?:tice)?\.?\s+Law\s*(?:and|&)?\s*Rules|C\.?P\.?L\.?R\.?)\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)', N'https://law.justia.com', N'codes/new-york/cvp/article-{section}/', NULL, N'FULL_PAGE_TEXT', 1),
	(N'NY_JUSTIA_ISC', N'NAME:NEW YORK', N'STATUTE', N'\bN(?:ew\s+York|\.?Y\.?)\s+Ins(?:urance)?\.?\s+Law\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)', N'https://law.justia.com', N'codes/new-york/isc/section-{section}/', NULL, N'FULL_PAGE_TEXT', 1),
	(N'NY_JUSTIA_GOB', N'NAME:NEW YORK', N'STATUTE', N'\bN(?:ew\s+York|\.?Y\.?)\s+Gen(?:eral)?\.?\s+Oblig(?:ations)?\.?\s+Law\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)', N'https://law.justia.com', N'codes/new-york/gob/section-{section}/', NULL, N'FULL_PAGE_TEXT', 1),
	(N'NY_JUSTIA_LAB', N'NAME:NEW YORK', N'STATUTE', N'\bN(?:ew\s+York|\.?Y\.?)\s+Lab(?:or)?\.?\s+Law\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)', N'https://law.justia.com', N'codes/new-york/lab/section-{section}/', NULL, N'FULL_PAGE_TEXT', 1),
	(N'NY_JUSTIA_EPT', N'NAME:NEW YORK', N'STATUTE', N'\bN(?:ew\s+York|\.?Y\.?)\s+(?:Est(?:ates)?\.?,?\s+Pow(?:ers)?\.?\s*(?:and|&)?\s*Tr(?:usts)?\.?\s+Law|E\.?P\.?T\.?L\.?)\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)', N'https://law.justia.com', N'codes/new-york/ept/section-{section}/', NULL, N'FULL_PAGE_TEXT', 1);

	INSERT POLOXI.Legal_AuthoritySource
	(
		LegalAuthoritySourceId, ProviderCode, JurisdictionCode, AuthorityKindCode, CitationPattern,
		BaseUrl, DocumentUrlTemplate, SectionAnchorTemplate, ExtractionStrategyCode,
		DiscoveryMethodCode, Priority, IsEnabled, TenantId, CreatedByUserId
	)
	SELECT
		NEWID(), s.ProviderCode, s.JurisdictionCode, s.AuthorityKindCode, s.CitationPattern,
		s.BaseUrl, s.DocumentUrlTemplate, s.SectionAnchorTemplate, s.ExtractionStrategyCode,
		N'MANUAL_SEED', 500, s.IsEnabled, NULL, @NySystemUserId
	FROM @NySeed s
	WHERE NOT EXISTS
	(
		SELECT 1 FROM POLOXI.Legal_AuthoritySource t
		WHERE t.ProviderCode = s.ProviderCode
		  AND t.JurisdictionCode = s.JurisdictionCode
		  AND t.AuthorityKindCode = s.AuthorityKindCode
		  AND t.TenantId IS NULL
		  AND t.IsDeleted = 0
	);

	-- Re-enable (and correct) any pre-existing rows from a prior partial run.
	UPDATE t
		SET t.IsEnabled = 1,
			t.CitationPattern = s.CitationPattern,
			t.BaseUrl = s.BaseUrl,
			t.DocumentUrlTemplate = s.DocumentUrlTemplate,
			t.ExtractionStrategyCode = s.ExtractionStrategyCode,
			t.ModifiedDateUtc = SYSUTCDATETIME()
	FROM POLOXI.Legal_AuthoritySource t
	INNER JOIN @NySeed s
		ON s.ProviderCode = t.ProviderCode
	   AND s.JurisdictionCode = t.JurisdictionCode
	   AND s.AuthorityKindCode = t.AuthorityKindCode
	WHERE t.TenantId IS NULL AND t.IsDeleted = 0 AND t.IsEnabled = 0;
END;

-- ───────────────────────────────────────────────────────────────────────────
-- 4. A complete New York Judz Matter running the full lifecycle pipeline.
--    Mirrors the Sapini seed (0387): a structured Legal_DecisionMatter + its
--    one-to-one PI Profile extension + a Judz Matter Lifecycle instance pinned
--    to PI_ADVANCED v1 (the default PI lifecycle). This gives a demonstrable
--    New-York-venued matter that exercises the court options and NY statutory
--    authorities seeded above, on the authoritative Judz workflow.
--    Tenant-scoped to the development demo tenant; deterministic ids
--    (A3000004-… namespace) keep the seed idempotent.
-- ───────────────────────────────────────────────────────────────────────────
DECLARE @NyTenant  UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @NyUser    UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @NyNow     DATETIME2        = SYSUTCDATETIME();
DECLARE @NyDOI     DATE             = N'2024-02-14';

DECLARE @NyMatter  UNIQUEIDENTIFIER = N'A3000004-0000-0000-0000-000000000001';
DECLARE @NyProfile UNIQUEIDENTIFIER = N'A3000004-0000-0000-0000-000000000002';
DECLARE @NyLife    UNIQUEIDENTIFIER = N'A3000004-0000-0000-0000-000000000010';

-- 3.1) Structured New York matter.
IF OBJECT_ID(N'POLOXI.Legal_DecisionMatter', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @NyMatter)
BEGIN
	INSERT POLOXI.Legal_DecisionMatter
		(DecisionMatterId, Title, MatterTypeCode, Jurisdiction, Posture, Description, StatusCode,
		 Subtype, CourtSystem, State, CourtLevel, County, GoverningLaw,
		 MovingParty, RespondingParty, MotionTarget, RequestedDisposition,
		 PracticeAreaCode, ClaimTypeCode,
		 TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES
		(@NyMatter,
		 N'Rivera v. Hudson Freight Lines - MVA (Grand Concourse, Bronx)',
		 N'Personal Injury',
		 N'Supreme Court, Bronx County - Governing law: New York (CPLR, Ins. Law no-fault)',
		 N'Pre-suit; treatment ongoing, serious-injury threshold at issue',
		 N'Type: Personal Injury (motor-vehicle accident).' + NCHAR(10)
		 + N'Jurisdiction: Supreme Court of the State of New York, Bronx County; governing law: New York.' + NCHAR(10)
		 + N'Posture: Pre-suit demand phase. Plaintiff Rivera rear-ended by a Hudson Freight Lines truck on the Grand Concourse; bilateral lumbar and cervical injuries.' + NCHAR(10)
		 + N'Core questions: whether injuries meet the Ins. Law § 5102(d) serious-injury threshold, comparative fault under CPLR § 1411, and the three-year CPLR § 214 limitations clock.',
		 N'OPEN',
		 N'Motor Vehicle Accident',
		 N'United States - State',
		 N'New York',
		 N'Supreme Court',
		 N'Bronx County',
		 N'New York',
		 N'Plaintiff Luis Rivera',
		 N'Defendant Hudson Freight Lines, Inc.',
		 N'Negligence / motor-vehicle accident',
		 N'Recovery of economic and non-economic damages',
		 N'PERSONAL_INJURY',
		 N'Motor Vehicle Accident',
		 @NyTenant, DATEADD(DAY, -20, @NyNow), @NyUser);
END

-- 3.2) One-to-one PI Profile extension (if the table exists).
IF OBJECT_ID(N'POLOXI.Legal_DecisionPIMatterProfile', N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @NyMatter)
   AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionPIMatterProfile WHERE DecisionPIMatterProfileId = @NyProfile)
BEGIN
	INSERT POLOXI.Legal_DecisionPIMatterProfile
		(DecisionPIMatterProfileId, DecisionMatterId, IncidentTypeCode, IncidentDate, IncidentTime,
		 IncidentLocation, IncidentCity, IncidentCounty, IncidentState,
		 IncidentSummary, LiabilitySummary, InjurySummary, TreatmentSummary, DamagesSummary,
		 CurrentStageCode, LitigationStatusCode, DemandStatusCode, SettlementStatusCode,
		 TenantId, CreatedDateUtc, CreatedByUserId)
	VALUES
		(@NyProfile, @NyMatter, N'Motor Vehicle Accident', @NyDOI, NULL,
		 N'Grand Concourse near E 167th Street, Bronx', N'Bronx', N'Bronx', N'New York',
		 N'Plaintiff was rear-ended by a Hudson Freight Lines truck on the Grand Concourse, sustaining cervical and lumbar injuries.',
		 N'Liability largely favorable (rear-end presumption); defendant asserts comparative fault under CPLR 1411 and sudden-stop defense.',
		 N'Cervical and lumbar disc injuries; epidural injections administered; surgical consult pending.',
		 N'Active treatment ongoing; no MMI declared; serious-injury threshold documentation being assembled.',
		 N'Specials approx. $64,500 to date; no-fault benefits partially exhausted; wage loss claimed for a commercial driver.',
		 N'INTAKE', N'PRE_SUIT', N'NOT_SENT', N'NOT_SETTLED',
		 @NyTenant, DATEADD(DAY, -20, @NyNow), @NyUser);
END

-- 3.3) Judz Matter Lifecycle instance — pinned to PI_ADVANCED v1 at INTAKE.
IF OBJECT_ID(N'POLOXI.Legal_MatterLifecycle', N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @NyMatter)
   AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterLifecycle WHERE MatterLifecycleId = @NyLife)
   AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterLifecycle WHERE DecisionMatterId = @NyMatter AND IsPrimary = 1 AND IsDeleted = 0)
BEGIN
	DECLARE @NyVerId  UNIQUEIDENTIFIER =
		(SELECT TOP 1 v.MatterLifecycleVersionId
		 FROM POLOXI.Legal_MatterLifecycleVersion v
		 INNER JOIN POLOXI.Legal_MatterLifecycleDefinition d ON d.MatterLifecycleDefinitionId = v.MatterLifecycleDefinitionId
		 WHERE d.Code = N'PI_ADVANCED' AND d.TenantId IS NULL AND d.IsDeleted = 0
		   AND v.VersionNumber = 1 AND v.IsDeleted = 0);

	DECLARE @NyIntake UNIQUEIDENTIFIER =
		(SELECT TOP 1 MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition
		 WHERE MatterLifecycleVersionId = @NyVerId AND Code = N'INTAKE' AND IsDeleted = 0);

	IF @NyVerId IS NOT NULL AND @NyIntake IS NOT NULL
		INSERT POLOXI.Legal_MatterLifecycle
			(MatterLifecycleId, DecisionMatterId, MatterLifecycleVersionId, CurrentStageDefinitionId,
			 StatusCode, AuthorityMode, StartedUtc, CurrentStageEnteredUtc, IsPrimary, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES
			(@NyLife, @NyMatter, @NyVerId, @NyIntake,
			 N'ACTIVE', N'JUDZ_AUTHORITATIVE', DATEADD(DAY, -20, @NyNow), DATEADD(DAY, -20, @NyNow), 1, @NyTenant, @NyNow, @NyUser);
END

COMMIT T