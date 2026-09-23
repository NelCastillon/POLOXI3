# POLOXI Legal Decision Intelligence — End-to-End Pipeline

> The **DECISION** is the primary object, not the conversation. Instead of returning the single most
> likely answer, POLOXI Core keeps competing outcomes in play, scores each on multiple epistemic
> dimensions, ranks candidates, and surfaces what could still flip the winner.

This document traces the complete runtime flow of a decision, from the Blazor cockpit through the API,
application service (V1 deterministic core + optional V2 dependency graph + advisory EA-7 governance),
persistence, and back to the UI. File and method references are to the actual implementation.

---

## 1. Component / Layer Map

| Layer | Project | Key Type(s) |
|-------|---------|-------------|
| UI (cockpit) | `Legal.Web` | `Components/Pages/LegalDecision.razor`, `Components/Pages/LegalMatters.razor` |
| Web API client | `Legal.Web` | `Services/ApiClient.cs` |
| REST API | `Legal.Api` | `Controllers/LegalDecisionController.cs` |
| Application service | `Legal.Application` | `LegalDecisionService.cs` (implements `ILegalDecisionService`) |
| Contracts / DTOs | `Legal.Application` | `Features/Intelligence/Decision/DecisionContracts.cs`, `DecisionConfigContracts.cs` |
| Deterministic core math/graph | `Legal.Application` | `DecisionCoreMath`, `DecisionGraph` |
| Epistemic governance (EA-6/EA-7) | `Legal.Application` | `Features/Intelligence/Epistemic/*`, epistemic bridge |
| AI provider | `Legal.Infrastructure` | `Services/AzureOpenAiProvider.cs` |
| Persistence (Dapper) | `Legal.Infrastructure` | `Persistence/Repositories/LegalDecisionRepository.cs` |
| Database | SQL Server | `POLOXI.Legal_Decision*` tables, `POLOXI.Legal_DecisionPrompt` |

---

## 2. High-Level Sequence

```
User (cockpit)
   │  types matter question, toggles POLOXI Engine + Dependency Graph (V2), presses Enter
   ▼
LegalDecision.razor ──► ApiClient.DecideAsync(DecisionSearchRequest)
   ▼
POST /api/legal/decision/decide  (LegalDecisionController.Decide)
   ▼
ILegalDecisionService.DecideAsync(request)     ◄── the whole pipeline
   │
   ├─ 0. Intake & configuration
   ├─ (engine off) ► ComposeDirectAnswerAsync ► return
   ├─ 1. Candidate Discovery      (LLM proposal)
   ├─ 2. Candidate × Branch competition + deterministic Core scoring
   ├─ 3. Uncertainty (entropy) + Margin
   ├─ 4. Decision-directed evidence retrieval
   ├─ 5. Frontier + Flip points
   ├─ 6. Convergence / terminal state + clarification gate
   ├─ 7. Answer assembly (LLM composer, structured artifact only)
   ├─ 8. Next best action + readiness checklist
   ├─ 9. Persist session (V1)
   ├─ 10. [if V2 enabled] RunDependencyGraphAsync
   │        ├─ graph proposal (DECISION_GRAPH)
   │        ├─ independent verify (DECISION_VERIFY)
   │        ├─ deterministic support recompute + invalidation propagation
   │        ├─ strongest-losing-side gate
   │        ├─ dependency-constrained readiness verdict
   │        └─ persist graph  ► then EA-7 advisory governance overlay
   └─ 11. BuildResponse ► DecisionSearchResponse
   ▼
back through API ► ApiClient ► LegalDecision.razor renders every panel
```

---

## 3. Stage-by-Stage Detail

All stages below live in `LegalDecisionService.DecideAsync(DecisionSearchRequest request)`.

### Stage 0 — Intake & Configuration
- Starts a `Stopwatch`, generates a new `sessionId` (`Guid.NewGuid()`).
- Loads deterministic settings:
  - `GetCoreSettingsAsync` → V1 thresholds (`MaxCandidates`, `MaxDepth`, margin/entropy thresholds, deepening flip).
  - `GetV2SettingsAsync` → V2 settings (`UseDependencyGraphDefault`, `PropagationMaxDepth`, readiness thresholds).
