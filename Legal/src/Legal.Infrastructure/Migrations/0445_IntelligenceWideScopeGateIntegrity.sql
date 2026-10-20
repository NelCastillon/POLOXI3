SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0445: Evidence-scope gate integrity (HIGH IMPACT: this is the only Domain Pack L2 -> L3 membership gate).
--   Audit of a live Motor Vehicle matter (353-target PERSONAL_INJURY catalog) found:
--     * the L2 relevance review prompt was silently truncated at the user-prompt budget, so 348/353 targets were
--       never scored by the model ("Not returned by L2 relevance review");
--     * a single lexical passage hit admitted a target, and DocumentPresent was inflated by fuzzy matching
--       (56 flagged vs 3 directly classified uploads);
--     * RelevanceKeywords carried stop-words (with, show, order, state, internal, ...) that matched any matter;
--     * PRIORITIZE rules keyed on generic tokens ("premises", "city", "county") fired on boilerplate text;
--     * the catalog had no incident-family tagging, so premises-liability evidence flooded a motor-vehicle case;
--     * COMPARATIVE_FAULT_THRESHOLD (a threshold rule) is inapplicable in California (pure comparative fault).
--   This migration is the TABLE half of the fix (code follows the same contracts):
--     1) New DB dials: Domain Pack automatic-deepening ceiling, L2 review prompt budget / retries / concurrency,
--        passage-admission floor, decisive-passage score, PENDING_REVIEW band floor.
--     2) Scope statuses: PENDING_REVIEW becomes a dormant band (no L3 pass until an attorney decides);
--        new REVIEW_FAILED status for targets the model never scored after bounded retries (never auto-admitted).
--     3) Incident families: lookup table + nullable IncidentFamilyCodes on Evidence Types, seeded from the
--        existing code-pattern narrowing (0437). NULL = applies to every family.
--     4) RelevanceKeywords stop-word cleanup (data only; curated terms preserved).
--     5) Over-broad PRIORITIZE rules deactivated (never deleted) and replaced with specific signals.
--     6) California pure comparative fault concept + SUPERSEDES relation over the generic threshold concept.
--   Idempotent and additive. Nothing is deleted; USER scope rows are untouched.
-- ============================================================================
BEGIN TRANSACTION;

DECLARE @Now DATETIME2=SYSUTCDATETIME();
DECLARE @SystemUserId UNIQUEIDENTIFIER='00000000-0000-0000-0000-000000000000';

