SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0437: Activate the Domain Pack Evidence Type -> matter linkage data consumed by the Wide2 matter funnel.
--   Audit (0436 review) found the funnel's catalog-driven relevance signals were INERT against real data:
--     * Legal_DecisionDomainPackEvidenceType.RelevanceKeywords : NULL on 353/353 rows
--     * Legal_DecisionDomainPackEvidenceType.MatterTypeCodes   : NULL on 353/353 rows
--     * Legal_DecisionDomainPackRelevanceRule                  : 0 rows
--   This migration seeds all three from DB-backed sources only (no hardcoded per-row prose):
--     1) RelevanceKeywords = normalized distinct terms from the row's own Name + Description + code tokens.
--        Only rows still NULL/blank are filled, so any curated value already present is preserved.
--     2) MatterTypeCodes   = configured Legal_DecisionDomainPackMatterType codes of the owning pack, set ONLY where
--        the evidence code is unambiguously subtype-specific (e.g. VEHICLE_* -> motor-vehicle subtypes).
--        Broadly-applicable evidence types stay NULL = applies to every matter type (no false +0.15 bonus).
--     3) RelevanceRule    = PRIORITIZE rules keyed on DOCUMENT_PRESENT for evidence types that are direct
--        prerequisites of others, and matter-corpus signal rules for subtype-specific evidence. No EXCLUDE
--        rules are seeded: exclusion is an inclusion gate and must be a deliberate per-pack decision.
--   Idempotent: re-running fills only still-empty columns and skips rules that already exist.
-- ============================================================================
BEGIN TRANSACTION;

DECLARE @Now DATETIME2=SYSUTCDATETIME();
DECLARE @SystemUserId UNIQUEIDENTIFIER='00000000-0000-0000-0000-000000000000';