- Resolves the effective toggle: `useGraph = request.UseDependencyGraph ?? v2Settings.UseDependencyGraphDefault`.
- Resolves the model route (`ResolveRouteAsync(request.ModelCode)`).
- Normalizes the context code (defaults to `General`).
- Folds any **clarification answer** and **counterfactual assumption** into `effectiveQuery` (this is what makes
  the §7 loop and §1 counterfactual testing re-compete candidates with new information).
- Emits `SESSION_STARTED / INTAKE` timeline event.
- **Engine-off short circuit:** if `!request.UsePoloxiEngine`, returns `ComposeDirectAnswerAsync(...)` — a plain
  single-shot LLM answer with no candidate competition. The rest of the pipeline is skipped.

### Stage 1 — Candidate Discovery (LLM proposal only)
- Loads the `DECISION_DISCOVERY` prompt from `POLOXI.Legal_DecisionPrompt` (throws if unconfigured).
- Fills `{{QUERY}}` / `{{CONTEXT}}` and calls `aiProvider.GenerateAsync(...)` with the prompt's output JSON schema.
- `ParseProposal(...)` turns the structured output into candidate outcomes (capped by `settings.MaxCandidates`).
- Throws if zero candidates were proposed.
- Emits `CANDIDATES_PROPOSED / DISCOVERY`.
- **Principle:** the LLM only *proposes*. It never scores, ranks, or decides.

### Stage 2 — Candidate × Branch Competition + Deterministic Core Scoring
- `ScoreCandidates(proposal, settings, ...)` computes, per candidate, the epistemic dimension scores shown
  in the cockpit table:
  - **L** Legal support, **F** Fact support, **E** Evidence support, **A** Authority support,
	**V** Verification, **U** Uncertainty → **Composite Score** + **Decision-support ceiling**, `RankOrder`,
	`IsWinner`, `IsEliminated`.
- Emits per-candidate branches (interpretive sub-questions) with `InformationValue`, `DecisionRelevance`,
  `FlipPotential`, `AdvScore`.
- Optional bounded adaptive deepening: if branches materialize below level 1, emits `BRANCH_DEEPENED / COMPETITION`.
- Emits `CANDIDATES_SCORED / COMPETITION`.
- **All scoring/ranking is deterministic Core math** — reproducible, not model-authored.

### Stage 3 — Uncertainty (Entropy) + Margin
- `orderedScores` (desc) → `DecisionCoreMath.Distribution` → `NormalizedEntropy` (0..1) and `Margin` (top − second).
- These are **uncertainty signals, not truth**: high entropy + tiny margin means the decision is *not separated*,
  regardless of how confident the leading text sounds.

### Stage 4 — Decision-Directed Evidence Retrieval (§13/§14)
- `RetrieveEvidenceAsync(contextCode, branches, ...)` retrieves evidence targeted at open branches.
- Each evidence row carries a `VerificationStatus` and `VerificationValue`.
- Emits `EVIDENCE_RETRIEVED / RETRIEVAL`.

### Stage 5 — Frontier + Flip Points (§11/§30)
- `BuildFlipPoints(branches, candidates, ...)` computes, for each open issue, whether resolving it could change
  the winner (`WinnerChanges`), the `ChangeCost`, and `RankDelta` (the "What could change this?" panel).
- Frontier = branches with `IsOnFrontier == true` (the live investigation surface).

### Stage 6 — Convergence / Terminal State + Clarification Gate
- `winner` = lowest `RankOrder`; `frontierOpen` and `maxAvailableAdv` computed.
- `ResolveTerminalState(settings, margin, entropy, frontierOpen, maxAvailableAdv)` → `(statusCode, terminalState, reason)`.
- **Clarification gate (§7):** if not already clarified and `entropy >= 0.85 && margin < 0.05`, the engine refuses to
  guess and asks the user **once**. It picks the highest-flip frontier branch as the pivot and produces
  `clarificationQuestion` / `clarificationTarget`; status becomes `UserClarificationRequired`.
- `terminalPhase` maps status → timeline label (`CONVERGENCE`, `PROVISIONAL_RESEARCH_REMAINS`,
  `CLARIFICATION_REQUIRED`, `RESEARCH_EXHAUSTED`). This is presentation only.
- Emits `TERMINAL_STATE / <phase>`.

### Stage 7 — Answer Assembly (§37)
- Only when status ∈ {`DecisionReady`, `ResearchExhausted`, `ProvisionalDecision`}.
- `ComposeAnswerAsync(...)` calls the `DECISION_ANSWER` composer with a **structured artifact only**
  (winner, margin, entropy, a derived confidence descriptor, candidates, frontier, flip points).