-- ---------------------------------------------------------------------------
-- 1) Platform configuration dials (upsert; existing tenant overrides untouched).
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200) NOT NULL, Value NVARCHAR(100) NOT NULL, DataTypeCode NVARCHAR(40) NOT NULL, Description NVARCHAR(1000) NOT NULL);
	INSERT @Settings(SettingKey,Value,DataTypeCode,Description) VALUES
	(N'Intelligence.SearchWide.DomainPackDepthCeiling',N'3',N'Integer',
	 N'Domain Pack mode only: maximum hierarchy depth reached by AUTOMATIC serial deepening (L1 Dimensions -> L2 catalog targets -> L3 atomic propositions). Reaching it records DEPTH_CEILING_REACHED and does NOT mark branches resolved. Other execution modes, existing deeper nodes, evidence propagation and explicitly targeted deepening are unaffected. 0 = no Domain Pack ceiling (AbsoluteDepthCeiling applies).'),
	(N'Intelligence.SearchWide.L2ReviewPromptBudgetChars',N'24000',N'Integer',
	 N'Character budget for ONE L2 relevance-review prompt (matter context + catalog lines). The catalog is split into as many batches as needed so no target line is ever truncated out of the prompt.'),
	(N'Intelligence.SearchWide.L2ReviewMaxRetries',N'2',N'Integer',
	 N'Bounded retries for L2 relevance-review targets that a batch did not return or returned invalid. Targets still unscored after the retries are marked REVIEW_FAILED (never auto-admitted to L3).'),
	(N'Intelligence.SearchWide.L2ReviewConcurrency',N'4',N'Integer',
	 N'Maximum concurrent L2 relevance-review LLM calls (bounded parallelism, subject to provider limits).'),
	(N'Intelligence.SearchWide.MatterScopeMinimumMappedPassages',N'2',N'Integer',
	 N'Passage-admission floor: an Evidence Type/Concept is admitted on mapped document passages alone only when at least this many distinct passages map to it. A single passage admits only when it is decisive (see MatterScopeDecisivePassageScore).'),
	(N'Intelligence.SearchWide.MatterScopeDecisivePassageScore',N'0.60',N'Decimal',
	 N'Single-passage exception to the passage floor: one mapped passage admits the target when its lexical mapping score is at or above this value.'),
	(N'Intelligence.SearchWide.MatterScopePendingReviewFloor',N'0.10',N'Decimal',
	 N'PENDING_REVIEW band: targets with no positive matter signal whose blended relevance lies between this floor and MatterScopeOutThreshold are marked PENDING_REVIEW (dormant, attorney-decidable) instead of OUT_OF_SCOPE.');

	UPDATE c SET c.DefaultValue=s.Value, c.Description=s.Description, c.ModifiedDateUtc=@Now
	  FROM Core.ConfigurationSetting c JOIN @Settings s ON s.SettingKey=c.SettingKey
	 WHERE c.TenantId IS NULL AND c.ScopeCode=N'Platform' AND c.IsDeleted=0;

	INSERT Core.ConfigurationSetting(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted)
	SELECT NEWID(),NULL,N'Platform',N'Intelligence',s.SettingKey,s.Value,s.Value,s.DataTypeCode,s.Description,0,0,@Now,0
	  FROM @Settings s
	 WHERE NOT EXISTS(SELECT 1 FROM Core.ConfigurationSetting c WHERE c.SettingKey=s.SettingKey AND c.TenantId IS NULL AND c.ScopeCode=N'Platform' AND c.IsDeleted=0);
END;

-- ---------------------------------------------------------------------------
-- 2) Scope statuses: PENDING_REVIEW is a dormant band; REVIEW_FAILED never participates.
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'POLOXI.Legal_MatterEvidenceScopeStatus',N'U') IS NOT NULL
BEGIN
	UPDATE POLOXI.Legal_MatterEvidenceScopeStatus
	   SET ParticipatesInL3=0,
		   Description=N'System found only weak or ambiguous matter signals. Dormant: held back from L3 generation until an attorney moves it in or out of scope.',
		   ModifiedDateUtc=@Now, ModifiedByUserId=@SystemUserId
	 WHERE StatusCode=N'PENDING_REVIEW' AND TenantId IS NULL AND IsDeleted=0;

	IF NOT EXISTS(SELECT 1 FROM POLOXI.Legal_MatterEvidenceScopeStatus WHERE StatusCode=N'REVIEW_FAILED' AND TenantId IS NULL AND IsDeleted=0)
		INSERT POLOXI.Legal_MatterEvidenceScopeStatus(TenantId,StatusCode,Name,Description,ParticipatesInL3,SortOrder,IsActive,CreatedDateUtc,CreatedByUserId,IsDeleted)
		VALUES(NULL,N'REVIEW_FAILED',N'Review failed',N'The L2 relevance review did not return a valid score for this target after bounded retries and no deterministic signal admitted it. Held back from L3 (never auto-admitted) and surfaced for attorney review.',0,40,1,@Now,@SystemUserId,0);
END;

