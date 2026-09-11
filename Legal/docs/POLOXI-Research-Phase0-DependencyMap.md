# POLOXI Research — Phase 0 Baseline & Dependency Map

> Investigation-only artifact. **No behavior was changed to produce this document.**
> It records where each POLOXI Wide responsibility currently lives so the Research
> capability (Claude Fable 5/5.1 implementation contract) can be built incrementally
> without rewriting POLOXI Core into a generic RAG/deep-research agent.

## 0. Scope & conventions discovered

- Solution: `Legal/Legal.sln`, four projects: `Legal.Web` (Blazor), `Legal.Application`,
  `Legal.Api`, `Legal.Infrastructure`. Targets **.NET 10**.
- **DB table convention:** `POLOXI.Legal_*` for pipeline state, `AI.Legal_*` for model/provider/
  feature-policy routing, `Core.*` for tenant/configuration. All tables carry the standard audit
  columns: `TenantId`, `CreatedDateUtc`, `CreatedByUserId`, `ModifiedDateUtc`, `ModifiedByUserId`,
  `IsDeleted` (soft delete).
- **Config convention:** DB-backed `Core.ConfigurationSetting` rows keyed `Intelligence.SearchWide.*`
  with `DataTypeCode` (`Boolean`/`Integer`/`String`) and platform scope, seeded via idempotent
  `MERGE` migrations (see `0203`–`0205`).
- **Migration convention:** sequential numeric prefix under
  `Legal/src/Legal.Infrastructure/Migrations/NNNN_*.sql`, embedded-resource glob. **Next free
  number: `0206`.**
- **NAMING MAPPING for spec tables:** the spec's bare `ResearchAction`, `RESEARCH.*` keys must be
  mapped to repo conventions:
  - Tables → `POLOXI.Legal_Research*` (e.g. `POLOXI.Legal_ResearchEvidence`).
  - Config keys → `Intelligence.Research.*`.
  - All new tables MUST include the standard audit columns above.

## 1. POLOXI Core responsibility map

All core logic currently lives in one ~6,200-line service:
`Legal/src/Legal.Application/IntelligenceWideService.cs`.

| Responsibility | Location | Notes |
| --- | --- | --- |
| Pipeline orchestration | `IntelligenceWideService.SearchDynamicAsync` / `SearchWithPoloxiWideAsync` | Entry point; single stateful method with inline fail-soft try/catch and `llmCalls` accounting. |
| Query Contract extraction | `IntelligenceWideService.cs` ~3401-3435, `ExtractQueryContractAsync` | |
| Hierarchy proposal (L1 intent) | `ProposeIntentAsync` ~2049-2061 | Iterative, not one-shot. |
| Hierarchy proposal (next level) | `ProposeNextLevelAsync` ~2063-2078 | Level-by-level expansion. |
| Hierarchy governance/validation | `IntelligenceWideService.cs` ~2108-2356 | POLOXI owns validation/registration/state, LLM only proposes. |
| Adaptive narrowing | `Features/Intelligence/WideNarrowingPolicy.cs` ~16-102 | Extracted policy — good extraction precedent. |
| Candidate discovery / seed enumeration | `EnumerateCandidateSeedsAsync` ~694-920 | |
| Information Value | `EstimateInformationValueAsync` ~4200-4301 | Weighted model; persisted to `Legal_WideInformationRound/Target/Prediction`. |
| Candidate × Branch competition | `IntelligenceWideService.cs` ~4484-4560 | Authoritative matrix; persisted to `Legal_WideCandidate(BranchScore)`. |
| Uncertainty | ~3666-3790 | Feeds convergence & narrowing. |
| Resolution deepening | ~5201-5503 | Config-gated, RESOLUTION-only. |
| Convergence | ~3275-3392 | |
| Answer assembly / synthesis | ~5315-5900 | Synthesis stages (retain requested model under tiered routing). |
| Branch lifecycle / state | `Features/Intelligence/IntelligenceWideContracts.cs` — `WideBranchStates` | **Already present** and richer than spec: `ACTIVE, SECONDARY, DORMANT, PRUNED, RESOLVED`. |

### Existing state model (spec sections 3, 8, 15 already partly satisfied)

- `WideBranchStates`: `ACTIVE / SECONDARY / DORMANT / PRUNED / RESOLVED` — with documented
  reversibility ("reversible uncertainty, irreversible invalidation"). This **already maps** to the
  spec's `BranchState` enum; do NOT introduce a parallel enum.
- `WideBranchDto` carries a **three-score model**: `InterpretationPrior`, `EvidenceSupport`,
  `PoloxiConfidence` — closely matching the spec's three confidence dimensions (Semantic/Evidence/
  Decision). Phase 9 should align to these existing fields, not replace them.
- `WideCandidateDto` already has `EvidenceCoverage`, `QualityScore`, `EvidenceConfidence`,
  `AdmissionModeCode`, `SupportTierCode`, `InterpretiveSupportCount`, `EvidenceHostSupportCount`.
  The distinction "quality never reduced by weak evidence" already exists.

## 2. Execution Infrastructure map

| Concern | Location |
| --- | --- |
| Model provider | `Legal.Infrastructure/Services/AzureOpenAiProvider.cs` |
| Provider router | `Legal.Infrastructure/Services/AiProviderRouter.cs`, iface `Legal.Application/Abstractions/Intelligence/IAiProviderRouter.cs` |
| Feature-policy route resolution | `Legal.Infrastructure/Persistence/Repositories/AiProviderRouteRepository.cs`; policies in `AI.Legal_FeaturePolicy` |
| Wide config + telemetry persistence | `Legal.Infrastructure/Persistence/Repositories/IntelligenceWideRepository.cs` (~710 lines) |
| Prompt catalog | `Legal.Infrastructure/Services/PromptCatalog.cs`; fallbacks in `Legal.Application/IntelligencePromptDefaults.cs` |
| Async start+poll transport | `Legal.Api/Services/WideSearchOperationStore.cs` — in-memory, single-instance, cancellation-capable |
| **Tiered model routing (v0, shipped)** | `IntelligenceWideService` `MechanicalModel`/`SynthesisModel`; config `Intelligence.SearchWide.EnableTieredModelRouting` + `FastModelCode`; migration `0205` |