- The confidence descriptor is derived deterministically so the prose can never sound more confident than the
  numbers permit.
- Emits `ANSWER_COMPOSED / ANSWER`. (When the status is `UserClarificationRequired`, no final answer is composed —
  the cockpit shows the clarification card instead.)

### Stage 8 — Next Best Action + Readiness
- `BuildNextBestAction(branches, statusCode)` → the single highest-information-value open investigation (impact
  class scales with flip potential).
- `BuildReadiness(candidates, branches, evidence, margin, entropy, statusCode)` → the "Is this decision safe to
  act on?" checklist (leading outcome, margin separation, uncertainty containment, strongest opposition,
  verified evidence count, open high-impact dependency).

### Stage 9 — Persist Session (V1)
- Builds `DecisionSessionPersistence` (session row + candidates + branches + evidence + flip points + timeline
  events + next-best-action + counterfactual + `MatterId`).
- `repository.PersistSessionAsync(...)` writes it. The session row is the FK parent for the V2 graph.

### Stage 10 — V2 Dependency Graph (only when `useGraph == true`)
`RunDependencyGraphAsync(request, route, sessionId, contextCode, effectiveQuery, candidates, branches, v2Settings, winner, ...)`:

1. **Graph proposal** — loads `DECISION_GRAPH` prompt, injects a structured **posture context** line
   (`BuildPostureContext`: procedural posture + motion target from the matter) plus a candidate/branch artifact,
   and calls the LLM (`DECISION_GRAPH` feature) with the graph output schema.
   `ParseGraphProposal(...)` maps proposed typed **nodes** (`fact | proposition | element | strategy | burden |
   procedure`) and **edges** (`SUPPORTS | REQUIRES | SATISFIES | ESTABLISHES | DEPENDS_ON | CONTRADICTS`) into a
   working `DecisionGraph.Model`. Candidate nodes are pre-seeded so `strategy → candidate` edges have valid targets.
   If **no nodes** survive mapping, it throws `"The V2 graph proposal returned no nodes."`.
2. **Independent verification** — loads `DECISION_VERIFY`, sends an edge artifact, and a *distinct* role assesses
   each edge. `ApplyVerification(...)` sets each edge to `Verified` or `Invalidated`.
3. **Deterministic Core** — `DecisionGraph.RecomputeSupport(...)` per node, then
   `DecisionGraph.PropagateInvalidation(model, v2Settings.PropagationMaxDepth)` propagates any invalidation downstream.
4. **Strongest-losing-side gate** — `BuildLosingSideTest(candidates, winner)`: the strongest non-winner becomes the
   challenger; the winner "survives" only if its composite strictly exceeds the challenger's. (In a 0.70 vs 0.70 tie,
   the winner does **not** survive — exactly what the cockpit reports.)
5. **Dependency-constrained readiness** — `DecisionGraph.EvaluateReadiness(...)` (hard gate; margin/entropy are not
   inputs) using authority-verified fraction required, max high-impact frontier, open high-impact count, and the
   losing-side margin. Produces the "NOT READY — blocked by dependencies" verdict with blockers.
6. **Map back** to persistence: node snapshots, edge snapshots, losing-side-test persistence, and a
   `DecisionGraphPersistence`. `repository.PersistGraphAsync(v2.Persistence)` writes it (after the session row).

**EA-6/EA-7 advisory governance overlay** (nested, strictly non-blocking):
- Builds an `EpistemicDecisionContext` from the V2 nodes/edges and calls `epistemicBridge.ProjectAndGovernAsync(...)`.
- Projects the graph into authoritative EA claims (verify + authority gate), runs readiness + output audit, records a
  **non-destructive** governance verdict (`PersistGovernanceVerdictAsync`). It never changes the V1/V2 verdict; all
  claims stay visible. Any failure is caught and logged (`EA-7 ... failed; decision unaffected`).

**Failure handling (the diagnostic surfacing fix):** the entire V2 block is wrapped in a try/catch. V2 is advisory,
so any failure logs a warning, sets `v2 = null`, and **captures `ex.Message` into `graphDiagnostic`**. This reason is
carried on the response so the cockpit shows *why* the graph was empty (e.g. "proposal returned no nodes", missing
prompt, parse failure) instead of a generic empty-graph notice.