-- ---------------------------------------------------------------------------
-- 3) Incident families: lookup + Evidence Type tagging.
--    MatterSignalPatterns = comma-separated LIKE patterns matched (case-insensitive) against the matter's
--    MatterTypeCode, Subtype, ClaimTypeCode and PI profile IncidentTypeCode. A matter may resolve to
--    several families (e.g. a truck collision is MOTOR_VEHICLE and TRUCKING).
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackIncidentFamily',N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_DecisionDomainPackIncidentFamily
	(
		DecisionDomainPackIncidentFamilyId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionDomainPackIncidentFamily PRIMARY KEY DEFAULT NEWID(),
		DecisionDomainPackId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DecisionDomainPackIncidentFamily_Pack REFERENCES POLOXI.Legal_DecisionDomainPack(DecisionDomainPackId),
		FamilyCode NVARCHAR(60) NOT NULL,
		Name NVARCHAR(200) NOT NULL,
		Description NVARCHAR(1000) NULL,
		MatterSignalPatterns NVARCHAR(1000) NOT NULL,
		SortOrder INT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackIncidentFamily_Sort DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackIncidentFamily_Active DEFAULT 1,
		TenantId UNIQUEIDENTIFIER NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackIncidentFamily_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackIncidentFamily_Deleted DEFAULT 0
	);
	CREATE UNIQUE INDEX UX_Legal_DecisionDomainPackIncidentFamily_Code ON POLOXI.Legal_DecisionDomainPackIncidentFamily(DecisionDomainPackId,FamilyCode) WHERE TenantId IS NULL AND IsDeleted=0;
END;

IF COL_LENGTH(N'POLOXI.Legal_DecisionDomainPackEvidenceType',N'IncidentFamilyCodes') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionDomainPackEvidenceType ADD IncidentFamilyCodes NVARCHAR(400) NULL;

COMMIT TRANSACTION;
GO

-- Second batch: the new column must be bound after the ALTER above is committed.
BEGIN TRANSACTION;

DECLARE @Now DATETIME2=SYSUTCDATETIME();
DECLARE @SystemUserId UNIQUEIDENTIFIER='00000000-0000-0000-0000-000000000000';
DECLARE @PiPackId UNIQUEIDENTIFIER=(SELECT TOP 1 DecisionDomainPackId FROM POLOXI.Legal_DecisionDomainPack WHERE PackCode=N'PERSONAL_INJURY' AND TenantId IS NULL AND IsDeleted=0);

