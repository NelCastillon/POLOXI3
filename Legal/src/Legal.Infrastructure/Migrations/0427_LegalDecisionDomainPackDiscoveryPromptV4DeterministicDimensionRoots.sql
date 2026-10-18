SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- DECISION_DISCOVERY_DOMAIN_PACK_V4 (idempotent).
--
-- Aligns the persisted Domain Pack discovery prefix with the deterministic L1/L2 materialization now
-- performed by the platform (every active Dimension is its own L1 root; L2 comes from the pack's
-- EvidenceTypes + FACTOR Concepts). V3 still told the model that Dimensions are "coverage guidance"
-- and to "not emit every dimension automatically", which DIRECTLY CONTRADICTED the runtime
-- SELECTED_DOMAIN_PACK interpretation rules sent in the same payload. V4 removes that contradiction
-- and adds the FULL CONTEXT BOUNDARY + SOURCE-FIRST generation + COVERAGE/OUTPUT sections that
-- previously existed only in the runtime overlay.
--
-- To guarantee V4 never diverges from V3 beyond these intended edits, this migration DERIVES V4's
-- text from the stored V3 row (SystemPrompt copied verbatim; UserPromptTemplate = V3 with the
-- DOMAIN-GROUNDED HIERARCHY paragraphs and one PRE-RETURN CHECK bullet replaced, and the new sections
-- inserted immediately before V3's closing ORIGINAL_PROMPT paragraph). V1/V2/V3 rows and the original
-- DECISION_DISCOVERY / DECISION_DISCOVERY_V2 prompt are NOT touched. POLOXI Core scoring, competition,
-- normalization, output schema, and canonical outcome injection are all unchanged.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @V3System   NVARCHAR(MAX) =
	(SELECT TOP 1 SystemPrompt        FROM POLOXI.Legal_DecisionPrompt WHERE PromptCode = N'DECISION_DISCOVERY_DOMAIN_PACK_V3');
DECLARE @V3Template NVARCHAR(MAX) =
	(SELECT TOP 1 UserPromptTemplate  FROM POLOXI.Legal_DecisionPrompt WHERE PromptCode = N'DECISION_DISCOVERY_DOMAIN_PACK_V3');
DECLARE @V3Stage    NVARCHAR(100)  =
	(SELECT TOP 1 StageCode           FROM POLOXI.Legal_DecisionPrompt WHERE PromptCode = N'DECISION_DISCOVERY_DOMAIN_PACK_V3');

IF @V3Template IS NOT NULL
BEGIN
	DECLARE @LF   NCHAR(1) = NCHAR(10);
	DECLARE @CRLF NCHAR(2) = NCHAR(13) + NCHAR(10);

	-- Normalize to LF so anchors match regardless of how the row was stored; restore CRLF at the end
	-- only if the source used it.
	DECLARE @UsesCrlf BIT = CASE WHEN CHARINDEX(@CRLF, @V3Template) > 0 THEN 1 ELSE 0 END;
	DECLARE @T NVARCHAR(MAX) = REPLACE(@V3Template, @CRLF, @LF);

	-- ── 1. Dimension → L1 ownership (replace the V1 "coverage guidance" paragraphs) ─────────────────
	DECLARE @OldDimA NVARCHAR(MAX) =
		N'Use Domain Pack Dimensions as coverage guidance for relevant L1 roots.' + @LF +
		N'Do not emit every dimension automatically.' + @LF + @LF +
		N'Where the pack contains overlapping or parent/child dimensions,' + @LF +
		N'preserve their meaning without creating redundant independent roots.';
	DECLARE @NewDimA NVARCHAR(MAX) =
		N'Each active Dimension in SELECTED_DOMAIN_PACK IS its own L1 root.' + @LF +
		N'Emit exactly one L1 per Dimension, using the DimensionCode as the branch' + @LF +
		N'code and preserving its supplied name and definition. Do not merge,' + @LF +
		N'consolidate, rename, reparent, or drop Dimensions into fewer roots, and' + @LF +
		N'do not invent additional L1 roots that duplicate a Dimension.' + @LF + @LF +
		N'L2 nodes are supplied by the Domain Pack and sit under their own' + @LF +
		N'Dimension''s L1 root (EvidenceType.DimensionCode / Concept.DimensionCode' + @LF +
		N'names the parent). EvidenceTypes (what proves a factor) and FACTOR-kind' + @LF +
		N'Concepts (determining sub-issues) are L2 siblings. Preserve their exact' + @LF +
		N'codes, names, definitions, and parent links; never place them under a' + @LF +
		N'single flat Evidence root. A Concept is never an OutcomeCandidate. An' + @LF +
		N'EvidenceType definition guides investigation; it does not establish that' + @LF +
		N'a corresponding evidence item exists or that its contents are known.' + @LF + @LF +
		N'Relations are dependency EDGES (SourceConceptCode -> TargetConceptCode' + @LF +
		N'with RelationTypeCode), not tree nodes. VerificationProfiles are node' + @LF +
		N'METADATA attached by EvidenceTypeCode / VerificationProfileCode, not a' + @LF +
		N'level. ResolvedMatterTypeCode is the run-scope selector above the tree,' + @LF +
		N'not a hierarchy node.' + @LF + @LF +
		N'Generate only matter-specific L3 to Ln proposals beneath the assigned L2' + @LF +
		N'nodes. L3 is not necessarily the final atomic level; decompose further' + @LF +
		N'only where materially useful and within the configured depth envelope.';

	DECLARE @OldDimB NVARCHAR(MAX) =
		N'The Domain Pack is coverage guidance, not a closed list.' + @LF +
		N'Propose a materially necessary factor absent from the pack when allowed' + @LF +
		N'by the original prompt. Do not invent a Domain Pack code for it.';
	DECLARE @NewDimB NVARCHAR(MAX) =
		N'The Domain Pack L1/L2 structure is authoritative and complete for this' + @LF +
		N'run. A materially necessary factor absent from the pack may be proposed' + @LF +
		N'only at L3 or deeper, under the L2 whose definition it refines, and only' + @LF +
		N'when allowed by the original prompt. Do not invent a Domain Pack code' + @LF +
		N'for it and do not create a new L1 or L2 for it.';

	DECLARE @OldCheck NVARCHAR(MAX) =
		N'- relevant Domain Pack dimensions informed coverage without automatic' + @LF +
		N'  inclusion of irrelevant entries;';
	DECLARE @NewCheck NVARCHAR(MAX) =
		N'- every active Domain Pack Dimension is present as exactly one L1 root' + @LF +
		N'  and every supplied L2 sits under its own Dimension;';

	-- ── 2. Sections previously only in the runtime overlay ─────────────────────────────────────────
	DECLARE @NewSections NVARCHAR(MAX) =
N'FULL CONTEXT BOUNDARY

Consider ALL authorized context supplied to this execution: the decision
request, objectives, jurisdiction and procedural posture; matter
description, narrative and incident information; every practice-area
profile and profile section; intake responses, custom fields and
structured values; parties, participants, roles and relationships;
dates, chronology, treatment, expenses, coverage and related records;
lifecycle stage, events, milestones and constraints; documents and
retrieved passages; correspondence, notes and attributed witness
accounts; supplied media observations and machine-data records;
supplied legal-authority passages and verification metadata; attorney
propositions, interpretations, corrections and review decisions; and
prior assessment information when explicitly supplied.

Do not assume access to information omitted from this execution, and
do not treat missing input as proof that the underlying information
does not exist. Preserve each input''s origin, author/source, version,
relevant date, and verification status where available. Profile fields
and matter descriptions are usable sources of supplied information;
they are not automatically independently verified evidence.

SOURCE-FIRST, BRANCH-DIRECTED GENERATION

For each assigned L2:
1. interpret its definition in the context of the current decision;
2. inspect relevant supplied context across profiles, descriptions,
   structured fields, related records and source material;
3. propose independently assessable statements supported by that
   context;
4. identify unresolved material conditions requiring further
   information;
5. include contrary interpretations and information where material;
6. propose the appropriate L3 to Ln decomposition within the depth
   limit.

Do not restrict extraction to documents. Do not infer document contents
from a profile''s reference to that document. Do not invent assertions
merely to populate every L2.

L3+ MATERIALITY GATE

Deeper levels are generated, not supplied. Create a child L(n+1) ONLY
when it materially changes at least one of: an interpretation, a
candidate assessment, a discriminator between candidates, an evidence
need, a verification obligation, a decision dependency, or a material
uncertainty. Otherwise fold it into its parent. Prefer the shallowest
tree that captures every material distinction; depth and breadth are
costs, not goals. Do not restate a parent, split a node merely for
symmetry, mirror the same sub-structure under unrelated branches, or
expand a branch the matter facts do not activate.

COVERAGE AND OUTPUT

Return an explicit result for every assigned L2 using existing
permitted structures, distinguishing: processing completed with
proposition proposals; completed with unresolved information needs;
applicability unresolved; not applicable with an explained basis; and
processing incomplete or failed. Do not equate an empty result with
non-applicability, and do not equate completed processing with complete
evidence. Use only configured fields, IDs, and relation types; expose
material representation gaps through existing permitted structures.

';

	-- Anchor: the closing paragraph the prefix ends with (same anchor used by 0408/0415).
	DECLARE @Anchor NVARCHAR(MAX) = N'The ORIGINAL_PROMPT (exact stored text), the SELECTED_DOMAIN_PACK';

	-- Apply edits (each is a no-op if its anchor is absent; the final test asserts presence).
	SET @T = REPLACE(@T, @OldDimA, @NewDimA);
	SET @T = REPLACE(@T, @OldDimB, @NewDimB);
	SET @T = REPLACE(@T, @OldCheck, @NewCheck);
	SET @T =
		CASE
			WHEN CHARINDEX(@Anchor, @T) > 0
				THEN REPLACE(@T, @Anchor, @NewSections + @Anchor)
			ELSE @T + @LF + @LF + @NewSections
		END;

	DECLARE @V4Template NVARCHAR(MAX) = CASE WHEN @UsesCrlf = 1 THEN REPLACE(@T, @LF, @CRLF) ELSE @T END;

	-- ── Upsert V4 (idempotent; refresh text on re-run so it tracks any V3 edits) ───────────────────
	MERGE POLOXI.Legal_DecisionPrompt AS target
	USING (VALUES
		(N'DECISION_DISCOVERY_DOMAIN_PACK_V4', @V3Stage, @V3System, @V4Template, CAST(NULL AS NVARCHAR(MAX)))
	) AS source (PromptCode, StageCode, SystemPrompt, UserPromptTemplate, OutputSchemaJson)
	ON target.PromptCode = source.PromptCode
	WHEN MATCHED THEN
		UPDATE SET
			StageCode          = source.StageCode,
			SystemPrompt       = source.SystemPrompt,
			UserPromptTemplate = source.UserPromptTemplate,
			OutputSchemaJson   = source.OutputSchemaJson
	WHEN NOT MATCHED BY TARGET THEN
		INSERT (PromptCode, StageCode, SystemPrompt, UserPromptTemplate, OutputSchemaJson)
		VALUES (source.PromptCode, source.StageCode, source.SystemPrompt, source.UserPromptTemplate, source.OutputSchemaJson);
END

COMMIT TRANSACTION;