### Stage 11 — Build Response
- `BuildResponse(persistence, nextAction, readiness, useGraph, v2, governanceVerdict, graphDiagnostic)` assembles the
  `DecisionSearchResponse` with candidates, branches, evidence, flip points, readiness, `UsedDependencyGraph`,
  `GraphDiagnostic`, graph nodes/edges, losing-side test, readiness verdict, and governance verdict.

---

## 4. Response → UI Rendering (`LegalDecision.razor`)

| Cockpit panel | Response source |
|---------------|-----------------|
| Clarification card | `ClarificationQuestion` / `ClarificationTarget` |
| Current leading outcome + composite support | winner candidate, `DecisionMargin`, `CandidateEntropy` |
| Next best action | `NextBestAction` |
| Decision readiness checklist | `Readiness` |
| Dependency-constrained readiness (V2) | `ReadinessVerdict` |
| Adversarial gate (strongest-losing-side) | `LosingSideTest` |
| Typed legal dependency graph (V2) | `GraphNodes` / `GraphEdges` |
| **"Enabled · no graph generated" + Reason** | `UsedDependencyGraph == true && GraphNodes empty`, with `GraphDiagnostic` |
| Candidates table (L/F/E/A/V/U/Score/Ceiling) | `Candidates` |
| "What could change this?" | `FlipPoints` |
| Branches table (IV/DR/FP/ADV/Frontier) | `Branches` |
| Decision timeline | timeline events emitted via `Record(...)` |
| Closed-loop result (V2.1) | `LastRecompetition` / loop result |
| Governance verdict (EA-7) | `GovernanceVerdict` |

The **Mark verified / Invalidate** actions on each graph edge feed the closed-loop (V2.1) path, which re-runs
verification/readiness and can re-compete candidates — advancing "Material authority verified" in the readiness gate.

---

## 5. Clarification & Counterfactual Loop (§7 / §1)

1. First run ends `UserClarificationRequired` (ambiguous tie).
2. User answers → new `DecideAsync` with `ClarificationAnswer` + `ClarificationTarget`.
3. Stage 0 folds the answer into `effectiveQuery`; candidates re-compete with the disambiguating detail.
4. The counterfactual "Test" buttons run the same path with `CounterfactualAssumption` set (treated as established
   for that analysis) to preview rank changes without committing.

---

## 6. Design Invariants (why the pipeline behaves this way)

- **LLM proposes; Core decides.** Discovery, graph proposal, verification, and answer composition are LLM steps.
  Scoring, ranking, entropy/margin, invalidation propagation, losing-side gate, and readiness are deterministic Core.
- **Competing outcomes stay in play** until margin/entropy and dependency readiness actually justify convergence.
- **A completed run ≠ a converged decision.** Status can be provisional with research still open.
- **V2 and EA-7 are advisory and non-blocking.** They never block the V1 decision; failures degrade gracefully and
  are now explained via `GraphDiagnostic`.
- **Every material transition is on the timeline** (`SESSION_STARTED`, `CANDIDATES_PROPOSED`, `CANDIDATES_SCORED`,
  `EVIDENCE_RETRIEVED`, optional `BRANCH_DEEPENED`, `TERMINAL_STATE`, `ANSWER_COMPOSED`).

---

## 7. Worked Example — Acme v. Contoso (Summary Judgment)

- **Discovery** proposed 2 candidates: *Grant SJ* and *Deny SJ*.
- **Scoring** produced a 0.70 vs 0.70 tie → entropy 1.0, margin 0.
- **Clarification gate** fired (entropy ≥ 0.85, margin < 0.05) → asked to clarify *"Sufficiency of Contoso's evidence
  negating claims"*; status `UserClarificationRequired`.
- **V2 graph** generated **11 nodes · 7 edges** (all UNVERIFIED, essential, dispositive).
- **Adversarial gate:** winner did NOT survive (0.700 vs 0.700).
- **Dependency-constrained readiness:** NOT READY — 3/9 essential requirements satisfied, material authority not
  established, 0 verified sources.
- Run duration ≈ 6.5 s, 1 LLM call at this depth, depth 1.

This is the intended behavior: the engine holds both outcomes open and refuses to declare a safe decision until
separation, verified authority, and dependency readiness are met.