IF @PiPackId IS NOT NULL
BEGIN
	DECLARE @Families TABLE(FamilyCode NVARCHAR(60), Name NVARCHAR(200), Patterns NVARCHAR(1000), SortOrder INT);
	INSERT @Families VALUES
	(N'MOTOR_VEHICLE',       N'Motor vehicle collision',      N'%vehicle%,%collision%,%rear-end%,%rear end%,%motorcycle%,%pedestrian%,%bicycle%,%rideshare%,%truck%,%auto accident%,%car accident%',10),
	(N'TRUCKING',            N'Trucking / commercial vehicle',N'%truck%,%commercial vehicle%,%tractor%,%trailer%,%big rig%',20),
	(N'PREMISES',            N'Premises liability',           N'%premises%,%slip and fall%,%trip and fall%,%slip%,%falling object%,%negligent security%,%store%',30),
	(N'NEGLIGENT_SECURITY',  N'Negligent security',           N'%negligent security%,%assault on premises%',40),
	(N'PRODUCT',             N'Product liability',            N'%product%,%defective%,%mass tort%,%pharmaceutical%,%recall%',50),
	(N'MEDICAL_MALPRACTICE', N'Medical malpractice',          N'%malpractice%,%medical procedure%,%surgical%,%misdiagnosis%',60),
	(N'WRONGFUL_DEATH',      N'Wrongful death',               N'%wrongful death%,%fatal%,%death%',70),
	(N'WORKPLACE',           N'Construction / workplace',     N'%construction%,%workplace%,%job site%,%scaffold%',80),
	(N'TOXIC_EXPOSURE',      N'Toxic tort / exposure',        N'%toxic%,%exposure%,%asbestos%,%chemical%',90),
	(N'NURSING_HOME',        N'Nursing home / elder abuse',   N'%nursing%,%elder%,%assisted living%',100),
	(N'DRAM_SHOP',           N'Dram shop / liquor liability', N'%dram%,%liquor%,%alcohol%,%bar fight%',110),
	(N'ANIMAL',              N'Dog bite / animal injury',     N'%dog%,%animal%,%bite%',120),
	(N'AVIATION_MARITIME',   N'Aviation / maritime',          N'%aviation%,%aircraft%,%maritime%,%vessel%,%boat%',130),
	(N'EMPLOYMENT',          N'Employment',                   N'%termination%,%discrimination%,%retaliation%,%harassment%,%wage%,%whistleblower%,%non-compete%,%trade secret%,%accommodation%,%employment%',140);

	INSERT POLOXI.Legal_DecisionDomainPackIncidentFamily(DecisionDomainPackId,FamilyCode,Name,MatterSignalPatterns,SortOrder,IsActive,TenantId,CreatedDateUtc,CreatedByUserId,IsDeleted)
	SELECT @PiPackId,f.FamilyCode,f.Name,f.Patterns,f.SortOrder,1,NULL,@Now,@SystemUserId,0
	  FROM @Families f
	 WHERE NOT EXISTS(SELECT 1 FROM POLOXI.Legal_DecisionDomainPackIncidentFamily x WHERE x.DecisionDomainPackId=@PiPackId AND x.FamilyCode=f.FamilyCode AND x.TenantId IS NULL AND x.IsDeleted=0);

	-- Evidence Type -> family tags from code patterns (mirrors and extends the 0437 narrowing table).
	DECLARE @Tag TABLE(Pattern NVARCHAR(120) NOT NULL, FamilyCode NVARCHAR(60) NOT NULL);
	INSERT @Tag(Pattern,FamilyCode) VALUES
	(N'%VEHICLE%',N'MOTOR_VEHICLE'),(N'%VEHICLE%',N'TRUCKING'),(N'DMV_%',N'MOTOR_VEHICLE'),(N'DMV_%',N'TRUCKING'),
	(N'EVENT_DATA_RECORDER',N'MOTOR_VEHICLE'),(N'EVENT_DATA_RECORDER',N'TRUCKING'),(N'DELTA_V_ANALYSIS',N'MOTOR_VEHICLE'),(N'DELTA_V_ANALYSIS',N'TRUCKING'),
	(N'%CRASH%',N'MOTOR_VEHICLE'),(N'%CRASH%',N'TRUCKING'),(N'%COLLISION%',N'MOTOR_VEHICLE'),(N'%COLLISION%',N'TRUCKING'),(N'%TRAFFIC%',N'MOTOR_VEHICLE'),(N'%TRAFFIC%',N'TRUCKING'),
	(N'%DASHCAM%',N'MOTOR_VEHICLE'),(N'%DASHCAM%',N'TRUCKING'),(N'%AIRBAG%',N'MOTOR_VEHICLE'),(N'%SEATBELT%',N'MOTOR_VEHICLE'),(N'%SKID%',N'MOTOR_VEHICLE'),(N'%SKID%',N'TRUCKING'),
	(N'DOT_%',N'TRUCKING'),(N'%HOURS_OF_SERVICE%',N'TRUCKING'),(N'%FMCSA%',N'TRUCKING'),(N'%DRIVER_QUALIFICATION%',N'TRUCKING'),(N'%ELD_%',N'TRUCKING'),
	(N'%PRODUCT%',N'PRODUCT'),(N'RECALL_NOTICE',N'PRODUCT'),(N'DESIGN_SPECIFICATION',N'PRODUCT'),(N'WARNING_LABEL_INSTRUCTIONS',N'PRODUCT'),
	(N'%PREMISES%',N'PREMISES'),(N'%PREMISES%',N'NEGLIGENT_SECURITY'),(N'SWEEP_INSPECTION_LOG',N'PREMISES'),(N'OPEN_OBVIOUS_PHOTO',N'PREMISES'),
	(N'FOOTWEAR_ATTIRE_PHOTO',N'PREMISES'),(N'WARNING_SIGN_PHOTO',N'PREMISES'),(N'%FLOOR%',N'PREMISES'),(N'%SPILL%',N'PREMISES'),(N'%SWEEP%',N'PREMISES'),
	(N'SECURITY_STAFFING_RECORD',N'NEGLIGENT_SECURITY'),(N'SECURITY_STAFFING_RECORD',N'PREMISES'),(N'%SECURITY_%',N'NEGLIGENT_SECURITY'),(N'%SECURITY_%',N'PREMISES'),
	(N'AUTOPSY_REPORT',N'WRONGFUL_DEATH'),(N'DEATH_CERTIFICATE',N'WRONGFUL_DEATH'),(N'SUCCESSOR_IN_INTEREST_DECLARATION',N'WRONGFUL_DEATH'),
	(N'%OSHA%',N'WORKPLACE'),(N'WORKERS_COMP_LIEN_NOTICE',N'WORKPLACE'),
	(N'%EXPOSURE%',N'TOXIC_EXPOSURE'),(N'%NURSING%',N'NURSING_HOME'),(N'%ELDER%',N'NURSING_HOME'),
	(N'%DRAM%',N'DRAM_SHOP'),(N'%ALCOHOL%',N'DRAM_SHOP'),(N'%LIQUOR%',N'DRAM_SHOP'),
	(N'%ANIMAL%',N'ANIMAL'),(N'%DOG%',N'ANIMAL'),
	(N'%MALPRACTICE%',N'MEDICAL_MALPRACTICE'),(N'%STANDARD_OF_CARE%',N'MEDICAL_MALPRACTICE'),(N'%INFORMED_CONSENT%',N'MEDICAL_MALPRACTICE'),
	(N'%AVIATION%',N'AVIATION_MARITIME'),(N'%MARITIME%',N'AVIATION_MARITIME');

	;WITH tagged AS
	(
		SELECT DISTINCT e.DecisionDomainPackEvidenceTypeId, t.FamilyCode
		FROM POLOXI.Legal_DecisionDomainPackEvidenceType e
		JOIN @Tag t ON e.EvidenceTypeCode LIKE t.Pattern
		WHERE e.IsDeleted=0 AND e.DecisionDomainPackId=@PiPackId
	),
	agg AS
	(
		SELECT DecisionDomainPackEvidenceTypeId, STRING_AGG(FamilyCode,N',') WITHIN GROUP (ORDER BY FamilyCode) AS Codes
		FROM tagged GROUP BY DecisionDomainPackEvidenceTypeId
	)
	UPDATE e SET e.IncidentFamilyCodes=LEFT(a.Codes,400), e.ModifiedDateUtc=@Now, e.ModifiedByUserId=@SystemUserId
	  FROM POLOXI.Legal_DecisionDomainPackEvidenceType e JOIN agg a ON a.DecisionDomainPackEvidenceTypeId=e.DecisionDomainPackEvidenceTypeId
	 WHERE NULLIF(LTRIM(RTRIM(e.IncidentFamilyCodes)),N'') IS NULL;
