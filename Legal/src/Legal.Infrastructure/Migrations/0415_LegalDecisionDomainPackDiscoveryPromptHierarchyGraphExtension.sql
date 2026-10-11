SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ──────────────────────────────────────────────────────────────────────────────────────────────────
-- DECISION_DISCOVERY_DOMAIN_PACK_V3 (idempotent).
--
-- Adds the JUDZ HIERARCHY + GRAPH + SOURCE-FAITHFULNESS EXTENSION to the Domain Pack discovery prefix.
-- This extension ONLY governs L3 → Ln generation quality (soft MECE sibling coverage/overlap, graph
-- relationships, atomic proposition quality, source faithfulness, advisory scoring discipline). It
-- EXPLICITLY preserves Domain Pack L1/L2 ownership: L1 and L2 definitions, identifiers, and parent
-- relationships continue to come from the supplied Domain Pack and are never generated, renamed,
-- merged, or reparented. Nothing in V1 (0406) or V2 (0408) is removed or altered.
--
-- To guarantee V3 never diverges from V2, this migration DERIVES V3's text from the stored V2 row
-- (copying SystemPrompt verbatim and inserting the extension section into a verbatim copy of V2's
-- UserPromptTemplate immediately before its closing ORIGINAL_PROMPT paragraph). The original
-- DECISION_DISCOVERY / DECISION_DISCOVERY_V2 prompt is NOT touched. POLOXI Core scoring, competition,
-- normalization, output schema, and canonical outcome injection are all unchanged.
-- ──────────────────────────────────────────────────────────────────────────────────────────────────

-- ── Source V2 text (must exist from migration 0408) ─────────────────────────────────────────────────
DECLARE @V2System   NVARCHAR(MAX) =
	(SELECT TOP 1 SystemPrompt        FROM POLOXI.Legal_DecisionPrompt WHERE PromptCode = N'DECISION_DISCOVERY_DOMAIN_PACK_V2');
DECLARE @V2Template NVARCHAR(MAX) =
	(SELECT TOP 1 UserPromptTemplate  FROM POLOXI.Legal_DecisionPrompt WHERE PromptCode = N'DECISION_DISCOVERY_DOMAIN_PACK_V2');
DECLARE @V2Stage    NVARCHAR(100)  =
	(SELECT TOP 1 StageCode           FROM POLOXI.Legal_DecisionPrompt WHERE PromptCode = N'DECISION_DISCOVERY_DOMAIN_PACK_V2');

