# POLOXI Wide-Search Pipeline — End-to-End Workflow (by execution order)

Source of truth: `src/Ams.Application/IntelligenceWideService.cs` (`SearchDynamicAsync`), UI: `src/Ams.Web/Components/Pages/Intelligence/IntelligenceSearchPoloxiWide.razor`.
Design invariant throughout: **The LLM proposes. Evidence informs. POLOXI decides** — every score, entropy, and gain is computed deterministically in code, never by the LLM.

---

## Stage −1 · Request Normalization & Continuation (V3.4 / V2.8 / V2.8.5)

1. Validate request, require authenticated user.
2. Normalize query; clamp `MaximumResults` to `[1,100]`; assign correlation id.
3. **V3.4 Server-side continuation state**: if `ParentWideExecutionId` + `ClarificationAnswer` present, load the persisted parent execution (query text, round, prior intent entropy, answer kind, clarification target). Client-carried fields are *overridden, never trusted*.
4. **V2.8 Clarification continuation**: append the answer as a hard constraint:
   `Query = "{Query} ({ClarificationTarget}: {ClarificationAnswer})"`, and **V2.8.5**: `ClarificationRound = max(round, 1)`.
5. If POLOXI Engine is off → `SearchLlmOnlyAsync` (pure LLM, no hierarchy/grounding/elimination). Done.
6. Load `WideConfiguration`; start execution row; fire **raw-LLM comparison call** (`GetRawLlmRankingAsync`) in parallel (fail-soft, comparison only).

## Stage 0 · Query Contract (V2.1 / V3.1 / V3.2.3 / V3.14)