END;

-- ---------------------------------------------------------------------------
-- 4) RelevanceKeywords stop-word cleanup (generic tokens that match any matter corpus).
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackEvidenceType',N'U') IS NOT NULL
BEGIN
	DECLARE @Stop TABLE(Token NVARCHAR(60) NOT NULL PRIMARY KEY);
	INSERT @Stop VALUES
	(N'with'),(N'without'),(N'within'),(N'show'),(N'shows'),(N'showing'),(N'before'),(N'after'),(N'statu'),(N'status'),(N'apply'),(N'applies'),(N'applied'),
	(N'order'),(N'orders'),(N'state'),(N'states'),(N'internal'),(N'external'),(N'incident'),(N'accident'),(N'medical'),(N'analysis'),(N'documentation'),
	(N'party'),(N'parties'),(N'plaintiff'),(N'defendant'),(N'claimant'),(N'insured'),(N'statement'),(N'statements'),(N'prior'),(N'claim'),(N'claims'),
	(N'care'),(N'loss'),(N'losses'),(N'provider'),(N'providers'),(N'agreement'),(N'injury'),(N'injuries'),(N'matter'),(N'case'),(N'legal'),(N'personal'),
	(N'general'),(N'other'),(N'specific'),(N'relevant'),(N'available'),(N'copy'),(N'copies'),(N'date'),(N'dates'),(N'time'),(N'information'),(N'detail'),(N'details'),
	(N'issue'),(N'issues'),(N'result'),(N'results'),(N'value'),(N'values'),(N'event'),(N'events'),(N'person'),(N'people'),(N'number'),(N'name'),(N'names'),(N'level');

	;WITH src AS
	(
		SELECT e.DecisionDomainPackEvidenceTypeId, LTRIM(RTRIM(t.value)) AS Token
		FROM POLOXI.Legal_DecisionDomainPackEvidenceType e
		CROSS APPLY STRING_SPLIT(e.RelevanceKeywords,N',') t
		WHERE e.IsDeleted=0 AND NULLIF(LTRIM(RTRIM(e.RelevanceKeywords)),N'') IS NOT NULL
	),
	kept AS
	(
		SELECT s.DecisionDomainPackEvidenceTypeId, s.Token
		FROM src s
		WHERE LEN(s.Token)>3 AND NOT EXISTS(SELECT 1 FROM @Stop x WHERE x.Token=LOWER(s.Token))
	),
	agg AS
	(
		SELECT DecisionDomainPackEvidenceTypeId, STRING_AGG(Token,N',') WITHIN GROUP (ORDER BY Token) AS Keywords
		FROM (SELECT DISTINCT DecisionDomainPackEvidenceTypeId,Token FROM kept) d
		GROUP BY DecisionDomainPackEvidenceTypeId
	)
	UPDATE e SET e.RelevanceKeywords=LEFT(ISNULL(a.Keywords,N''),1000), e.ModifiedDateUtc=@Now, e.ModifiedByUserId=@SystemUserId
	  FROM POLOXI.Legal_DecisionDomainPackEvidenceType e
	  LEFT JOIN agg a ON a.DecisionDomainPackEvidenceTypeId=e.DecisionDomainPackEvidenceTypeId
	 WHERE e.IsDeleted=0 AND NULLIF(LTRIM(RTRIM(e.RelevanceKeywords)),N'') IS NOT NULL
	   AND ISNULL(a.Keywords,N'')<>e.RelevanceKeywords;
