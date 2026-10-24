-- ============================================================================
-- 0451: Separate Decision Landscapes + Directional Candidate-Dimension Scoring
--
-- WHY THIS EXISTS:
--   A single legal matter answers TWO materially different questions that must
--   NOT share one competition set or one relative-support denominator:
--
--     Contract A - PROCEDURAL_DISPOSITION : what happens to the pending motion
--                  (P1 Granted in full | P2 Granted in part | P3 Denied | P4 Other)
--     Contract B - MATTER_RESOLUTION      : how the underlying matter resolves
--                  (M1 Negotiated settlement | M2 Continued negotiation |
--                   M3 Plaintiff judgment | M4 Defense judgment/dismissal |
--                   M5 Voluntary withdrawal | M6 ADR award / procedural bar)
--
--   Collapsing these into one pool caused incompatible dispositions (plaintiff
--   verdict vs voluntary dismissal) to compete on one denominator, and let the
--   SAME positive evidence support contradictory outcomes because the only
--   polarity mechanism was the coarse ArchetypeCode gate (migration 0450).
--
-- WHAT THIS ADDS (DB is the source of truth; additive / idempotent):
--   1. Legal_DecisionDomainPackDecisionContract -- the generic contract layer
--      (ContractKind lets Employment/Contract packs add their own contracts).
--   2. DecisionContractCode + ContractKind columns on the existing outcome
--      candidate table (nullable; C1-C9 stay valid as legacy rows).
--   3. Legal_DecisionDomainPackOutcomeCandidateDimension -- the DIRECTIONAL
--      mapping (SUPPORTS | OPPOSES | NEUTRAL | UNKNOWN) + Weight +
--      IsEssentialElement. This is the missing piece: the same atomic
--      proposition can SUPPORT one outcome while OPPOSING its mirror, so LPI
--      back-propagation competes candidates by signed direction instead of
--      adding the same positive evidence to every candidate.
--   4. Legal_DecisionDomainPackOutcomeCandidateEvidenceRequirement -- the
--      essential-element / readiness gate linking outcomes to the evidence
--      types (migration 0284) that must be satisfied for decision-readiness,
--      separating scored coverage from verified evidence coverage.
--   5. Legal_DecisionContractInfluence -- cross-contract influence (e.g. a
--      procedural outcome constraining a resolution outcome). Table created
--      but UNSEEDED for v1 (contracts are independent); advisory only.
--
--   Seeds the two PERSONAL_INJURY contracts, P1-P4 and M1-M6 candidates, their
--   directional dimension vectors, and their essential-evidence requirements,
--   all keyed to the dimensions/evidence types already seeded by 0284. No
--   outcome name is hardcoded in application code; everything is read from here.
--
-- Depends on 0284 (pack/dimensions/evidence types) and 0396/0450 (outcome
-- candidate table + ArchetypeCode). Idempotent / safe to re-run.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────
-- 1. Legal_DecisionDomainPackDecisionContract
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackDecisionContract', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionDomainPackDecisionContract
(
	DecisionDomainPackDecisionContractId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionDomainPackDecisionContract PRIMARY KEY DEFAULT NEWID(),
	DecisionDomainPackId                 UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DecisionDomainPackDecisionContract_Pack REFERENCES POLOXI.Legal_DecisionDomainPack (DecisionDomainPackId),
	ContractCode                         NVARCHAR(60) NOT NULL,   -- PROCEDURAL_DISPOSITION | MATTER_RESOLUTION | ...
	ContractKind                         NVARCHAR(40) NOT NULL,   -- same taxonomy; lets future packs add kinds
	Name                                 NVARCHAR(200) NOT NULL,
	Description                          NVARCHAR(1000) NULL,
	DecisionQuestion                     NVARCHAR(600) NULL,      -- the human question the contract answers
	SortOrder                            INT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackDecisionContract_SortOrder DEFAULT 0,
	IsActive                             BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackDecisionContract_IsActive DEFAULT 1,
	TenantId                             UNIQUEIDENTIFIER NULL,
	CreatedDateUtc                       DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackDecisionContract_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId                      UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc                      DATETIME2 NULL,
	ModifiedByUserId                     UNIQUEIDENTIFIER NULL,
	IsDeleted                            BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackDecisionContract_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackDecisionContract') AND name = N'IX_Legal_DecisionDomainPackDecisionContract_Pack')
	CREATE INDEX IX_Legal_DecisionDomainPackDecisionContract_Pack
		ON POLOXI.Legal_DecisionDomainPackDecisionContract (DecisionDomainPackId, IsActive, SortOrder) INCLUDE (ContractCode, ContractKind) WHERE IsDeleted = 0;

GO

-- ────────────────────────────────────────────────────────────────────────────
-- 2. Outcome-candidate contract membership (nullable/additive; C1-C9 unaffected)
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackOutcomeCandidate', N'U') IS NOT NULL
	AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackOutcomeCandidate') AND name = N'DecisionContractCode')
	ALTER TABLE POLOXI.Legal_DecisionDomainPackOutcomeCandidate ADD DecisionContractCode NVARCHAR(60) NULL;

IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackOutcomeCandidate', N'U') IS NOT NULL
	AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackOutcomeCandidate') AND name = N'ContractKind')
	ALTER TABLE POLOXI.Legal_DecisionDomainPackOutcomeCandidate ADD ContractKind NVARCHAR(40) NULL;

GO

-- ────────────────────────────────────────────────────────────────────────────
-- 3. Legal_DecisionDomainPackOutcomeCandidateDimension (directional mapping)
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackOutcomeCandidateDimension', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionDomainPackOutcomeCandidateDimension
(
	DecisionDomainPackOutcomeCandidateDimensionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionDomainPackOutcomeCandidateDimension PRIMARY KEY DEFAULT NEWID(),
	DecisionDomainPackId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DecisionDomainPackOutcomeCandidateDimension_Pack REFERENCES POLOXI.Legal_DecisionDomainPack (DecisionDomainPackId),
	OutcomeCode           NVARCHAR(60) NOT NULL,   -- joins Legal_DecisionDomainPackOutcomeCandidate.OutcomeCode
	DimensionCode         NVARCHAR(60) NOT NULL,   -- joins Legal_DecisionDomainPackDimension.DimensionCode
	DirectionCode         NVARCHAR(20) NOT NULL CONSTRAINT CK_Legal_DecisionDomainPackOutcomeCandidateDimension_Direction
		CHECK (DirectionCode IN (N'SUPPORTS', N'OPPOSES', N'NEUTRAL', N'UNKNOWN')),
	Weight                DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackOutcomeCandidateDimension_Weight DEFAULT 0.5000,  -- 0..1 relative influence
	IsEssentialElement    BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackOutcomeCandidateDimension_Essential DEFAULT 0,            -- gating element for readiness
	Rationale             NVARCHAR(600) NULL,
	SortOrder             INT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackOutcomeCandidateDimension_SortOrder DEFAULT 0,
	IsActive              BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackOutcomeCandidateDimension_IsActive DEFAULT 1,
	TenantId              UNIQUEIDENTIFIER NULL,
	CreatedDateUtc        DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackOutcomeCandidateDimension_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId       UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc       DATETIME2 NULL,
	ModifiedByUserId      UNIQUEIDENTIFIER NULL,
	IsDeleted             BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackOutcomeCandidateDimension_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackOutcomeCandidateDimension') AND name = N'IX_Legal_DecisionDomainPackOutcomeCandidateDimension_Lookup')
	CREATE INDEX IX_Legal_DecisionDomainPackOutcomeCandidateDimension_Lookup
		ON POLOXI.Legal_DecisionDomainPackOutcomeCandidateDimension (DecisionDomainPackId, OutcomeCode, IsActive) INCLUDE (DimensionCode, DirectionCode, Weight, IsEssentialElement) WHERE IsDeleted = 0;

GO

-- ────────────────────────────────────────────────────────────────────────────
-- 4. Legal_DecisionDomainPackOutcomeCandidateEvidenceRequirement (readiness gate)
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackOutcomeCandidateEvidenceRequirement', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionDomainPackOutcomeCandidateEvidenceRequirement
(
	DecisionDomainPackOutcomeCandidateEvidenceRequirementId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionDomainPackOutcomeCandidateEvidenceRequirement PRIMARY KEY DEFAULT NEWID(),
	DecisionDomainPackId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DecisionDomainPackOutcomeCandidateEvidenceRequirement_Pack REFERENCES POLOXI.Legal_DecisionDomainPack (DecisionDomainPackId),
	OutcomeCode               NVARCHAR(60) NOT NULL,   -- joins Legal_DecisionDomainPackOutcomeCandidate.OutcomeCode
	EvidenceTypeCode          NVARCHAR(60) NOT NULL,   -- joins Legal_DecisionDomainPackEvidenceType.EvidenceTypeCode
	IsEssential               BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackOutcomeCandidateEvidenceRequirement_Essential DEFAULT 1,  -- gating vs corroborating
	MinVerificationProfileCode NVARCHAR(60) NULL,      -- joins Legal_DecisionDomainPackVerificationProfile.ProfileCode
	Rationale                 NVARCHAR(600) NULL,
	SortOrder                 INT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackOutcomeCandidateEvidenceRequirement_SortOrder DEFAULT 0,
	IsActive                  BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackOutcomeCandidateEvidenceRequirement_IsActive DEFAULT 1,
	TenantId                  UNIQUEIDENTIFIER NULL,
	CreatedDateUtc            DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackOutcomeCandidateEvidenceRequirement_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId           UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc           DATETIME2 NULL,
	ModifiedByUserId          UNIQUEIDENTIFIER NULL,
	IsDeleted                 BIT NOT NULL CONSTRAINT DF_Legal_DecisionDomainPackOutcomeCandidateEvidenceRequirement_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackOutcomeCandidateEvidenceRequirement') AND name = N'IX_Legal_DecisionDomainPackOutcomeCandidateEvidenceRequirement_Lookup')
	CREATE INDEX IX_Legal_DecisionDomainPackOutcomeCandidateEvidenceRequirement_Lookup
		ON POLOXI.Legal_DecisionDomainPackOutcomeCandidateEvidenceRequirement (DecisionDomainPackId, OutcomeCode, IsActive) INCLUDE (EvidenceTypeCode, IsEssential, MinVerificationProfileCode) WHERE IsDeleted = 0;

GO

-- ────────────────────────────────────────────────────────────────────────────
-- 5. Legal_DecisionContractInfluence (cross-contract; created UNSEEDED for v1)
-- ────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_DecisionContractInfluence', N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionContractInfluence
(
	DecisionContractInfluenceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionContractInfluence PRIMARY KEY DEFAULT NEWID(),
	DecisionDomainPackId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DecisionContractInfluence_Pack REFERENCES POLOXI.Legal_DecisionDomainPack (DecisionDomainPackId),
	SourceContractCode          NVARCHAR(60) NOT NULL,   -- e.g. PROCEDURAL_DISPOSITION
	SourceOutcomeCode           NVARCHAR(60) NOT NULL,   -- e.g. P1
	TargetContractCode          NVARCHAR(60) NOT NULL,   -- e.g. MATTER_RESOLUTION
	TargetOutcomeCode           NVARCHAR(60) NOT NULL,   -- e.g. M4
	InfluenceDirection          NVARCHAR(20) NOT NULL CONSTRAINT CK_Legal_DecisionContractInfluence_Direction
		CHECK (InfluenceDirection IN (N'SUPPORTS', N'OPPOSES', N'IMPLIES', N'EXCLUDES')),
	Weight                      DECIMAL(5,4) NOT NULL CONSTRAINT DF_Legal_DecisionContractInfluence_Weight DEFAULT 0.5000,
	IsAdvisoryOnly              BIT NOT NULL CONSTRAINT DF_Legal_DecisionContractInfluence_Advisory DEFAULT 1,  -- v1: advisory, never auto-applied
	Rationale                   NVARCHAR(600) NULL,
	SortOrder                   INT NOT NULL CONSTRAINT DF_Legal_DecisionContractInfluence_SortOrder DEFAULT 0,
	IsActive                    BIT NOT NULL CONSTRAINT DF_Legal_DecisionContractInfluence_IsActive DEFAULT 1,
	TenantId                    UNIQUEIDENTIFIER NULL,
	CreatedDateUtc              DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionContractInfluence_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId             UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc             DATETIME2 NULL,
	ModifiedByUserId            UNIQUEIDENTIFIER NULL,
	IsDeleted                   BIT NOT NULL CONSTRAINT DF_Legal_DecisionContractInfluence_IsDeleted DEFAULT 0
);

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionContractInfluence') AND name = N'IX_Legal_DecisionContractInfluence_Lookup')
	CREATE INDEX IX_Legal_DecisionContractInfluence_Lookup
		ON POLOXI.Legal_DecisionContractInfluence (DecisionDomainPackId, SourceContractCode, SourceOutcomeCode, IsActive) INCLUDE (TargetContractCode, TargetOutcomeCode, InfluenceDirection, Weight) WHERE IsDeleted = 0;

GO

-- ════════════════════════════════════════════════════════════════════════════
-- SEED: PERSONAL_INJURY contracts, candidates, directional vectors, evidence reqs
-- ════════════════════════════════════════════════════════════════════════════
DECLARE @PiPackId UNIQUEIDENTIFIER =
	(SELECT TOP 1 DecisionDomainPackId FROM POLOXI.Legal_DecisionDomainPack
	 WHERE PackCode = N'PERSONAL_INJURY' AND TenantId IS NULL AND IsDeleted = 0);

IF @PiPackId IS NOT NULL
BEGIN
	-- ── 6. Decision Contracts (A: procedural disposition, B: matter resolution) ──
	MERGE POLOXI.Legal_DecisionDomainPackDecisionContract AS target
	USING (VALUES
		(N'PROCEDURAL_DISPOSITION', N'PROCEDURAL_DISPOSITION', N'Procedural Disposition',
		 N'The disposition of the pending procedural motion (e.g. the plaintiff''s all-claims summary-judgment motion).',
		 N'What happens to the pending procedural motion?', 10),
		(N'MATTER_RESOLUTION', N'MATTER_RESOLUTION', N'Matter Resolution',
		 N'How the underlying personal injury matter ultimately resolves, independent of any single interim motion.',
		 N'How might the underlying personal injury matter resolve?', 20)
	) AS source (ContractCode, ContractKind, Name, Description, DecisionQuestion, SortOrder)
	ON target.DecisionDomainPackId = @PiPackId AND target.ContractCode = source.ContractCode AND target.TenantId IS NULL
	WHEN NOT MATCHED BY TARGET THEN
		INSERT (DecisionDomainPackId, ContractCode, ContractKind, Name, Description, DecisionQuestion, SortOrder, TenantId)
		VALUES (@PiPackId, source.ContractCode, source.ContractKind, source.Name, source.Description, source.DecisionQuestion, source.SortOrder, NULL);

	-- ── 7. Outcome candidates: P1-P4 (procedural) and M1-M6 (resolution) ──
	-- ArchetypeCode reuses the 0450 gate taxonomy; RoleCode PATHWAY (prospective).
	DECLARE @Candidates TABLE (
		OutcomeCode NVARCHAR(60), Name NVARCHAR(200), Description NVARCHAR(1000),
		ContractCode NVARCHAR(60), ContractKind NVARCHAR(40), ArchetypeCode NVARCHAR(40), SortOrder INT);
	INSERT INTO @Candidates (OutcomeCode, Name, Description, ContractCode, ContractKind, ArchetypeCode, SortOrder) VALUES
		-- Contract A — Procedural disposition (plaintiff's all-claims SJ motion)
		(N'P1', N'Granted in full',
		 N'The plaintiff''s all-claims summary-judgment motion is granted in full: no genuine dispute of material fact on any element, so the plaintiff prevails as a matter of law.',
		 N'PROCEDURAL_DISPOSITION', N'PROCEDURAL_DISPOSITION', N'PLAINTIFF_MERITS', 10),
		(N'P2', N'Granted in part',
		 N'The motion is granted as to some claims/elements and denied as to others — a mixed ruling where the record is decisive on part of the case only.',
		 N'PROCEDURAL_DISPOSITION', N'PROCEDURAL_DISPOSITION', N'NEUTRAL', 20),
		(N'P3', N'Denied',
		 N'The motion is denied: at least one genuine dispute of material fact survives, so the claims proceed and the moving party does not prevail on the papers.',
		 N'PROCEDURAL_DISPOSITION', N'PROCEDURAL_DISPOSITION', N'DEFENSE_MERITS', 30),
		(N'P4', N'Other procedural disposition',
		 N'Another procedural outcome (continuance, deferral pending discovery, denial without prejudice, administrative disposition) that neither grants nor denies on the merits.',
		 N'PROCEDURAL_DISPOSITION', N'PROCEDURAL_DISPOSITION', N'NEUTRAL', 40),
		-- Contract B — Matter resolution
		(N'M1', N'Negotiated settlement',
		 N'A negotiated settlement concludes the matter, avoiding the uncertainty of proving every element and resolving disputed comparative fault at trial.',
		 N'MATTER_RESOLUTION', N'MATTER_RESOLUTION', N'SETTLEMENT', 10),
		(N'M2', N'Continued negotiation',
		 N'Negotiation or mediation continues without a concluded settlement, keeping the matter in an active-resolution posture.',
		 N'MATTER_RESOLUTION', N'MATTER_RESOLUTION', N'SETTLEMENT', 20),
		(N'M3', N'Plaintiff judgment',
		 N'An adjudicated result for the plaintiff on the merits: duty, breach, causation, and damages are each established to the applicable burden with comparative fault addressed.',
		 N'MATTER_RESOLUTION', N'MATTER_RESOLUTION', N'PLAINTIFF_MERITS', 30),
		(N'M4', N'Defense judgment / dismissal',
		 N'A judgment or dismissal for the defense: an essential element fails its burden, or a defense (comparative fault, causation gap, limitations) defeats the claim.',
		 N'MATTER_RESOLUTION', N'MATTER_RESOLUTION', N'DEFENSE_MERITS', 40),
		(N'M5', N'Voluntary withdrawal',
		 N'The plaintiff voluntarily dismisses or withdraws the claim — a plaintiff-initiated, non-merits resolution that neither adjudicates liability nor settles value.',
		 N'MATTER_RESOLUTION', N'MATTER_RESOLUTION', N'NEUTRAL', 50),
		(N'M6', N'ADR award / procedural bar',
		 N'Resolution by arbitration/ADR award or by a procedural bar (limitations, jurisdiction) that terminates the matter outside a conventional merits judgment.',
		 N'MATTER_RESOLUTION', N'MATTER_RESOLUTION', N'SETTLEMENT', 60);

	INSERT INTO POLOXI.Legal_DecisionDomainPackOutcomeCandidate
		(DecisionDomainPackOutcomeCandidateId, DecisionDomainPackId, OutcomeCode, Name, Description, RoleCode, RequiresVerification, MatterTypeCode, SortOrder, IsActive, TenantId, ArchetypeCode, DecisionContractCode, ContractKind)
	SELECT NEWID(), @PiPackId, c.OutcomeCode, c.Name, c.Description, N'PATHWAY', 0, N'PERSONAL_INJURY', c.SortOrder, 1, NULL, c.ArchetypeCode, c.ContractCode, c.ContractKind
	FROM @Candidates c
	WHERE NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDomainPackOutcomeCandidate x
					  WHERE x.DecisionDomainPackId = @PiPackId AND x.OutcomeCode = c.OutcomeCode AND x.TenantId IS NULL AND x.IsDeleted = 0);

	-- Backfill contract membership on any pre-existing P/M rows (idempotent re-run safety).
	UPDATE oc
	SET oc.DecisionContractCode = c.ContractCode,
		oc.ContractKind = c.ContractKind,
		oc.ArchetypeCode = COALESCE(oc.ArchetypeCode, c.ArchetypeCode),
		oc.ModifiedDateUtc = SYSUTCDATETIME()
	FROM POLOXI.Legal_DecisionDomainPackOutcomeCandidate oc
	INNER JOIN @Candidates c ON c.OutcomeCode = oc.OutcomeCode
	WHERE oc.DecisionDomainPackId = @PiPackId AND oc.TenantId IS NULL AND oc.IsDeleted = 0
	  AND (oc.DecisionContractCode IS NULL OR oc.ContractKind IS NULL);

	-- ── 8. Directional candidate × dimension vectors ──
	-- DirectionCode: SUPPORTS (dimension strength raises this outcome), OPPOSES
	-- (dimension strength lowers it), NEUTRAL (tension/irrelevant). IsEssential=1
	-- flags a gating legal element for readiness. Mirror outcomes (M3/M4, P1/P3)
	-- carry opposite directions on the shared element axes so the SAME proposition
	-- cannot inflate contradictory dispositions.
	DECLARE @Dim TABLE (OutcomeCode NVARCHAR(60), DimensionCode NVARCHAR(60), DirectionCode NVARCHAR(20), Weight DECIMAL(5,4), IsEssentialElement BIT, Rationale NVARCHAR(600), SortOrder INT);
	INSERT INTO @Dim (OutcomeCode, DimensionCode, DirectionCode, Weight, IsEssentialElement, Rationale, SortOrder) VALUES
		-- P1 Granted in full (plaintiff SJ): every element must be undisputed as a matter of law.
		(N'P1', N'LIABILITY',  N'SUPPORTS', 1.0000, 1, N'SJ for plaintiff requires undisputed duty/breach.', 10),
		(N'P1', N'CAUSATION',  N'SUPPORTS', 1.0000, 1, N'Causation must be undisputed to win on the papers.', 20),
		(N'P1', N'EVIDENCE',   N'SUPPORTS', 0.9000, 1, N'No genuine dispute of material fact on the record.', 30),
		(N'P1', N'DEFENSES',   N'OPPOSES',  0.9000, 0, N'A viable defense defeats summary judgment for plaintiff.', 40),
		(N'P1', N'DAMAGES',    N'NEUTRAL',  0.3000, 0, N'SJ on liability does not require damages quantum.', 50),
		(N'P1', N'PROCEDURE',  N'SUPPORTS', 0.5000, 0, N'Proper posture/timing supports granting.', 60),
		-- P2 Granted in part (mixed): partial record sufficiency.
		(N'P2', N'LIABILITY',  N'SUPPORTS', 0.6000, 0, N'Some elements decisively established, others not.', 10),
		(N'P2', N'EVIDENCE',   N'SUPPORTS', 0.5000, 0, N'Record resolves part of the dispute only.', 20),
		(N'P2', N'DEFENSES',   N'NEUTRAL',  0.4000, 0, N'Defenses survive on the undecided claims.', 30),
		(N'P2', N'DAMAGES',    N'NEUTRAL',  0.3000, 0, N'Damages typically reserved.', 40),
		-- P3 Denied (defense survives motion): genuine disputes exist.
		(N'P3', N'DEFENSES',   N'SUPPORTS', 0.8000, 1, N'Genuine disputes/defenses defeat the motion.', 10),
		(N'P3', N'EVIDENCE',   N'OPPOSES',  0.8000, 1, N'Disputed material facts preclude summary judgment.', 20),
		(N'P3', N'LIABILITY',  N'OPPOSES',  0.6000, 0, N'Liability remains genuinely contested.', 30),
		(N'P3', N'CAUSATION',  N'OPPOSES',  0.6000, 0, N'Causation remains genuinely contested.', 40),
		-- P4 Other procedural: non-merits posture signals.
		(N'P4', N'PROCEDURE',  N'SUPPORTS', 0.7000, 0, N'Procedural posture/discovery gaps drive deferral.', 10),
		(N'P4', N'EVIDENCE',   N'NEUTRAL',  0.3000, 0, N'Record not yet ripe for merits ruling.', 20),
		-- M1 Negotiated settlement (tension peaks when merits are genuinely mixed).
		(N'M1', N'SETTLEMENT', N'SUPPORTS', 0.9000, 1, N'Active demand/offer activity drives settlement.', 10),
		(N'M1', N'DAMAGES',    N'SUPPORTS', 0.6000, 0, N'Quantifiable damages create settlement value.', 20),
		(N'M1', N'INSURANCE',  N'SUPPORTS', 0.6000, 0, N'Available coverage/limits enable settlement.', 30),
		(N'M1', N'LIABILITY',  N'NEUTRAL',  0.5000, 0, N'Settlement peaks under mixed liability, not decisive merits.', 40),
		(N'M1', N'DEFENSES',   N'NEUTRAL',  0.4000, 0, N'Live defenses increase settlement incentive.', 50),
		-- M2 Continued negotiation (earlier-stage settlement posture).
		(N'M2', N'SETTLEMENT', N'SUPPORTS', 0.7000, 1, N'Ongoing negotiation without concluded terms.', 10),
		(N'M2', N'DAMAGES',    N'NEUTRAL',  0.4000, 0, N'Damages not yet crystallized.', 20),
		(N'M2', N'LIABILITY',  N'NEUTRAL',  0.5000, 0, N'Parties still testing positions.', 30),
		(N'M2', N'EVIDENCE',   N'NEUTRAL',  0.4000, 0, N'Record still developing.', 40),
		-- M3 Plaintiff judgment (conjunctive merits): mirror of M4.
		(N'M3', N'LIABILITY',  N'SUPPORTS', 1.0000, 1, N'Plaintiff must establish duty and breach.', 10),
		(N'M3', N'CAUSATION',  N'SUPPORTS', 1.0000, 1, N'Causal link from incident to injury is essential.', 20),
		(N'M3', N'INJURY',     N'SUPPORTS', 0.8000, 1, N'Compensable injury must be proven.', 30),
		(N'M3', N'DAMAGES',    N'SUPPORTS', 0.8000, 1, N'Damages quantum must be established.', 40),
		(N'M3', N'EVIDENCE',   N'SUPPORTS', 0.7000, 0, N'Admissible proof carries each element.', 50),
		(N'M3', N'DEFENSES',   N'OPPOSES',  0.9000, 0, N'Comparative fault/defenses reduce or defeat recovery.', 60),
		-- M4 Defense judgment / dismissal (disjunctive over failures): mirror of M3.
		(N'M4', N'LIABILITY',  N'OPPOSES',  1.0000, 1, N'Weak liability drives a defense result.', 10),
		(N'M4', N'CAUSATION',  N'OPPOSES',  1.0000, 1, N'A causation gap defeats the claim.', 20),
		(N'M4', N'DEFENSES',   N'SUPPORTS', 0.9000, 1, N'An established defense wins for the defense.', 30),
		(N'M4', N'DAMAGES',    N'OPPOSES',  0.5000, 0, N'Unproven damages undercut recovery.', 40),
		(N'M4', N'PROCEDURE',  N'SUPPORTS', 0.6000, 0, N'A procedural/limitations bar aids the defense.', 50),
		(N'M4', N'EVIDENCE',   N'OPPOSES',  0.6000, 0, N'Thin proof favors the defense.', 60),
		-- M5 Voluntary withdrawal (plaintiff-initiated non-merits exit).
		(N'M5', N'LIABILITY',  N'OPPOSES',  0.5000, 0, N'Weak liability motivates withdrawal.', 10),
		(N'M5', N'EVIDENCE',   N'OPPOSES',  0.5000, 0, N'Evidentiary gaps motivate withdrawal.', 20),
		(N'M5', N'PROCEDURE',  N'SUPPORTS', 0.5000, 0, N'Procedural exposure can prompt withdrawal.', 30),
		(N'M5', N'DAMAGES',    N'OPPOSES',  0.4000, 0, N'Low recoverable value motivates withdrawal.', 40),
		-- M6 ADR award / procedural bar.
		(N'M6', N'SETTLEMENT', N'SUPPORTS', 0.6000, 0, N'ADR/arbitration channel resolves the matter.', 10),
		(N'M6', N'PROCEDURE',  N'SUPPORTS', 0.6000, 0, N'A procedural bar can terminate the matter.', 20),
		(N'M6', N'INSURANCE',  N'SUPPORTS', 0.4000, 0, N'Coverage terms may compel ADR.', 30),
		(N'M6', N'DEFENSES',   N'NEUTRAL',  0.4000, 0, N'Bar/ADR sidesteps a full merits contest.', 40);

	INSERT INTO POLOXI.Legal_DecisionDomainPackOutcomeCandidateDimension
		(DecisionDomainPackOutcomeCandidateDimensionId, DecisionDomainPackId, OutcomeCode, DimensionCode, DirectionCode, Weight, IsEssentialElement, Rationale, SortOrder, IsActive, TenantId)
	SELECT NEWID(), @PiPackId, d.OutcomeCode, d.DimensionCode, d.DirectionCode, d.Weight, d.IsEssentialElement, d.Rationale, d.SortOrder, 1, NULL
	FROM @Dim d
	WHERE NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDomainPackOutcomeCandidateDimension x
					  WHERE x.DecisionDomainPackId = @PiPackId AND x.OutcomeCode = d.OutcomeCode AND x.DimensionCode = d.DimensionCode AND x.TenantId IS NULL AND x.IsDeleted = 0);

	-- ── 9. Essential evidence-type requirements (readiness / verified-coverage gate) ──
	DECLARE @Evid TABLE (OutcomeCode NVARCHAR(60), EvidenceTypeCode NVARCHAR(60), IsEssential BIT, MinVerificationProfileCode NVARCHAR(60), Rationale NVARCHAR(600), SortOrder INT);
	INSERT INTO @Evid (OutcomeCode, EvidenceTypeCode, IsEssential, MinVerificationProfileCode, Rationale, SortOrder) VALUES
		-- P1 Granted in full: undisputed facts require corroborated liability record.
		(N'P1', N'ACCIDENT_REPORT',    1, N'LIABILITY_FACTS',   N'Official report anchors undisputed incident facts.', 10),
		(N'P1', N'WITNESS_STATEMENTS', 1, N'LIABILITY_FACTS',   N'Consistent witness accounts negate factual disputes.', 20),
		(N'P1', N'DEPOSITION',         1, N'LIABILITY_FACTS',   N'Sworn admissions remove genuine disputes.', 30),
		(N'P1', N'EXPERT_REPORTS',     0, N'EXPERT_FOUNDATION', N'Unrebutted expert opinion supports judgment as a matter of law.', 40),
		-- P3 Denied: disputed facts, so conflicting evidence is the signal (corroborating).
		(N'P3', N'DEPOSITION',         0, NULL,                 N'Conflicting testimony evidences genuine dispute.', 10),
		(N'P3', N'WITNESS_STATEMENTS', 0, NULL,                 N'Divergent accounts preclude summary judgment.', 20),
		-- M3 Plaintiff judgment: each merits element needs its proof.
		(N'M3', N'MEDICAL_RECORDS',    1, N'MEDICAL_CAUSATION', N'Treating records establish injury and causation.', 10),
		(N'M3', N'EXPERT_REPORTS',     1, N'EXPERT_FOUNDATION', N'Expert opinion supports causation and permanency.', 20),
		(N'M3', N'ACCIDENT_REPORT',    1, N'LIABILITY_FACTS',   N'Incident record supports duty/breach.', 30),
		(N'M3', N'MEDICAL_BILLS',      1, N'DAMAGES_QUANTUM',   N'Bills establish economic damages.', 40),
		(N'M3', N'IMAGING',            0, N'MEDICAL_CAUSATION', N'Objective imaging corroborates injury.', 50),
		(N'M3', N'WAGE_RECORDS',       0, N'DAMAGES_QUANTUM',   N'Wage records support lost-earnings damages.', 60),
		-- M4 Defense judgment / dismissal: evidence that undercuts an element or proves a defense.
		(N'M4', N'EXPERT_REPORTS',     1, N'EXPERT_FOUNDATION', N'Defense expert rebuts causation.', 10),
		(N'M4', N'DEPOSITION',         1, N'LIABILITY_FACTS',   N'Admissions/impeachment establish a defense.', 20),
		(N'M4', N'MEDICAL_RECORDS',    0, N'MEDICAL_CAUSATION', N'Pre-existing conditions sever causation.', 30),
		-- M1 Negotiated settlement: settlement-channel artifacts.
		(N'M1', N'DEMAND_OFFER',       1, NULL,                 N'Demand/offer correspondence evidences active settlement.', 10),
		(N'M1', N'MEDICAL_BILLS',      0, N'DAMAGES_QUANTUM',   N'Specials anchor settlement valuation.', 20),
		-- M2 Continued negotiation: earlier settlement activity.
		(N'M2', N'DEMAND_OFFER',       1, NULL,                 N'Open demand/offer thread without conclusion.', 10),
		-- M6 ADR award / procedural bar.
		(N'M6', N'DEMAND_OFFER',       0, NULL,                 N'ADR submissions/awards in the correspondence record.', 10);

	INSERT INTO POLOXI.Legal_DecisionDomainPackOutcomeCandidateEvidenceRequirement
		(DecisionDomainPackOutcomeCandidateEvidenceRequirementId, DecisionDomainPackId, OutcomeCode, EvidenceTypeCode, IsEssential, MinVerificationProfileCode, Rationale, SortOrder, IsActive, TenantId)
	SELECT NEWID(), @PiPackId, e.OutcomeCode, e.EvidenceTypeCode, e.IsEssential, e.MinVerificationProfileCode, e.Rationale, e.SortOrder, 1, NULL
	FROM @Evid e
	WHERE NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDomainPackOutcomeCandidateEvidenceRequirement x
					  WHERE x.DecisionDomainPackId = @PiPackId AND x.OutcomeCode = e.OutcomeCode AND x.EvidenceTypeCode = e.EvidenceTypeCode AND x.TenantId IS NULL AND x.IsDeleted = 0);
END

GO

COMMIT TRANSACTION;
GO