- `ExtractQueryContractAsync` (LLM #1): hard constraints, output requirements, ambiguous concepts, `AnswerKind`, `CandidateKind`, `RankingConcept`, `RequestedCount`, safety risk.
- Deterministic refinements:
  - `ApplyStructuralQueryContract` — regex extraction: **RequestedCountPattern** (`top|best|first N`), **RequiredTermPattern**, **ExcludedTermPattern**.
  - `ApplySelectionUpgradeGuard` — SINGLE_ANSWER + selection intent + entity type ⇒ upgrade to `ENTITY_RANKING`, `RequestedCount ??= 1`.
  - `RefineQueryContractForAmbiguity` — e.g. unqualified "bridge" forces `CLARIFICATION_REQUIRED` (safety-sensitive).
- **V3.2.3 AnswerKind carry-forward**: continuations inherit the ORIGINAL run's kind (re-classification of answer-polluted text is untrusted).
- **V3.2/V3.3 Answer-Kind budgets** (`ResolveAnswerKindBudgets`): per-kind `DepthCeiling` / `MaxInformationRounds` from the `POLOXI.AnswerKind` lookup table (compiled constants as fallback). Budgets only **shrink**, never expand; unknown kind ⇒ full budgets (fail-safe toward thoroughness).
- **Batch overlap**: candidate-seed enumeration LLM call (`EnumerateCandidateSeedsAsync`) is *started* here, awaited later.

## Stage 1 · Intent Framing → Level-1 Hierarchy

- `ProposeIntentAsync` (LLM #2): problem-specific Level-1 branches (open, not catalog-limited). Each branch carries `Confidence` (the **Interpretation Prior**), `SemanticTypeCode` (`ALTERNATIVE` = mutually exclusive readings vs `DIMENSION` = jointly valid criteria), `ContinueNarrowing`.
- `MaterializeBranches` + persist.

## Stage 2 · Iterative Narrowing Loop (ground → classify → narrow)

Per depth level, until a termination code fires:

1. **Parallel grounding** (`GroundBranchAsync`, semaphore = `GroundingConcurrency`). Wide search uses an **empty capability catalog** ⇒ every branch is `INTERPRETIVE` (knowledge-only, no enterprise grounding).
2. **V2.1 branch lifecycle** (thresholds, never elimination-by-absence):
   ```
   state = Confidence ≥ SecondaryBranchThreshold → ACTIVE
		 : Confidence ≥ DormantBranchThreshold   → SECONDARY
		 : otherwise                             → DORMANT
   ```
   `PRUNED` is reserved for hard-constraint violations / contradictions — never for lacking evidence. Evidence-void GROUNDED branches below `TargetConfidence` demote to DORMANT (reactivatable).
3. **Aggregate Confidence** (`ComputeAggregateConfidence`):
   `AggregateConfidence = max over survivors of (Confidence + 0.15 if GROUNDED & EvidenceCount>0 else Confidence)`, clamped 0..1.
4. **Termination codes** (in check order): `NO_SURVIVORS` / `HIERARCHY_SETTLED` (V2.3), `CONFIDENCE_REACHED` (depth ≥ 2 & AggregateConfidence ≥ TargetConfidence), `LLM_COMPLETE` (depth ≥ 2 & no branch wants narrowing), `DEPTH_CEILING_REACHED` / `ANSWER_KIND_DEPTH_BUDGET`, `LLM_CALL_CEILING_REACHED`, **V2.3 `EVIDENCE_COVERAGE_PRIORITY`**:
   `supportedShare = count(EvidenceCount>0)/survivors < EvidencePriorityCoverageFloor` at depth ≥ `EvidencePriorityMinimumDepth` ⇒ stop deepening, let information rounds work.
5. `ProposeNextLevelAsync` (LLM per level); **degenerate-progress guard** (`NO_PROGRESS` when the LLM merely rephrases the level).

## Stage 3 · Evidence Ranking & External Grounding

1. `RankEvidence` over surviving branches only (evidence of eliminated branches never surfaces).
2. **Live external grounding** `GatherExternalKnowledgeAsync`: per-branch candidate-seeking web query (cache-first, `CacheHours`; `MaximumQueriesPerExecution`; `MaximumSnippetsPerQuery`; concurrency = `ExternalRetrievalConcurrency`). **V3.6.1**: results deduped by `(Query, Url)` so overlapping branch queries never double-count.
3. Continuation runs inherit the parent execution's snippets, URL-deduped (fresh wins).
4. **V2.1 Three-Score Model** (per branch, deterministic):
   - **Evidence Support** (`ComputeEvidenceSupport`):
	 ```
	 enterpriseSupport = 0 if none else min(EnterpriseSupportCeiling,
						  EnterpriseSupportBase + EnterpriseSupportIncrement·(n−1))
	 externalSupport   = max snippet score · min(1, ExternalSupportBase + ExternalSupportIncrement·m)
	 ```
   - **Bounded Consensus** (`ResolveBoundedConsensusEvidenceSupport`):
	 both sources agree within `EvidenceConsensusThreshold` ⇒ `max(enterprise, external)`; disagree ⇒ `enterprise`; external-only ⇒ `external · ExternalOnlySupportDiscount`.
   - **POLOXI Confidence**:
	 `PoloxiConfidence = clamp(PriorWeight·InterpretationPrior + EvidenceWeight·EvidenceSupport, 0, 1)`
   - **REWEIGHT**: state re-derived from PoloxiConfidence (DORMANT with strong evidence reactivates; PRUNED is terminal).

## Stage 4 · Candidate Universe Seeding (V2.9 seeds + harvest)

- `HarvestCandidateNames(externalKnowledge)` → initial universe.
- Await `candidateSeedTask`; filter seeds: `IsValidCandidateForContract` ∧ ¬`IsQueryTopicEcho` ∧ not already known; take ≤ 20. Seeds are **untrusted** — they must still earn support at admission gates.
- `GatherSeedVerificationKnowledgeAsync` gives seeds a chance to accumulate host support (URL-deduped append).

## Stage 5 · Uncertainty Measurement (entropy machinery)

**Formula — Shannon entropy with normalization** (`EntropyFromValues`):
```
p_i = v_i / Σv       H = −Σ p_i · log2(p_i)       Hmax = log2(N)
NormalizedEntropy = clamp(H / Hmax, 0, 1)          (4-decimal rounding)
```
**Basis selection** (`ComputeUncertainty`, V2.3 / V2.5 / V3.1):
- Ranking query (RankingConcept or RequestedCount ≥ 1, competition not skipped) & ≥ 2 candidates ⇒ **CANDIDATE basis**: entropy over `ComputeCandidateSignals` values (mention-weighted evidence signals, floored at 0.0001).
- Otherwise ≥ 2 competing ALTERNATIVE branches ⇒ **BRANCH basis** (`ComputeEntropy` over `max(PoloxiConfidence, .0001)` of ACTIVE/SECONDARY ALTERNATIVE branches only; DIMENSION branches never enter winner-take-all entropy).
- No candidates known yet ⇒ report **maximal uncertainty (1.0)** on CANDIDATE basis so rounds keep investigating.

**Candidate signal** (`ComputeCandidateSignals`, saturating):
```
raw = Σ RelevanceScore(evidence mentioning c) + Σ Score(snippets mentioning c)
signal(c) = round(raw / (1 + raw), 4)         // saturation keeps 0..1, preserves order
```
V3.6.1 sibling guard: text is stripped of longer sibling names before the mention test ("Rolling Hills" is not credited with "Rolling Hills Estates" snippets).

## Stage 6 · Information Rounds (V2.2 Information-Directed Exploration)

Loop `round = 1..effectiveInformationRounds`, with per-round **frozen candidate basis** (V2.5 — `Hmax = log2(N)` must not shift mid-round):

1. `entropyBefore = ComputeUncertainty(...)`; **stop** if `NormalizedEntropy < InformationValueTriggerEntropy` or LLM budget exhausted or < 2 eligible branches.
2. `DescribeContestedPair` (deterministic leader vs runner-up) → prompt context only.
3. `EstimateInformationValueAsync` (LLM, batched): per-branch categorical judgments (VERY_LOW..VERY_HIGH) validated by `ValidateCategories`, converted by `CategoryValue` (DB-calibrated values).
4. **Raw Information Value** (per target):
   ```
   RawIV = clamp( wU·U + wRI·RI + wCD·CD + wEA·EA + wN·N − wR·Redundancy , 0, 1 )
   ```
   (weights: `CriterionUncertaintyWeight`, `CriterionRankingImpactWeight`, `CriterionDiscriminationWeight`, `CriterionEvidenceAvailabilityWeight`, `CriterionNoveltyWeight`, `CriterionRedundancyPenalty` — migration 0164.)
5. **Adjusted IV** (facts POLOXI already knows):
   ```
   evidenceGap      = clamp(1 − EvidenceSupport, 0, 1)
   branchImportance = clamp(PoloxiConfidence / maxConfidence, 0, 1)
   candidateNeed    = clamp(1 − 5·(top1 − top2 PoloxiConfidence margin), 0, 1)
   AdjustedIV = clamp( wLLM·RawIV + wGap·evidenceGap + wBranch·branchImportance + wNeed·candidateNeed , 0, 1)
   ```
6. **V2.5 Marginal Information Value** (repeat discount):
   ```
   noveltyFactor = 1 / (1 + priorInvestigations)
   AdjustedIV ×= noveltyFactor · clamp(priorRoundEffectiveness, .10, 1)
   ```
   **V3.6.1 calibration guard**: after a measured weak round, even *fresh* targets are discounted by `clamp(priorRoundEffectiveness, .25, 1)` (measured math only, never LLM self-report).
7. **Selection**: `AdjustedIV ≥ MinimumInformationValue`, top `MaximumInformationTargetsPerRound`. Zero selected ⇒ `NO_HIGH_VALUE_INVESTIGATION`, break. All targets (selected or not) persisted for audit.
8. **Falsifiable predictions** (V2.2): baseline `ScoreBefore = ComputeCandidateSignals(...)` + `RankBefore = RankSignals(...)` stamped *before* retrieval.
9. **Targeted retrieval** (branch `SearchText` overridden with the evidence target); **V3.6.1** fresh knowledge filtered by pool URLs before counting/appending.
10. **V3.0 Discovery Admission Gate** (Invariant 3): with adaptive narrowing, newly harvested names join the universe only with sufficient distinct-host attestation within the per-round admission budget (`WideNarrowingPolicy.EvaluateExpansion`); rejections disclosed.
11. **Re-score branches** (same V2.1 formula: `PriorWeight·Prior + EvidenceWeight·Support`).
12. **Actual Information Gain** (same frozen basis):
	```
	rawDelta   = H_before − H_after       (negative preserved for diagnostics)
	ActualGain = max(0, rawDelta)
	PriorRoundEffectiveness = clamp(ActualGain / H_before · 4, 0, 1)
	```
13. **Prediction verification** (V2.2): re-measure signals; `relative = |Δ|/before`; magnitude `NONE / LOW(<.15) / MEDIUM(<.5) / HIGH`; grade `DirectionCorrect` / `MagnitudeCorrect` (LLM predicted; POLOXI measured).
14. **V2.6 stability snapshot**: ordering of frozen basis by signal appended to `roundRankings`.
15. **V3.0 Evidence-Guided Adaptive Narrowing** (deterministic, zero-LLM, fail-soft): `EvaluateBranches` (resolve/reopen), `EvaluateCandidates` (state transitions), `ComputeTrend`; iteration persisted.
16. **Stall detection**: `weakRounds++` when `ActualGain < MinimumActualInformationGain`; break at `InformationNoProgressRounds` ⇒ `INFORMATION_GAIN_STALLED`.

## Stage 7 · Answer Composition

- `ComposeAnswerAsync` (LLM): survivors + ranked evidence + snippets, clamped to a 12,000-char user-prompt budget with progressive shrink. **V3.14**: the query contract is injected as non-negotiable.
- **Relevance validation**: only evidence the answer LLM judged relevant survives (`RelevantEvidenceNumbers`); superficial keyword matches cannot inflate confidence. INTERPRETIVE / zero-relevant ⇒ `aggregateConfidence = min(aggregate, answer.Confidence)`.

## Stage 8 · Candidate × Branch Competition (V2.1 Candidate Engine)

Routing first:
- **V3.1/V3.3**: `SkipsCandidateCompetition` via AnswerKind lookup (`RunsCandidateCompetition` column); CONTENT_ENUMERATION et al. skip.
- **V3.7 mid-run reclassification**: ≥ 2 distinct named candidates + a `RankingConcept` re-enables the competition (interpretive results are ground truth).

Then `CompleteRankingAsync` → competition & scoring:
1. **Candidate validity** (V2.7): reject category/placeholder phrases (`CandidateInvalidWords`), artifacts, **V2.8.1/V2.8.3 Attribute-Hypothesis Rejection** (non-identifying qualifiers).
2. **Canonicalization** (`CanonicalizeCandidates`, most-specific-first):
   - **V2.8.6 identity merge**: identical canonical tokens ⇒ unconditional merge ("Overland Park, Kansas" = "Overland Park").
   - **V2.7.2 recall preservation**: prefix relation with > 1 canonical entry ⇒ ambiguous, stays separate.
   - **V3.6.1 exclusive-mention guard** (`HasExclusiveMention`): genuine prefix merge requires the shorter name has NO mention that survives removal of the longer form ("Rolling Hills" vs "Rolling Hills Estates" stay distinct when independently attested). Host-overlap identity check otherwise.
3. **V3.10 merit signals**:
   - **EEA — Exclusive Evidence Attribution** (`CountExclusiveSourceHosts`): distinct hosts mentioning exactly ONE candidate (V3.6.1 sibling-stripping applied). Shared listicles attest nothing.
   - **FD — Fragment Domination** (`FindDominatedFragments`): strict token-subset of another candidate's full-name tokens with zero exclusive evidence ⇒ name fragment, excluded with disclosure (V3.10.3 qualifier-inclusive tokens).
   - `CountDistinctSourceHosts` via shared `CandidateMatchKeys` (V2.6.1: primary-name matching so qualified names still match).
4. **Dimension scoring** with **contrast amplification**:
   `effective = clamp(mean + (score − mean) · 1.6, 0, 1)` per dimension with ≥ 2 scored candidates (order- and mean-preserving).
5. **Composite (Quality) score**: `Composite = Σ_b rfnWeight_b · score_b` (RFN global branch weights; unscored dimensions contribute 0 — coverage implicitly scales, never multiplied twice). Optional `ComputeGuardrailPenalty` multiplier.
6. **Evidence Confidence** (V2.6 separation of concerns — quality ≠ supportability):
   ```
   diversityFactor    = hosts ≤ 1 ? .70 : min(1, .70 + .15·(hosts−1))
   EvidenceConfidence = clamp(diversityFactor · (.5 + .5·coverage), 0, 1)
   ```
7. **Tiered admission** (V2.9.4 → V3.10 → V3.10.5 Recall Floor): STRONG (corpus support & dimension support met, or ≥ 2 exclusive hosts) / MODERATE / LIMITED; SB demotes one tier (never hard-excludes); zero-support names excluded. Constraint violators kept visible as PRUNED with reason (never silently dropped).
8. Post-processing (`PostProcessRankingCandidates`): subset-alias dedup guarded by `SubsetNameIndependentlyAttested` (V3.6.1); interpretive fallback path (`BuildInterpretiveFallbackCandidates`) uses
   `branchScore = .65·rankScore + .35·branchConfidence`, `composite = .85·weightedQuality + .15·min(1, hosts/requiredSupport)`.

## Stage 9 · Output Contract Validation (V2.9.2 / V3.5.2)

- `Delivered ≥ RequestedCount`? Shortfall = validation failure ⇒ one **recovery pass** with relaxed candidate discovery (gates never lowered, only additional independent support credited); remaining shortfall **disclosed**, never silently accepted.
- **V3.5.2 implicit-plural floor**: ranking queries without an explicit count get a default expected count so the same recovery fires.

## Stage 10 · Final Uncertainty & Confidence

1. **V3.6 Fix A — Competition-Outcome Entropy** (`ComputeCompetitionOutcomeEntropy`): softmax over quality scores with temperature 0.1:
   ```
   w_i = max(exp((q_i − q_max)/0.1), 1e−7)   →  EntropyFromValues(w, CANDIDATE)
   ```
   Adopted only if it *reduces* normalized entropy vs the mention-signal measurement (never claims more certainty than evidence established).
2. **Evidence Coverage**: `coveredBranches / survivors`; **V2.5 Decision Evidence Coverage** measured only over branches in the final competition.
3. **V2.6 Ranking Stability** (`ComputeRankingStability`):
   - `WinnerStability` = fraction of consecutive snapshots with unchanged #1.
   - `TopKStability` = mean Jaccard overlap of top-3 sets between consecutive snapshots.
4. **V2.6 Decision Confidence** (replaces aggregate when a competition produced the answer):
   ```
   separation = clamp((C_winner − C_runnerUp)/max(C_winner, .0001), 0, 1)
   DecisionConfidence = clamp(.35·QualityScore + .25·EvidenceConfidence
							+ .15·separation + .15·WinnerStability + .10·DecisionEvidenceCoverage, 0, 1)
   ```

## Stage 11 · Challenge-the-Winner (Phase 2a, WATCH MODE, default OFF)

Fires when `challengeMargin < ChallengeMarginThreshold` ∨ decision coverage < .50 ∨ `WinnerStability ≤ .05`. One adversarial LLM assessment argues AGAINST the leader; verdict is **audit-only** — never changes winner, ranking, confidence, or answer.

## Stage 12 · Clarification Intelligence (V2.8 → V2.8.6)

1. **Intent Entropy** (V2.8.4): normalized Shannon entropy over top ≤ 4 candidates' composite scores — measured on *every* run.
2. **Clarification Gain** (V2.8.5): `clamp(PriorIntentEntropy − IntentEntropy, −1, 1)` — persisted for calibration.
3. **Multi-round stop rules**: round < `MaximumClarificationRounds` ∧ previous gain ≥ `MinimumClarificationGain` (must converge, never loop).
4. **V2.8.6 Uncertainty Router** (deterministic, zero-LLM): EVIDENCE GAP → retrieve; INTENT GAP → ask; DECISION UNCERTAINTY → rank + disclose. Gate fires only when `alternativeShare ≥ .5` (decision rests on ALTERNATIVE branches) **and** retrieval is stalled (rounds exhausted or `totalActualInformationGain ≤ MinimumActualInformationGain`).
5. **Clarification Value** (V2.8.1/V2.8.5, 5-factor, zero-LLM): per dimension `CV = CandidateSeparation × Answerability` (+ additional factors) — what to ASK is not what to RETRIEVE.

## Stage 13 · Response Assembly & Persistence

- Answer locking (immutable winner narrative with composite % and margin), candidate summaries/contrasts, ranking-change driver, ambiguity groups (`BuildAmbiguityGroups`), provenance (branches, rounds, targets, predictions, narrowing iterations, transitions), raw-LLM comparison, funnel metrics (entropy basis codes: `BRANCH` / `CANDIDATE`), termination reason, output contract result.
- UI (`IntelligenceSearchPoloxiWide.razor`) renders truthfully: elimination narrative is gated on `EliminatedBranches.Count > 0` (V3.6.1 narrative gating).

---

## Formula quick reference (by name)

| # | Formula name | Definition |
|---|---|---|
| 1 | Interpretation Prior (V2.1) | LLM branch confidence — controls retrieval allocation, never truth |
| 2 | Branch state thresholds (V2.1) | ACTIVE ≥ SecondaryBranchThreshold; SECONDARY ≥ DormantBranchThreshold; else DORMANT |
| 3 | Aggregate Confidence | `max(Confidence + .15·[grounded w/ evidence])` clamped 0..1 |
| 4 | Evidence Support (saturating) | enterprise: base+increment→ceiling; external: maxScore·(base+increment·n) |
| 5 | Bounded Consensus | agree ⇒ max; disagree ⇒ enterprise; external-only ⇒ ·discount |
| 6 | POLOXI Confidence (three-score model) | `PriorWeight·Prior + EvidenceWeight·Support` |
| 7 | Candidate Signal (saturation) | `raw/(1+raw)`, mention-weighted, sibling-stripped |
| 8 | Shannon Entropy / Normalization | `H=−Σp·log2 p`, `Ĥ=H/log2 N` |
| 9 | Raw Information Value | weighted category sum − redundancy penalty |
| 10 | Adjusted IV | `wLLM·raw + wGap·gap + wBranch·importance + wNeed·need` |
| 11 | Candidate Need | `clamp(1 − 5·topMargin, 0, 1)` |
| 12 | Marginal IV (V2.5) | `AdjIV × 1/(1+repeats) × clamp(effectiveness,.10,1)` |
| 13 | Weak-round calibration (V3.6.1) | fresh targets ×`clamp(effectiveness,.25,1)` |
| 14 | Actual Information Gain | `max(0, H_before − H_after)` on frozen basis |
| 15 | Prior Round Effectiveness | `clamp(gain/H_before·4, 0, 1)` |
| 16 | Prediction magnitude grading | relative Δ: <.15 LOW, <.5 MEDIUM, else HIGH |
| 17 | Contrast Amplification | `mean + (s−mean)·1.6` per dimension |
| 18 | Composite / Quality Score | `Σ rfnWeight·dimensionScore` (·guardrail penalty) |
| 19 | Evidence Confidence (V2.6) | `(hosts≤1? .70 : min(1,.70+.15·(hosts−1))) · (.5+.5·coverage)` |
| 20 | EEA exclusive hosts (V3.10) | distinct hosts mentioning exactly one candidate |
| 21 | Fragment Domination (V3.10) | token-subset ∧ zero exclusive hosts ⇒ fragment |
| 22 | Competition-Outcome Entropy (V3.6 Fix A) | softmax(q/T), T=0.1, → entropy; adopt only if lower |
| 23 | Winner / TopK Stability (V2.6) | winner-change fraction; mean top-3 Jaccard |
| 24 | Decision Confidence (V2.6) | `.35·Q + .25·EC + .15·sep + .15·stab + .10·covg` |
| 25 | Separation / Challenge Margin | `(C1−C2)/max(C1,.0001)` |
| 26 | Intent Entropy (V2.8.4) | normalized entropy over top-4 composites |
| 27 | Clarification Gain (V2.8.5) | `prior − current` intent entropy, clamped ±1 |
| 28 | Clarification Value (V2.8.1) | `CandidateSeparation × Answerability` per dimension |
| 29 | Fallback branch score | `.65·rankScore + .35·branchConfidence` |
| 30 | Fallback composite | `.85·weightedQuality + .15·min(1, hosts/requiredSupport)` |