END;

-- ---------------------------------------------------------------------------
-- 5) PRIORITIZE rules keyed on boilerplate tokens: deactivate (audit-preserved) and add specific signals.
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackRelevanceRule',N'U') IS NOT NULL AND @PiPackId IS NOT NULL
BEGIN
	UPDATE POLOXI.Legal_DecisionDomainPackRelevanceRule
	   SET IsActive=0, ModifiedDateUtc=@Now, ModifiedByUserId=@SystemUserId,
		   Reason=ISNULL(Reason,N'')+N' [0445: deactivated - signal token is boilerplate (matches jurisdiction/venue text in unrelated matters); incident family tagging now governs]'
	 WHERE DecisionDomainPackId=@PiPackId AND IsDeleted=0 AND IsActive=1 AND EffectCode=N'PRIORITIZE' AND SignalKindCode=N'MATTER_SIGNAL'
	   AND ((TargetCode=N'OPEN_OBVIOUS_PHOTO' AND SignalValue=N'premises')
		 OR (TargetCode=N'GOVERNMENT_CLAIM_DENIAL' AND SignalValue IN (N'city',N'county')));

	DECLARE @Rules TABLE(TargetCode NVARCHAR(60), SignalKindCode NVARCHAR(40), SignalValue NVARCHAR(200), Priority INT, Reason NVARCHAR(600));
	INSERT @Rules VALUES
	(N'GOVERNMENT_CLAIM_DENIAL',N'MATTER_SIGNAL',N'public entity',20,N'Public entity defendant referenced: Government Claims Act prerequisites control.'),
	(N'GOVERNMENT_CLAIM_DENIAL',N'MATTER_SIGNAL',N'government claim',20,N'Government claim referenced: rejection starts the six-month suit window.'),
	(N'OPEN_OBVIOUS_PHOTO',N'MATTER_SIGNAL',N'open and obvious',20,N'Open-and-obvious defense explicitly raised in the matter.');

	INSERT POLOXI.Legal_DecisionDomainPackRelevanceRule(DecisionDomainPackRelevanceRuleId,DecisionDomainPackId,TargetKindCode,TargetCode,SignalKindCode,SignalValue,EffectCode,Priority,Reason,IsActive,TenantId,CreatedDateUtc,CreatedByUserId,IsDeleted)
	SELECT NEWID(),@PiPackId,N'EVIDENCE_TYPE',r.TargetCode,r.SignalKindCode,r.SignalValue,N'PRIORITIZE',r.Priority,r.Reason,1,NULL,@Now,@SystemUserId,0
	  FROM @Rules r
	 WHERE NOT EXISTS(SELECT 1 FROM POLOXI.Legal_DecisionDomainPackRelevanceRule x WHERE x.DecisionDomainPackId=@PiPackId AND x.TargetKindCode=N'EVIDENCE_TYPE' AND x.TargetCode=r.TargetCode AND x.SignalKindCode=r.SignalKindCode AND x.SignalValue=r.SignalValue AND x.TenantId IS NULL AND x.IsDeleted=0);