### Streaming/progress GAP

There is **no SignalR hub** for Wide. Progress is exposed only via `WideSearchOperationStore`
(start + poll status: `RUNNING/COMPLETED/FAILED/CANCELLED`). Spec Phase 14 (progress events) is
**greenfield** — no existing hub to extend. (`src/Ams.Api/Hubs/LeadScoringHub.cs` is an unrelated
sibling and not part of the Legal solution.)

## 3. Persistence map (existing Wide tables)

| Table | Migration |
| --- | --- |
| `POLOXI.Legal_WideExecution`, `POLOXI.Legal_WideBranch` | `0142` |
| `POLOXI.Legal_WideCandidate`, `POLOXI.Legal_WideCandidateBranchScore` | `0146` |
| `POLOXI.Legal_WideInformationRound/Target/Prediction` | `0149` |
| `POLOXI.Legal_WideNarrowingIteration` | `0155` |
| `POLOXI.Legal_ExecutionBranchOutcome` | `0169` |
| `POLOXI.Legal_SearchContext` | `0181` |
| `POLOXI.Legal_HierarchyBranch`, `POLOXI.Legal_ExecutionEvidence` | `0139` |

### Existing evidence table (spec sections 8–10 gap analysis)

`POLOXI.Legal_ExecutionEvidence` (0139) columns:
`ExecutionEvidenceId, TenantId, PoloxiExecutionId, HierarchyBranchId, SearchDocumentId,
EntityTypeCode, EntityId, SourceReference, Title, Excerpt, RelevanceScore, RankNumber` + audit.

**What already exists:** explicit `HierarchyBranchId` FK — the spec's "explicit BranchId attribution"
invariant is **already honored** for internal evidence.

**Gaps vs. spec (all greenfield, need new `POLOXI.Legal_Research*` tables):**
- No **claim** entity (no `ResearchClaim`).
- No **verification state** (no Pending/Verified/PartiallyVerified/Rejected/Unverifiable).
- No **evidence relation** (Supports/Contradicts/Qualifies/ContextOnly).
- No **source model** (authority/directness/recency/independence group/content hash).
- No **source independence** grouping (raw count == evidence count today).
- No **contradiction** entity.
- No **research action / action-score audit** (spec section 32 — cannot answer "why this search?").
- No **candidate↔evidence provenance** join beyond branch-level.

## 4. Phase 11 conflict resolution (DECISION)

**Decision (user-approved):** Keep the shipped **static tiered routing as v0**. In spec Phase 11,
introduce `IModelRouter` consuming a `ModelRoutingContext` (ambiguity, semantic/decision/domain risk,
prior confidence, remaining budget). The static `MechanicalModel`/`SynthesisModel` split becomes the
**fallback policy** when `Intelligence.Research.AdaptiveModelRouting` (feature flag
`RESEARCH_ADAPTIVE_ROUTING`) is **off**. The static split is a degenerate/constant-risk case of the
adaptive router; nothing shipped blocks the spec. Phase 11 must **replace**, not layer on top of, the
static helpers at the call sites once the router is proven.

## 5. Testing baseline GAP (Phase 0 risk)

**There is NO dedicated test project in the solution** (no `*.Tests.csproj` found;
`tests/Ams.Application.Tests/...` belongs to an unrelated sibling repo). This is the single biggest
Phase 0 risk: the spec's "extract without behavioral change" (Phase 1) is **unverifiable** without a
regression harness.

**Required before any Phase 1 extraction:**
1. Create `Legal/tests/Legal.Application.Tests/Legal.Application.Tests.csproj` (framework TBD — ask
   user; repo has no precedent).
2. Capture golden-master outputs for representative Wide queries (hierarchy shape, candidate ranking,
   convergence outcome) as the regression baseline.
3. Capture current latency / LLM-call / token / cost traces per query for the benchmark comparison
   (spec section 40: Raw LLM / LLM+search / current POLOXI / POLOXI Research).

## 6. Behavioral risks for the future refactor

- `IntelligenceWideService` is a single stateful method with **inline fail-soft semantics** and
  `llmCalls` accounting interwoven with stage logic. Naive `foreach`-stage extraction would break
  fail-soft ordering and the overlapped grounding tasks. Extraction must preserve the stateful
  graph, not linearize it (spec Invariant re: no naive sequential pipeline).
- The existing three-score model (`InterpretationPrior`/`EvidenceSupport`/`PoloxiConfidence`) and
  rich branch states must be **reused, not duplicated**. Introducing spec-named parallel enums would
  fork the state model.
- `WideCandidateDto` scoring formulas are documented and load-bearing — **do not silently change**
  (spec delivery rule).

## 7. Recommended next steps (not yet executed)

1. Decide test framework + create `Legal.Application.Tests` (blocks Phase 1).
2. Capture golden-master + performance baselines.
3. Then Phase 1: extract orchestration boundaries preserving stateful graph + fail-soft.
4. Phase 2: add `POLOXI.Legal_Research*` schema (migration `0206`+) with audit columns and explicit
   `HierarchyBranchId`/`WideCandidateId` FKs.