-- ---------------------------------------------------------------------------
-- 1) RelevanceKeywords from Name + Description + code tokens (distinct, lower-case, length > 3).
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackEvidenceType',N'U') IS NOT NULL
BEGIN
	;WITH src AS
	(
		SELECT e.DecisionDomainPackEvidenceTypeId,
			   LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
				   ISNULL(e.Name,N'')+N' '+ISNULL(e.Description,N'')+N' '+REPLACE(e.EvidenceTypeCode,N'_',N' '),
				   N',',N' '),N'.',N' '),N';',N' '),N':',N' '),N'(',N' '),N')',N' '),N'/',N' '),N'-',N' '),N'''',N' '),N'"',N' ')) AS Text
		FROM POLOXI.Legal_DecisionDomainPackEvidenceType e
		WHERE e.IsDeleted=0 AND NULLIF(LTRIM(RTRIM(e.RelevanceKeywords)),N'') IS NULL
	),
	tokens AS
	(
		SELECT s.DecisionDomainPackEvidenceTypeId, LTRIM(RTRIM(t.value)) AS Token
		FROM src s CROSS APPLY STRING_SPLIT(s.Text,N' ') t
		WHERE LEN(LTRIM(RTRIM(t.value)))>3
		  AND LTRIM(RTRIM(t.value)) NOT IN (N'with',N'from',N'that',N'this',N'those',N'these',N'into',N'onto',N'over',N'under',N'about',N'after',N'before',N'their',N'there',N'which',N'where',N'when',N'while',N'other',N'such',N'than',N'then',N'them',N'they',N'have',N'been',N'being',N'were',N'will',N'would',N'should',N'could',N'does',N'done',N'each',N'also',N'only',N'into',N'used',N'uses',N'using',N'record',N'records',N'document',N'documents',N'report',N'reports',N'form',N'forms',N'notice',N'evidence',N'type',N'types',N'related',N'relating',N'regarding',N'including',N'includes',N'include',N'showing',N'shows',N'show',N'supporting',N'supports',N'support')
	),
	agg AS
	(
		SELECT DecisionDomainPackEvidenceTypeId,
			   STRING_AGG(Token,N',') WITHIN GROUP (ORDER BY Token) AS Keywords
		FROM (SELECT DISTINCT DecisionDomainPackEvidenceTypeId,Token FROM tokens) d
		GROUP BY DecisionDomainPackEvidenceTypeId
	)
	UPDATE e
	   SET e.RelevanceKeywords=LEFT(a.Keywords,1000),
		   e.ModifiedDateUtc=@Now,
		   e.ModifiedByUserId=@SystemUserId
	  FROM POLOXI.Legal_DecisionDomainPackEvidenceType e
	  JOIN agg a ON a.DecisionDomainPackEvidenceTypeId=e.DecisionDomainPackEvidenceTypeId;
END;

-- ---------------------------------------------------------------------------
-- 2) MatterTypeCodes from the owning pack's configured matter types, narrowed for subtype-specific codes.
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackEvidenceType',N'U') IS NOT NULL AND OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackMatterType',N'U') IS NOT NULL
BEGIN
	DECLARE @Narrow TABLE(Pattern NVARCHAR(120) NOT NULL, MatterTypeLike NVARCHAR(200) NOT NULL);
	-- Pattern = LIKE over EvidenceTypeCode; MatterTypeLike = LIKE over the pack's configured MatterTypeCode names.
	INSERT @Narrow(Pattern,MatterTypeLike) VALUES
	(N'%VEHICLE%',          N'%Vehicle%'),(N'%VEHICLE%',N'%Truck%'),(N'%VEHICLE%',N'%Motorcycle%'),(N'%VEHICLE%',N'%Rideshare%'),(N'%VEHICLE%',N'%Pedestrian%'),(N'%VEHICLE%',N'%Bicycle%'),
	(N'DMV_%',              N'%Vehicle%'),(N'DMV_%',N'%Truck%'),(N'DMV_%',N'%Motorcycle%'),(N'DMV_%',N'%Rideshare%'),(N'DMV_%',N'%Pedestrian%'),(N'DMV_%',N'%Bicycle%'),
	(N'EVENT_DATA_RECORDER',N'%Vehicle%'),(N'EVENT_DATA_RECORDER',N'%Truck%'),(N'EVENT_DATA_RECORDER',N'%Rideshare%'),
	(N'DELTA_V_ANALYSIS',   N'%Vehicle%'),(N'DELTA_V_ANALYSIS',N'%Truck%'),(N'DELTA_V_ANALYSIS',N'%Motorcycle%'),
	(N'DOT_%',              N'%Truck%'),(N'DOT_%',N'%Commercial Vehicle%'),
	(N'%PRODUCT%',          N'%Product Liability%'),(N'%PRODUCT%',N'%Mass Tort%'),
	(N'RECALL_NOTICE',      N'%Product Liability%'),(N'RECALL_NOTICE',N'%Mass Tort%'),
	(N'DESIGN_SPECIFICATION',N'%Product Liability%'),
	(N'WARNING_LABEL_INSTRUCTIONS',N'%Product Liability%'),
	(N'%PREMISES%',         N'%Premises%'),(N'%PREMISES%',N'%Slip and Fall%'),(N'%PREMISES%',N'%Negligent Security%'),
	(N'SWEEP_INSPECTION_LOG',N'%Premises%'),(N'SWEEP_INSPECTION_LOG',N'%Slip and Fall%'),
	(N'OPEN_OBVIOUS_PHOTO', N'%Premises%'),(N'OPEN_OBVIOUS_PHOTO',N'%Slip and Fall%'),
	(N'FOOTWEAR_ATTIRE_PHOTO',N'%Premises%'),(N'FOOTWEAR_ATTIRE_PHOTO',N'%Slip and Fall%'),
	(N'WARNING_SIGN_PHOTO', N'%Premises%'),(N'WARNING_SIGN_PHOTO',N'%Slip and Fall%'),
	(N'SECURITY_STAFFING_RECORD',N'%Negligent Security%'),(N'SECURITY_STAFFING_RECORD',N'%Premises%'),
	(N'AUTOPSY_REPORT',     N'%Wrongful Death%'),(N'DEATH_CERTIFICATE',N'%Wrongful Death%'),(N'SUCCESSOR_IN_INTEREST_DECLARATION',N'%Wrongful Death%'),
	(N'%OSHA%',             N'%Construction%'),(N'%OSHA%',N'%Workplace%'),
	(N'WORKERS_COMP_LIEN_NOTICE',N'%Construction%'),(N'WORKERS_COMP_LIEN_NOTICE',N'%Workplace%'),
	(N'%EXPOSURE%',         N'%Toxic Tort%'),(N'%EXPOSURE%',N'%Mass Tort%'),
	(N'%NURSING%',          N'%Nursing Home%'),(N'%ELDER%',N'%Nursing Home%'),
	(N'%DRAM%',             N'%Dram Shop%'),(N'%ALCOHOL%',N'%Dram Shop%'),(N'%LIQUOR%',N'%Dram Shop%'),
	(N'%ANIMAL%',           N'%Dog Bite%'),(N'%DOG%',N'%Dog Bite%'),
	(N'%MALPRACTICE%',      N'%Medical Malpractice%'),(N'%STANDARD_OF_CARE%',N'%Medical Malpractice%'),(N'%INFORMED_CONSENT%',N'%Medical Malpractice%'),
	(N'%AVIATION%',         N'%Aviation%'),(N'%MARITIME%',N'%Aviation%');

	;WITH packTypes AS
	(
		SELECT m.DecisionDomainPackId, m.MatterTypeCode
		FROM POLOXI.Legal_DecisionDomainPackMatterType m
		WHERE m.IsDeleted=0 AND m.IsActive=1
	),
	narrowed AS
	(
		SELECT DISTINCT e.DecisionDomainPackEvidenceTypeId, p.MatterTypeCode
		FROM POLOXI.Legal_DecisionDomainPackEvidenceType e
		JOIN @Narrow n ON e.EvidenceTypeCode LIKE n.Pattern
		JOIN packTypes p ON p.DecisionDomainPackId=e.DecisionDomainPackId AND p.MatterTypeCode LIKE n.MatterTypeLike
		WHERE e.IsDeleted=0
	),
	agg AS
	(
		SELECT DecisionDomainPackEvidenceTypeId, STRING_AGG(MatterTypeCode,N',') WITHIN GROUP (ORDER BY MatterTypeCode) AS Codes
		FROM narrowed GROUP BY DecisionDomainPackEvidenceTypeId
	)
	UPDATE e
	   SET e.MatterTypeCodes=LEFT(a.Codes,400),
		   e.ModifiedDateUtc=@Now,
		   e.ModifiedByUserId=@SystemUserId
	  FROM POLOXI.Legal_DecisionDomainPackEvidenceType e
	  JOIN agg a ON a.DecisionDomainPackEvidenceTypeId=e.DecisionDomainPackEvidenceTypeId
	 WHERE NULLIF(LTRIM(RTRIM(e.MatterTypeCodes)),N'') IS NULL;
END;

-- ---------------------------------------------------------------------------
-- 3) Relevance rules (PRIORITIZE only). SignalKindCode semantics match IntelligenceWide2Service.ResolveRelevanceRule:
--      DOCUMENT_PRESENT : fires when an uploaded document is classified as SignalValue (an Evidence Type code)
--      MATTER_SIGNAL    : fires when every token of SignalValue is present in the matter corpus
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackRelevanceRule',N'U') IS NOT NULL
BEGIN
	DECLARE @Rules TABLE(TargetCode NVARCHAR(240) NOT NULL, SignalKindCode NVARCHAR(80) NOT NULL, SignalValue NVARCHAR(800) NOT NULL, Priority INT NOT NULL, Reason NVARCHAR(2000) NOT NULL);
	INSERT @Rules(TargetCode,SignalKindCode,SignalValue,Priority,Reason) VALUES
	-- Direct prerequisites: a classified upload makes the dependent evidence type a near-certain factor.
	(N'MEDICAL_BILLS',                   N'DOCUMENT_PRESENT', N'MEDICAL_RECORDS',      10, N'Medical records on file: billing/specials are the next quantum factor.'),
	(N'PRIOR_MEDICAL_HISTORY',           N'DOCUMENT_PRESENT', N'MEDICAL_RECORDS',      20, N'Medical records on file: pre-existing-condition apportionment must be addressed.'),
	(N'FUTURE_MEDICAL_COST_PROJECTION',  N'DOCUMENT_PRESENT', N'MEDICAL_RECORDS',      30, N'Medical records on file: future care projection is a direct dependent.'),
	(N'POLICY_LIMITS_DISCLOSURE',        N'DOCUMENT_PRESENT', N'INSURANCE_POLICY',     10, N'Policy on file: limits disclosure governs settlement ceiling.'),
	(N'POLICY_ENDORSEMENT',              N'DOCUMENT_PRESENT', N'INSURANCE_POLICY',     20, N'Policy on file: endorsements can alter coverage grants/exclusions.'),
	(N'EXCESS_UMBRELLA_POLICY',          N'DOCUMENT_PRESENT', N'INSURANCE_POLICY',     30, N'Primary policy on file: excess/umbrella layer must be confirmed or ruled out.'),
	(N'WITNESS_STATEMENTS',              N'DOCUMENT_PRESENT', N'ACCIDENT_REPORT',      10, N'Accident report on file: listed witnesses should be developed.'),
	(N'SCENE_INSPECTION',                N'DOCUMENT_PRESENT', N'ACCIDENT_REPORT',      20, N'Accident report on file: scene inspection corroborates or contradicts the report.'),
	(N'PHOTOS_VIDEO',                    N'DOCUMENT_PRESENT', N'ACCIDENT_REPORT',      30, N'Accident report on file: contemporaneous imagery is a direct corroborant.'),
	(N'HOSPITAL_LIEN_FILING',            N'DOCUMENT_PRESENT', N'MEDICAL_BILLS',        10, N'Medical bills on file: provider/hospital liens must be reconciled.'),
	(N'LIEN_RESOLUTION_RECORD',          N'DOCUMENT_PRESENT', N'LIEN_ASSERTIONS',      10, N'Liens asserted: resolution record drives net-to-client.'),
	-- Matter-corpus signals: subtype-specific evidence is prioritized when the matter text establishes the subtype.
	(N'SWEEP_INSPECTION_LOG',            N'MATTER_SIGNAL',    N'slip fall',            10, N'Slip-and-fall matter: inspection cadence is the core notice factor.'),
	(N'PREMISES_INCIDENT_LOG',           N'MATTER_SIGNAL',    N'slip fall',            20, N'Slip-and-fall matter: prior incident log establishes constructive notice.'),
	(N'SURVEILLANCE_VIDEO',              N'MATTER_SIGNAL',    N'surveillance',         10, N'Matter references surveillance: footage is dispositive on hazard duration.'),
	(N'OPEN_OBVIOUS_PHOTO',              N'MATTER_SIGNAL',    N'premises',             30, N'Premises matter: open-and-obvious defense must be evaluated.'),
	(N'EVENT_DATA_RECORDER',             N'MATTER_SIGNAL',    N'vehicle collision',    10, N'Vehicle collision: EDR data is primary speed/braking evidence.'),
	(N'DMV_ACCIDENT_REPORT_SR1',         N'MATTER_SIGNAL',    N'vehicle collision',    20, N'Vehicle collision: SR-1 filing is a statutory prerequisite factor.'),
	(N'VEHICLE_DAMAGE_APPRAISAL',        N'MATTER_SIGNAL',    N'vehicle collision',    30, N'Vehicle collision: damage appraisal supports impact severity.'),
	(N'DOT_VEHICLE_INSPECTION_REPORT',   N'MATTER_SIGNAL',    N'truck commercial',     10, N'Commercial trucking: DOT inspection history is a core negligence factor.'),
	(N'RECALL_NOTICE',                   N'MATTER_SIGNAL',    N'product defect',       10, N'Product defect matter: recall history is a direct defect/notice factor.'),
	(N'PRODUCT_FAILURE_ANALYSIS',        N'MATTER_SIGNAL',    N'product defect',       20, N'Product defect matter: failure analysis establishes the defect mechanism.'),
	(N'AUTOPSY_REPORT',                  N'MATTER_SIGNAL',    N'wrongful death',       10, N'Wrongful death: autopsy establishes cause and manner of death.'),
	(N'DEATH_CERTIFICATE',               N'MATTER_SIGNAL',    N'wrongful death',       20, N'Wrongful death: death certificate is a statutory standing prerequisite.'),
	(N'SECURITY_STAFFING_RECORD',        N'MATTER_SIGNAL',    N'negligent security',   10, N'Negligent security: staffing records establish foreseeability response.'),
	(N'WORKERS_COMP_LIEN_NOTICE',        N'MATTER_SIGNAL',    N'workplace injury',     10, N'Workplace injury: comp lien governs third-party recovery allocation.');

	INSERT POLOXI.Legal_DecisionDomainPackRelevanceRule
		  (DecisionDomainPackRelevanceRuleId,DecisionDomainPackId,TargetKindCode,TargetCode,SignalKindCode,SignalValue,EffectCode,Priority,Reason,IsActive,TenantId,CreatedDateUtc,CreatedByUserId,IsDeleted)
	SELECT NEWID(), e.DecisionDomainPackId, N'EVIDENCE_TYPE', e.EvidenceTypeCode, r.SignalKindCode, r.SignalValue, N'PRIORITIZE', r.Priority, r.Reason, 1, e.TenantId, @Now, @SystemUserId, 0
	  FROM @Rules r
	  JOIN POLOXI.Legal_DecisionDomainPackEvidenceType e ON e.EvidenceTypeCode=r.TargetCode AND e.IsDeleted=0 AND e.IsActive=1
	 WHERE (r.SignalKindCode<>N'DOCUMENT_PRESENT'
			OR EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDomainPackEvidenceType s WHERE s.DecisionDomainPackId=e.DecisionDomainPackId AND s.EvidenceTypeCode=r.SignalValue AND s.IsDeleted=0))
	   AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDomainPackRelevanceRule x
						WHERE x.DecisionDomainPackId=e.DecisionDomainPackId AND x.TargetKindCode=N'EVIDENCE_TYPE' AND x.TargetCode=e.EvidenceTypeCode
						  AND x.SignalKindCode=r.SignalKindCode AND x.SignalValue=r.SignalValue AND x.IsDeleted=0);
END;

COMMIT TRANSACTION;