END;

-- ---------------------------------------------------------------------------
-- 6) California pure comparative fault (Civ. Code 1714; Li v. Yellow Cab (1975) 13 Cal.3d 804): no threshold.
--    Jurisdiction-specific concept + SUPERSEDES relation over the generic threshold concept. Code applies
--    SUPERSEDES only when the source concept's JurisdictionCode matches the matter jurisdiction.
-- ---------------------------------------------------------------------------
IF @PiPackId IS NOT NULL AND OBJECT_ID(N'POLOXI.Legal_DecisionDomainConcept',N'U') IS NOT NULL
BEGIN
	IF NOT EXISTS(SELECT 1 FROM POLOXI.Legal_DecisionDomainConcept WHERE DecisionDomainPackId=@PiPackId AND ConceptCode=N'CA_PURE_COMPARATIVE_FAULT' AND TenantId IS NULL AND IsDeleted=0)
		INSERT POLOXI.Legal_DecisionDomainConcept(DecisionDomainPackId,ConceptCode,DimensionCode,Name,Description,ConceptKindCode,SourceClassCode,VerificationProfileCode,JurisdictionCode,MatterTypeCode,IsRequiredCoverage,IsFallbackEligible,SortOrder,VersionNumber,TenantId,CreatedDateUtc,CreatedByUserId,IsDeleted)
		VALUES(@PiPackId,N'CA_PURE_COMPARATIVE_FAULT',N'DEFENSES',N'California pure comparative fault',
			N'California applies pure comparative negligence: the plaintiff''s recovery is reduced by the plaintiff''s percentage of fault with NO bar threshold (Li v. Yellow Cab Co. (1975) 13 Cal.3d 804; Civ. Code section 1714). Applicable only when the authoritative matter jurisdiction or governing law is California.',
			N'LEGAL_RULE',N'LEGAL_AUTHORITY',N'LIABILITY_FACTS',N'CALIFORNIA',NULL,1,1,42,1,NULL,@Now,@SystemUserId,0);

	DECLARE @CaId UNIQUEIDENTIFIER=(SELECT TOP 1 DecisionDomainConceptId FROM POLOXI.Legal_DecisionDomainConcept WHERE DecisionDomainPackId=@PiPackId AND ConceptCode=N'CA_PURE_COMPARATIVE_FAULT' AND TenantId IS NULL AND IsDeleted=0);
	DECLARE @ParentId UNIQUEIDENTIFIER=(SELECT TOP 1 DecisionDomainConceptId FROM POLOXI.Legal_DecisionDomainConcept WHERE DecisionDomainPackId=@PiPackId AND ConceptCode=N'COMPARATIVE_FAULT' AND TenantId IS NULL AND IsDeleted=0);
	DECLARE @ThresholdId UNIQUEIDENTIFIER=(SELECT TOP 1 DecisionDomainConceptId FROM POLOXI.Legal_DecisionDomainConcept WHERE DecisionDomainPackId=@PiPackId AND ConceptCode=N'COMPARATIVE_FAULT_THRESHOLD' AND TenantId IS NULL AND IsDeleted=0);

	IF @CaId IS NOT NULL AND @ParentId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM POLOXI.Legal_DecisionDomainConceptRelation WHERE DecisionDomainPackId=@PiPackId AND SourceConceptCode=N'CA_PURE_COMPARATIVE_FAULT' AND TargetConceptCode=N'COMPARATIVE_FAULT' AND RelationTypeCode=N'SPECIALIZES' AND TenantId IS NULL AND IsDeleted=0)
		INSERT POLOXI.Legal_DecisionDomainConceptRelation(DecisionDomainPackId,SourceDecisionDomainConceptId,TargetDecisionDomainConceptId,SourceConceptCode,TargetConceptCode,RelationTypeCode,ConstraintCode,Description,JurisdictionCode,MatterTypeCode,IsHardConstraint,IsActive,SortOrder,TenantId,CreatedDateUtc,CreatedByUserId,IsDeleted)
		VALUES(@PiPackId,@CaId,@ParentId,N'CA_PURE_COMPARATIVE_FAULT',N'COMPARATIVE_FAULT',N'SPECIALIZES',N'JURISDICTION_APPLICABILITY',N'Use only when authoritative matter jurisdiction or governing law is California.',N'CALIFORNIA',NULL,1,1,31,NULL,@Now,@SystemUserId,0);

	IF @CaId IS NOT NULL AND @ThresholdId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM POLOXI.Legal_DecisionDomainConceptRelation WHERE DecisionDomainPackId=@PiPackId AND SourceConceptCode=N'CA_PURE_COMPARATIVE_FAULT' AND TargetConceptCode=N'COMPARATIVE_FAULT_THRESHOLD' AND RelationTypeCode=N'SUPERSEDES' AND TenantId IS NULL AND IsDeleted=0)
		INSERT POLOXI.Legal_DecisionDomainConceptRelation(DecisionDomainPackId,SourceDecisionDomainConceptId,TargetDecisionDomainConceptId,SourceConceptCode,TargetConceptCode,RelationTypeCode,ConstraintCode,Description,JurisdictionCode,MatterTypeCode,IsHardConstraint,IsActive,SortOrder,TenantId,CreatedDateUtc,CreatedByUserId,IsDeleted)
		VALUES(@PiPackId,@CaId,@ThresholdId,N'CA_PURE_COMPARATIVE_FAULT',N'COMPARATIVE_FAULT_THRESHOLD',N'SUPERSEDES',N'JURISDICTION_APPLICABILITY',N'California has no comparative-fault bar threshold; the generic threshold concept is inapplicable when governing law is California.',N'CALIFORNIA',NULL,1,1,32,NULL,@Now,@SystemUserId,0);
END;

COMMIT TRANSACTION;