IF @V2Template IS NOT NULL
BEGIN
	-- ── Extension section (inserted before V2's closing ORIGINAL_PROMPT paragraph) ───────────────────
	DECLARE @ExtensionSection NVARCHAR(MAX) = N'════════════════════════════════════════════════════════════════════════
JUDZ HIERARCHY + GRAPH + SOURCE-FAITHFULNESS EXTENSION — V1
════════════════════════════════════════════════════════════════════════

ROLE AND PRESERVATION

Perform the task defined by the ORIGINAL_PROMPT.

This extension adds controls for hierarchy ownership, proposition quality,
graph relationships, and source faithfulness. Preserve the original:
- decision-first analysis;
- outcome-candidate generation and preservation policy;
- interpretative reasoning;
- advisory scoring fields and numeric scale;
- grounding and capability-catalog requirements;
- depth and execution limits;
- output schema and JSON-only response contract.

Do not replace outcome candidates with analytical factors.
Do not assign authoritative POLOXI states, execute LPI propagation,
select a final winner, or assert the actual matter outcome.

The LLM proposes semantic content, relationships, interpretations, and
permitted advisory scores. POLOXI Core validates, registers, and evaluates
those proposals.

Use only information supplied to this execution. Do not assume access to
previous conversations, earlier runs, documents, or external sources that
are not included in the current input.

════════════════════════════════════════════════════════════════════════
1. DOMAIN PACK OWNERSHIP — L1 AND L2
════════════════════════════════════════════════════════════════════════

When Domain Pack mode is enabled:

L1 and L2 definitions, identifiers, and parent relationships come from the
supplied Domain Pack configuration.

Do not generate, rename, replace, merge, or reparent L1 or L2 nodes.
Do not substitute outcome candidates for configured factors.

Generate matter-specific L3 → L4 → … → Ln proposals beneath the appropriate
supplied L2 nodes, preserving their exact identifiers in attachment references.

Use the configured factor definitions to determine semantic fit.
A matching word alone does not establish that a proposition belongs under
a particular L2.

Do not invent propositions to fill every configured L2.
Distinguish:
- relevant and supported;
- relevant but unresolved;
- applicability unresolved;
- not applicable, with an explained basis.

If a material issue does not fit the supplied hierarchy, expose a
configuration coverage gap using an existing permitted output structure.
Do not force it into an unrelated L2 or invent a new L1/L2.

When Domain Pack mode is disabled, retain the ORIGINAL_PROMPT''s hierarchy
generation behavior. Apply the quality controls below to the generated
hierarchy without imposing Domain Pack ownership.

════════════════════════════════════════════════════════════════════════
2. DECISION-RELEVANT L3 → Ln GENERATION
════════════════════════════════════════════════════════════════════════

Keep the proposed decision, jurisdiction, procedural posture, objective,
and time horizon visible when determining relevance.

For each relevant L2:
- identify its materially distinct conditions;
- identify conditions supporting and undermining relevant candidates;
- expose essential prerequisites, exceptions, and missing information;
- decompose further only when doing so materially improves interpretation,
  evidence matching, dependency analysis, or candidate assessment.

Respect the configured depth envelope.
Do not force every branch to the same depth.
Do not assume every L3 node is atomic.

Distinguish a condition to investigate from an established assertion.

Example:
"Whether the applicable signal was red" is an unresolved question.
It must not become "The applicable signal was red" without supporting material.

If the depth envelope prevents sufficient decomposition, expose that
limitation using an existing permitted structure.

════════════════════════════════════════════════════════════════════════
3. ATOMIC PROPOSITION QUALITY
════════════════════════════════════════════════════════════════════════

An atomic proposition contains one independently assessable assertion.

The ORIGINAL_PROMPT''s ATOMIC-FACTOR RULE (semanticQuestion, whyMaterial, stable
branchId, candidateBranchRelations, and the SHARED-DEPENDENCY EXCLUSION RULE)
remains authoritative and is not superseded. This section reinforces that rule
and binds each proposition to the matter, profile, and Domain Pack; wherever the
two overlap, use the ORIGINAL_PROMPT''s exact field names and the rules below.

MATTER, PROFILE, AND DOMAIN-PACK BINDING (AUTHORITATIVE)

Every atomic proposition you propose must be BOUNDED BY and TRACEABLE TO the
specific matter actually before you. It is never a generic legal checklist item.

Derive each atomic proposition from, and keep it consistent with:
- the MATTER_INPUT facts, parties, injuries, timeline, and documents;
- the supplied matter PROFILE (matter type, jurisdiction, procedural posture,
  objective, time horizon, and any profile-specific configuration values);
- the Domain Pack L1 root and its owning L2 node under which it is attached;
- the Domain Pack factor definition for that L2 (semantic fit, not keyword match);
- the configured OutcomeCandidates it bears on; and
- the other related factors and propositions already proposed for this matter.

An atomic proposition is well-formed for this matter only when ALL hold:
1. It states ONE concrete, independently resolvable question (semanticQuestion)
   that could be answered true, false, or unresolved by specific matter evidence
   or a specific legal authority, without first splitting into further questions.
2. It explains, in whyMaterial, how resolving it changes support for a named
   candidate THROUGH the meaning of its parent L2 and the matter facts — not in
   the abstract.
3. It attaches to the correct supplied L2 node by that node''s exact identifier,
   and does not restate, rename, or substitute for the L1/L2 heading itself.
4. It connects to every candidate it affects via candidateBranchRelations using a
   stable branchId, and is proposed ONCE (shared, never duplicated under multiple
   L2 nodes or candidates).
5. It carries the matter-specific entities, dates, amounts, and qualifiers that
   make it about THIS matter rather than a template proposition.

A broad dimension label, outcome name, candidate name, or generic answer heading
is never an atomic proposition. If a configured L2 has only one genuine underlying
question for this matter, that single matter-specific question — not the L2 heading
— is the factor.

Do not invent a proposition merely to populate a configured L2 that the matter
does not actually raise; mark applicability unresolved or not applicable with an
explained basis instead. Do not import a proposition from general legal knowledge
that the matter, profile, and Domain Pack do not support; expose it as an
unresolved question or a configuration coverage gap using a permitted structure.

Preserve material:
- actors and entities;
- action, condition, or relationship;
- event time and temporal scope;
- location and relevant context;
- negation, modality, uncertainty, and attribution;
- conditions, exceptions, and other qualifiers.

Separate assertions that could have different support or truth status.
Do not split statements so aggressively that necessary context or meaning
is lost.

Distinguish:
- attributed source statements;
- observations;
- factual inferences;
- expert opinions;
- legal propositions;
- strategic interpretations.

Use existing schema fields to preserve these distinctions. If dedicated
fields are absent, express them in permitted explanatory fields.

Do not promote one type into another without exposing the inferential step.

Examples:
"The witness states that the signal was red" does not establish that it was red.
"A red signal is visible" does not establish which lane it controlled.
"An injury followed the incident" does not establish causation.
"A document is authentic" does not establish that its contents are true.

Keep materially different scope, timing, attribution, and modality separate.
"The treatment may have contributed" and "The treatment caused" are not
equivalent propositions.

════════════════════════════════════════════════════════════════════════
4. LOCAL COVERAGE AND OVERLAP — SOFT MECE
════════════════════════════════════════════════════════════════════════

Apply coverage and overlap checks to generated siblings under each parent.

Seek materially distinct propositions that adequately represent the parent''s
decision-relevant conditions.

Check for:
- semantic duplicates;
- compound assertions;
- unnecessary overlap;
- omitted essential conditions;
- omitted exceptions;
- omitted contrary interpretations.

Merge true duplicates while preserving provenance and relationships.
Retain legitimate overlap where statements differ in meaning, timing,
scope, conditions, or legal effect.

Do not force legal issues, events, or evidence into artificial exclusivity.
Do not equate a large number of children with complete coverage.

Missing information must remain an explicit gap.
Never invent a proposition or source to make a decomposition appear complete.

A review of the supplied sources establishes coverage only within the supplied
source boundary. It does not establish exhaustive coverage of the matter.

════════════════════════════════════════════════════════════════════════
5. GRAPH RELATIONSHIPS
════════════════════════════════════════════════════════════════════════

Preserve the hierarchy as the primary decomposition structure.
Use explicit, schema-supported relationships for connections across it.

SHARED PROPOSITIONS

Where the schema supports shared identity, represent the same proposition
once and reference it from multiple relevant factors or candidates.

Retain its configured primary placement when required.
Use additional relationships for other relevant placements.

Do not duplicate an assertion merely because it affects several candidates.
Do not merge statements whose attribution, scope, timing, or meaning differs.

If shared identity cannot be expressed by the schema, expose the
representation limitation through an existing permitted structure.
Do not invent an alternative graph format.

RELATIONSHIP MEANING

Using only permitted relationship types, represent materially useful:
- parent-child decomposition;
- evidence support and contradiction;
- essential prerequisites and dependencies;
- exceptions and qualifications;
- proposition contradictions;
- candidate-specific relevance and effects.

Do not confuse these relationships:
- decomposition is not causation;
- temporal sequence is not causation;
- relevance is not support;
- co-occurrence is not dependency;
- an exception is not necessarily a contradiction.

DEPENDENCIES

Identify a dependency only when one condition is materially required for
another proposition or candidate assessment.

Explain whether the relationship is:
- an essential prerequisite;
- an alternative sufficient route;
- a contributing consideration;
- or unresolved.

Use existing permitted fields to express this meaning.
Do not invent formal dependency rules unsupported by the matter or authority.

CONTRADICTIONS

Treat propositions as contradictory only after comparing entities, timing,
scope, conditions, and meaning.

Different accounts may represent a genuine contradiction, different
perspectives, different events, or insufficient context.
Preserve unresolved conflict without deciding which source is truthful.

CANDIDATE-SPECIFIC EFFECTS

The same proposition may:
- support one candidate;
- undermine another;
- affect several candidates similarly;
- or be relevant without a presently determinable direction.

Explain the effect through the candidate''s conditions and dependencies.
Do not infer an effect solely from semantic similarity.

STRUCTURAL INTEGRITY

Keep parent-child decomposition acyclic.
Do not create circular prerequisite chains.
Represent contradiction links separately from decomposition.

Repeated references or multiple graph paths do not create independent
evidentiary support.

════════════════════════════════════════════════════════════════════════
6. SOURCE-FAITHFULNESS VALIDATION
════════════════════════════════════════════════════════════════════════

Before proposing a source-backed assertion, compare its exact meaning
with the referenced material.

Check:
1. Does the source support this statement, rather than a weaker or different one?
2. Are the actor, event, timing, and scope correct?
3. Were attribution, negation, uncertainty, and exceptions preserved?
4. Did the proposal add an unsupported causal or legal conclusion?
5. Is the passage sufficiently complete to support the interpretation?
6. Is the source version and exact anchor available?

Retain exact anchors where supplied:
- document page, paragraph, section, or passage identifier;
- image identifier and region;
- video or audio interval;
- machine-data record, timestamp, units, and relevant metadata;
- legal-authority citation and pinpoint passage.

Never invent an anchor, quotation, source identifier, or unavailable content.

For media, distinguish visible or audible observations from inferred identity,
intent, timing, signal applicability, or causation.

For machine data, preserve units, timestamp basis, measurement meaning,
and supplied reliability limitations.

For legal authority, distinguish:
- source existence;
- the proposition actually supported by its text;
- holding versus other discussion;
- jurisdiction and procedural fit;
- temporal applicability;
- supplied authority-status verification.

Do not claim independent verification of authenticity, current legal status,
admissibility, or factual truth unless the execution provides that verification.

If support is partial, narrow the proposition or label its limitation.
If support is absent, retain it as an unresolved question or unsupported
proposal where permitted; do not present it as established.

Faithful extraction of a statement is not proof that the statement is true.

════════════════════════════════════════════════════════════════════════
7. ADVISORY SCORING — PRESERVE THE ORIGINAL CONTRACT
════════════════════════════════════════════════════════════════════════

Provide the advisory scores required by the ORIGINAL_PROMPT and configured
output schema, using its exact fields, definitions, and numeric scale.

Do not add unsupported scoring fields.

Assess each signal separately. Do not substitute one generic confidence
number for legal support, factual support, evidence support, authority support,
verification, discrimination, or ranking impact.

Explain material scores using:
- supporting material;
- contrary material;
- interpretation limitations;
- unresolved assumptions;
- essential dependencies;
- candidate-specific relevance.

Where separate support, opposition, and confidence fields exist, use them.
Otherwise preserve these distinctions in existing permitted explanations.

Use null for insufficient information only where allowed.
Otherwise follow the configured missing-value convention and explicitly
state the limitation. Unknown must not silently become false.

Do not score source truth or authenticity as verified merely because the
source exists or was accurately extracted.

Keep shared propositions'' evidentiary assessments consistent.
Assess their direction and relevance separately for each candidate.

Higher-level advisory assessments must respect the parent''s meaning and
essential dependencies. Do not mechanically average children.

Do not allow strong incidental support to conceal an unresolved essential
condition.

Multiple copies, passages, summaries, channels, or graph paths derived from
the same underlying information are not independent corroboration.
Additional channels do not automatically justify additional points.

Do not infer a proposition''s evidentiary strength from its display position
or neighboring node scores.

Do not perform authoritative LPI propagation.
Do not label advisory outcome scores as calibrated win probabilities.
POLOXI Core owns authoritative evaluation and aggregation.

════════════════════════════════════════════════════════════════════════
8. CANDIDATE INTERPRETATIONS AND CHANGE HANDLING
════════════════════════════════════════════════════════════════════════

For each candidate, preserve interpretative detail explaining:
- how the candidate answers the decision;
- linked factors and propositions;
- supporting and undermining conditions;
- essential dependencies;
- contrary interpretations;
- source and authority limitations;
- unresolved information;
- conditions that could change the assessment.

Preserve asserted historical outcomes as verify-first information.
A status field alone does not establish settlement, payment, liability,
causation, damages, or agreed terms.

When a prior assessment is supplied:
- distinguish new information from existing information;
- identify affected propositions, relationships, and candidates;
- distinguish changed evidence from changed interpretation or configuration;
- preserve contrary information and unresolved conditions;
- do not assume new information strengthens the current preferred candidate.

If no prior assessment is supplied, do not invent assessment history.

Use existing structured fields. Do not replace required objects with narrative.

════════════════════════════════════════════════════════════════════════
9. FINAL QUALITY CHECK
════════════════════════════════════════════════════════════════════════

Before returning, check that:
- the original decision and candidate policy are preserved;
- Domain Pack L1/L2 identifiers and relationships remain unchanged when enabled;
- generated nodes attach to valid parents;
- atomic assertions preserve material scope and qualifiers;
- unresolved questions are not presented as established facts;
- sibling coverage and overlap have been reviewed;
- shared identities and references are consistent;
- dependencies, contradictions, and candidate effects are explained;
- source-backed assertions faithfully reflect their referenced material;
- unavailable verification is not claimed;
- advisory scoring follows the original contract;
- duplicate information has not been treated as independent support;
- the output conforms exactly to the configured schema.

Repair detectable errors within the configured execution limits.
If an issue cannot be repaired, expose it using an existing permitted
unresolved-information structure rather than fabricating completion.

Return only the JSON required by the ORIGINAL_PROMPT.

';

	-- Anchor: the closing paragraph that V2 ends with (inherited verbatim from V1). Insert the extension
	-- immediately before it so the extension sits inside the pack prefix, ahead of the ORIGINAL_PROMPT
	-- handoff, and governs only L3 → Ln generation quality (L1/L2 ownership is preserved above).
	DECLARE @Anchor NVARCHAR(MAX) = N'The ORIGINAL_PROMPT (exact stored text), the SELECTED_DOMAIN_PACK';

	DECLARE @V3Template NVARCHAR(MAX) =
		CASE
			WHEN CHARINDEX(@Anchor, @V2Template) > 0
				THEN REPLACE(@V2Template, @Anchor, @ExtensionSection + @Anchor)
			ELSE @V2Template + NCHAR(10) + NCHAR(10) + @ExtensionSection
		END;

	-- ── Upsert V3 (idempotent; refresh text on re-run so it tracks any V2 edits) ─────────────────────
	MERGE POLOXI.Legal_DecisionPrompt AS target
	USING (VALUES
		(N'DECISION_DISCOVERY_DOMAIN_PACK_V3', @V2Stage, @V2System, @V3Template, CAST(NULL AS NVARCHAR(MAX)))
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
