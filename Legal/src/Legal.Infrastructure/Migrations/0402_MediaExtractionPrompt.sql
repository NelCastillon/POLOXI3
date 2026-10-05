SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- 0402: POLOXI Legal — MEDIA_EXTRACTION_V1 media/machine extraction prompt (idempotent).
--
-- A SEPARATE prompt role for the Media & Machine Evidence → proposition pipeline (Phase 3). It extracts
-- ATOMIC proposition proposals from supplied media-derived content (image analysis text, audio transcript
-- segments, parsed structured records) and PROPOSES qualitative hierarchy placements, each attached to an
-- EXACT source anchor. It is a proposal-only role: it never scores candidates, never picks a winner,
-- never assigns numeric support, and never claims verification or authenticity.
--
-- DECISION_DISCOVERY_V2 and DECISION_EXTRACTION_V1 are LEFT COMPLETELY UNCHANGED by this migration. This
-- is a NEW PromptCode insert guarded by WHEN NOT MATCHED, so reruns are safe.
--
-- Extraction invariants baked into the prompt:
--   * Separate directly observable content, attributed assertions, and proposed interpretations.
--   * Preserve uncertainty, negation, units, attribution, and timing verbatim.
--   * Do not infer identity, speed, causation, fault, or authenticity unless the material supports it.
--   * Attach each proposal to an exact source anchor using only valid IDs/timestamps/regions/records.
--   * Distinguish media-relative time from asserted real-world event time.
--   * If no node fits, flag a hierarchy gap rather than force-fitting a lexical match.
-- ─────────────────────────────────────────────────────────────────────────────────────────────────

MERGE POLOXI.Legal_DecisionPrompt AS target
USING (VALUES
	(N'MEDIA_EXTRACTION_V1', N'EXTRACTION',
		N'You are the POLOXI Legal Media & Machine Evidence EXTRACTION layer. Given media-derived content (image-analysis observations, a time-aligned audio transcript, sampled video frames, or parsed structured records such as GPS/telematics/device logs) together with the current decision hierarchy (its question, candidate outcomes, and nodes) and the exact source asset identifiers, you extract ATOMIC proposition proposals and PROPOSE where each belongs in the hierarchy. You are a proposal-only role: you never score candidates, never pick a winner, never assign numeric support, never decide the outcome, and never claim verification or authenticity — POLOXI Core alone scores. Return only strict JSON. Rules: (1) One atomic proposition per object; split compound statements. (2) Classify each as evidenceKind = OBSERVATION (directly observable content), ATTRIBUTED_ASSERTION (a spoken or written assertion attributed to someone), or PROPOSED_INTERPRETATION (an inference you propose). (3) Preserve uncertainty, negation, units, attribution, and timing verbatim; never promote a reported assertion to an established fact. (4) Do NOT infer identity, speed, causation, fault, or authenticity unless the supplied material specifically supports that inference. (5) Attach each proposal to an exact source anchor: for images a normalized region (x,y,width,height in [0,1]); for audio/video a start/end in milliseconds (media-relative time, NOT real-world time) with optional speaker label and transcript segment; for structured records the record ids/row range, field names, unit, and coordinate reference. Use only valid asset ids, timestamps, regions, and record references supplied to you — never fabricate an anchor. (6) Distinguish media-relative time from asserted real-world event time; if a real-world time is asserted, carry it separately in effectiveAt. (7) For each proposition propose zero or more placements; each placement names a target node and a single qualitative relationship: SUPPORTS, CONTRADICTS, QUALIFIES, or CONTEXT_ONLY (CONTEXT_ONLY carries no support). (8) If no existing node fits a materially relevant proposition, set needsHierarchyReview=true and describe the gap instead of forcing a lexical match. If the supplied material does not support a proposition, do not invent one.',
		N'Decision question:\n{{QUERY}}\n\nHierarchy context (candidates and nodes JSON):\n{{CONTEXT}}\n\nMedia-derived content and source anchors (mediaAssetVersionId, assetType, derivative payloads, valid anchor references JSON):\n{{ARTIFACT}}\n\nExtract atomic proposition proposals, classify each as observation/attributed/interpretation, attach an exact source anchor, and propose qualitative hierarchy placements.',
		N'{"type":"object","additionalProperties":false,"required":["propositions"],"properties":{"propositions":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["propositionText","evidenceKind","anchor"],"properties":{"propositionText":{"type":"string"},"evidenceKind":{"type":"string","enum":["OBSERVATION","ATTRIBUTED_ASSERTION","PROPOSED_INTERPRETATION"]},"assertionType":{"type":"string","enum":["Asserts","Reports","Documents","StatesLaw","Infers"]},"attributedTo":{"type":"string"},"effectiveAt":{"type":"string"},"uncertaintyNote":{"type":"string"},"needsHierarchyReview":{"type":"boolean"},"reviewReason":{"type":"string"},"anchor":{"type":"object","additionalProperties":false,"required":["anchorType"],"properties":{"anchorType":{"type":"string","enum":["IMAGE_REGION","VIDEO_INTERVAL","AUDIO_INTERVAL","STRUCTURED_RECORD"]},"normX":{"type":"number"},"normY":{"type":"number"},"normWidth":{"type":"number"},"normHeight":{"type":"number"},"startMs":{"type":"number"},"endMs":{"type":"number"},"frameNumber":{"type":"number"},"speakerLabel":{"type":"string"},"transcriptSegmentRef":{"type":"string"},"recordReference":{"type":"string"}}},"placements":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["targetNodeCode","relationship"],"properties":{"targetNodeCode":{"type":"string"},"relationship":{"type":"string","enum":["SUPPORTS","CONTRADICTS","QUALIFIES","CONTEXT_ONLY"]},"leftNeighborCode":{"type":"string"},"rightNeighborCode":{"type":"string"},"placementFraction":{"type":"number"},"rationale":{"type":"string"}}}}}}}}}')
) AS source (PromptCode, StageCode, SystemPrompt, UserPromptTemplate, OutputSchemaJson)
ON target.PromptCode = source.PromptCode
WHEN NOT MATCHED BY TARGET THEN
	INSERT (PromptCode, StageCode, SystemPrompt, UserPromptTemplate, OutputSchemaJson)
	VALUES (source.PromptCode, source.StageCode, source.SystemPrompt, source.UserPromptTemplate, source.OutputSchemaJson);

COMMIT TRANSACTION;
