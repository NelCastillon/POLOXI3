# EPH Engine Search Module — Complete Technical Documentation

**Module:** Intelligent Search Wide (EPH V2.1)
**Page:** `/intelligence/search/eph_wide`
**API:** `api/intelligence_wide`
**Status:** Production (V2.1 canonical architecture)

---

## Table of Contents

1. [Overview](#1-overview)
2. [Core Concepts](#2-core-concepts)
3. [Architecture and Layering](#3-architecture-and-layering)
4. [Database Schema (Table Layer)](#4-database-schema-table-layer)
5. [Configuration Settings](#5-configuration-settings)
6. [Contracts and DTOs](#6-contracts-and-dtos)
7. [API Layer](#7-api-layer)
8. [Pipeline Stages — Step by Step](#8-pipeline-stages--step-by-step)
9. [Scoring Model](#9-scoring-model)
10. [Branch Lifecycle States](#10-branch-lifecycle-states)
11. [Candidate Competition Engine](#11-candidate-competition-engine)
12. [Termination Reasons](#12-termination-reasons)
13. [Repository and Persistence](#13-repository-and-persistence)
14. [Blazor UI](#14-blazor-ui)
15. [Migrations and Deployment](#15-migrations-and-deployment)
16. [Failure Modes and Fail-Soft Behavior](#16-failure-modes-and-fail-soft-behavior)
17. [Security and Tenancy](#17-security-and-tenancy)
18. [End-to-End Walkthrough Example](#18-end-to-end-walkthrough-example)

---

## 1. Overview

The **EPH Engine** (Exploratory Progressive Hierarchy) is a dynamic, LLM-generated
disambiguation mechanism for enterprise AI search. Instead of forcing an LLM to
guess the single most likely interpretation of an ambiguous question, EPH:

1. Extracts a **Query Contract** separating hard constraints from ambiguous concepts.
2. Asks the LLM to propose a **problem-specific interpretation hierarchy** (not a fixed catalog).
3. **Progressively narrows** the hierarchy level by level, grounding each branch with evidence.
4. Manages branches through a **lifecycle** (`ACTIVE` / `SECONDARY` / `DORMANT` / `PRUNED`)
   instead of hard elimination.
5. Scores every branch with a **three-score model**: Interpretation Prior, Evidence
   Support, and EPH Confidence.
6. Runs a **candidate competition** — every answer candidate is scored against every
   surviving interpretation dimension, with hard constraints enforced visibly.
7. Composes a **grounded, verification-labeled answer** with full auditability.

Design pillars:

- **Depth is unbounded** — the LLM decides when narrowing is done; DB-backed circuit
  breakers (`AbsoluteDepthCeiling`, `MaximumTotalLlmCalls`) exist only to prevent runaway cost.
- **Nothing is ever silently deleted** — pruned branches and constraint-violating
  candidates stay persisted and visible with reasons.
- **DB is the source of truth** — all thresholds, weights, and toggles live in
  `Core.ConfigurationSetting` (Platform scope, tenant-overridable).
- **Fail-soft** — every optional stage (query contract, external grounding, candidate
  competition) degrades gracefully rather than failing the search.

---

## 2. Core Concepts

| Concept | Meaning |
|---|---|
| **Query Contract** | A pre-analysis of the question separating hard constraints (must be satisfied), ambiguous concepts (need disambiguation), and output requirements (shape of the answer). Only ambiguity is branched. |
| **Interpretation Branch** | One possible meaning of an ambiguous concept, proposed by the LLM at a given hierarchy level. |
| **Interpretation Prior** | The LLM's initial percentage estimate for a branch. It controls *retrieval budget allocation*, **not** truth. |
| **Evidence Support** | A deterministic score computed from actual enterprise evidence rows and matched external snippets — what the data supports. |
| **EPH Confidence** | The post-evidence conclusion: a weighted combination of prior and support. |
| **Branch State** | Lifecycle position: `ACTIVE`, `SECONDARY`, `DORMANT`, `PRUNED`. |
| **Candidate** | A concrete answer entity (e.g., a city, a policy, a market) extracted from interpretive result sets and scored against every surviving branch. |
| **Evidence Coverage** | Share of surviving branches (or dimensions, for candidates) backed by at least one evidence item. Low coverage means a result may look strong only because data is missing. |
| **Grounded Answer** | The final answer, labeled `VERIFIED` / `PARTIALLY_VERIFIED` / `INTERPRETIVE` according to how much of it is backed by evidence. |

---

## 3. Architecture and Layering

The module is fully isolated from the classic `api/intelligence/search/eph` path so
it can evolve independently.

```
┌─────────────────────────────────────────────────────────────────┐
│ Blazor UI                                                        │
│ src/Ams.Web/Components/Pages/Intelligence/                       │
│   IntelligenceSearchEphWide.razor  (@page "/intelligence/search/eph_wide") │
└───────────────▲─────────────────────────────────────────────────┘
				│ HTTP (ApiClient)
┌───────────────┴─────────────────────────────────────────────────┐
│ API                                                              │
│ src/Ams.Api/Controllers/IntelligenceWideController.cs            │
│   POST api/intelligence_wide/search/eph_wide                     │
│   POST api/intelligence_wide/search/dynamic                      │
└───────────────▲─────────────────────────────────────────────────┘
				│
┌───────────────┴─────────────────────────────────────────────────┐
│ Application                                                      │
│ src/Ams.Application/IntelligenceWideService.cs                   │
│ src/Ams.Application/Features/Intelligence/IntelligenceWideContracts.cs │
└───────────────▲─────────────────────────────────────────────────┘
				│ Dapper
┌───────────────┴─────────────────────────────────────────────────┐
│ Infrastructure                                                   │
│ src/Ams.Infrastructure/Persistence/Repositories/                 │
│   IntelligenceWideRepository.cs                                  │
│ src/Ams.Infrastructure/Persistence/DatabaseMigrator.cs           │
│ src/Ams.Infrastructure/Migrations/0142..0146_*.sql               │
└───────────────▲─────────────────────────────────────────────────┘
				│
┌───────────────┴─────────────────────────────────────────────────┐
│ SQL Server — EPH schema                                          │
│ EPH.WideExecution, EPH.WideBranch, EPH.WideCandidate,            │
│ EPH.WideCandidateBranchScore + Core.ConfigurationSetting seeds   │
└─────────────────────────────────────────────────────────────────┘
```

LLM calls are routed through the governed AI provider router
(`aiProviderRouter.GenerateAsync`) under the `INTELLIGENCE_WIDE_*` feature
policies seeded in `AI.FeaturePolicy` (migration 0142), so every call is
model-governed, correlated, and auditable.

---

## 4. Database Schema (Table Layer)

All tables live in the `EPH` schema and include the standard base/audit fields
(`TenantId`, `CreatedDateUtc`, `CreatedByUserId`, `ModifiedDateUtc`,
`ModifiedByUserId`, `IsDeleted`).

### 4.1 `EPH.WideExecution` — execution log (one row per search)

| Column | Type | Purpose |
|---|---|---|
| `WideExecutionId` | UNIQUEIDENTIFIER PK | Execution identity |
| `TenantId`, `UserId` | UNIQUEIDENTIFIER | Tenant/user scope |
| `QueryText` | NVARCHAR(1000) | The normalized question |
| `CorrelationId` | NVARCHAR(120) | End-to-end correlation |
| `StatusCode` | NVARCHAR(50), default `RUNNING` | `RUNNING` → `COMPLETED` / `UNAVAILABLE` / `FAILED` |
| `TerminationReasonCode` | NVARCHAR(50) | Why the loop stopped (see §12) |
| `DepthReached` | INT | Deepest hierarchy level executed |
| `LlmCallCount` | INT | Total governed LLM calls |
| `FinalConfidence` | DECIMAL(5,4) | Aggregate confidence |
| `AnswerVerificationCode` | NVARCHAR(50) | `VERIFIED` / `PARTIALLY_VERIFIED` / `INTERPRETIVE` |
| `FinalAnswer` | NVARCHAR(MAX) | Composed answer text |
| `DurationMilliseconds` | BIGINT | Wall-clock duration |
| **V2.1** `QueryContractJson` | NVARCHAR(MAX) | Serialized query contract |
| **V2.1** `EvidenceCoverage` | DECIMAL(5,4) | Share of surviving branches with evidence |
| **V2.1** `ExternalEvidenceCount` | INT | Live external snippets used |
| **V2.1** `EnterpriseEvidenceCount` | INT | Enterprise evidence rows used |
| **V2.1** `CandidateCount` | INT | Candidates ranked in the competition |

Index: `IX_EphWideExecution_Tenant (TenantId, CreatedDateUtc DESC) WHERE IsDeleted=0`.

### 4.2 `EPH.WideBranch` — branch audit (every proposed branch, never deleted)

| Column | Type | Purpose |
|---|---|---|
| `WideBranchId` | UNIQUEIDENTIFIER PK | Branch identity |
| `WideExecutionId` | FK → WideExecution | Owning execution |
| `ParentWideBranchId` | FK → WideBranch (self) | Parent branch in the hierarchy |
| `LevelNumber` | INT | Hierarchy depth (1 = Level 1) |
| `BranchCode` | NVARCHAR(120) | Stable code (used for degenerate-progress detection) |
| `DisplayName` | NVARCHAR(300) | Human-readable interpretation label |
| `Interpretation` | NVARCHAR(1000) | Full interpretation description |
| `CapabilityCode` | NVARCHAR(100) | Enterprise capability match (null on knowledge-only wide path) |
| `SearchText` | NVARCHAR(400) | Retrieval query used for grounding |
| `GroundingStatusCode` | NVARCHAR(50) | `GROUNDED` / `INTERPRETIVE` |
| `EvidenceCount` | INT | Enterprise evidence rows found |
| `Confidence` | DECIMAL(5,4) | LLM's raw branch percentage |
| `ContinueNarrowing` | BIT | LLM's decision to spawn a deeper level |
| `StopReason` | NVARCHAR(50) | LLM's stop rationale |
| `IsEliminated` | BIT | True only when `PRUNED` |
| `EliminationReason` | NVARCHAR(400) | Human-readable state/prune reason |
| `SortOrder` | INT | Display order within the level |
| **V2.1** `BranchStateCode` | NVARCHAR(20), default `ACTIVE` | `ACTIVE` / `SECONDARY` / `DORMANT` / `PRUNED` |
| **V2.1** `InterpretationPrior` | DECIMAL(5,4) | LLM prior (frozen) |
| **V2.1** `EvidenceSupport` | DECIMAL(5,4) | Deterministic evidence score |
| **V2.1** `EphConfidence` | DECIMAL(5,4) | Weighted conclusion |

Index: `IX_EphWideBranch_Execution (WideExecutionId, LevelNumber, SortOrder) WHERE IsDeleted=0`.

### 4.3 `EPH.WideCandidate` — candidate universe (V2.1, never deleted)

| Column | Type | Purpose |
|---|---|---|
| `WideCandidateId` | UNIQUEIDENTIFIER PK | Candidate identity |
| `WideExecutionId` | FK → WideExecution | Owning execution |
| `DisplayName` | NVARCHAR(300) | Candidate name |
| `Detail` | NVARCHAR(1000) | One-sentence description |
| `CompositeScore` | DECIMAL(5,4) | Branch-importance-weighted composite (0 for violators) |
| `RankNumber` | INT | Final rank (violators sink to the bottom) |
| `IsConstraintViolation` | BIT | Failed a hard query constraint |
| `ConstraintViolationReason` | NVARCHAR(400) | Why the candidate was ruled out |

Index: `IX_EphWideCandidate_Execution (WideExecutionId, RankNumber) WHERE IsDeleted=0`.

### 4.4 `EPH.WideCandidateBranchScore` — candidate × branch matrix (V2.1)

| Column | Type | Purpose |
|---|---|---|
| `WideCandidateBranchScoreId` | UNIQUEIDENTIFIER PK | Row identity |
| `WideCandidateId` | FK → WideCandidate | Candidate |
| `WideBranchId` | FK → WideBranch | Interpretation dimension |
| `BranchDisplayName` | NVARCHAR(300) | Denormalized for display |
| `EvidenceScore` | DECIMAL(5,4) | How strongly the candidate performs on that dimension (0–1) |

Index: `IX_EphWideCandidateBranchScore_Candidate (WideCandidateId) WHERE IsDeleted=0`.

---

## 5. Configuration Settings

All settings are seeded into `Core.ConfigurationSetting` (`ScopeCode='Platform'`,
`ModuleCode='Intelligence'`, tenant-overridable) and loaded via
`IntelligenceWideRepository.GetWideConfigurationAsync` into the
`WideConfiguration` record. **Never hardcode these values.**

### Base pipeline (migration 0142)

| Setting Key | Default | Purpose |
|---|---|---|
| `Intelligence.SearchWide.TargetConfidence` | `0.85` | Aggregate confidence at which the loop stops early (`CONFIDENCE_REACHED`) |
| `Intelligence.SearchWide.MinimumBranchConfidence` | `0.35` | Legacy elimination floor (used for evidence ranking) |
| `Intelligence.SearchWide.MaximumBranchesPerLevel` | `5` | Max branches the LLM may propose per level |
| `Intelligence.SearchWide.AbsoluteDepthCeiling` | `25` | Runaway circuit breaker only — the LLM decides natural termination |
| `Intelligence.SearchWide.MaximumTotalLlmCalls` | `30` | Cost circuit breaker per execution |

### V2.1 additions (migration 0146)

| Setting Key | Default | Purpose |
|---|---|---|
| `Intelligence.SearchWide.SecondaryBranchThreshold` | `0.35` | Prior/EphConfidence below this → `SECONDARY` (smaller retrieval budget) |
| `Intelligence.SearchWide.DormantBranchThreshold` | `0.20` | Below this → `DORMANT` (not searched deeper, reactivatable) |
| `Intelligence.SearchWide.PriorWeight` | `0.30` | Weight of the LLM prior in EPH Confidence |
| `Intelligence.SearchWide.EvidenceWeight` | `0.70` | Weight of evidence support in EPH Confidence |
| `Intelligence.SearchWide.MaximumCandidates` | `10` | Max candidates ranked in the competition matrix |
| `Intelligence.SearchWide.EnableQueryContract` | `true` | Toggle Stage 0 query contract extraction |

Additional migrations 0143–0145 seed answer output/input budget settings and
Stage 2.5 external grounding configuration (`WideExternalGroundingConfiguration`:
`Enabled`, `ProviderCode`, `ApiKey`, snippet limits). A blank API key or
`Enabled=false` disables live retrieval and the pipeline degrades to
interpretive-only answers.

---

## 6. Contracts and DTOs

Defined in `src/Ams.Application/Features/Intelligence/IntelligenceWideContracts.cs`.

### 6.1 Request

```
WideSearchRequest(TenantId, UserId, Query [2..1000 chars], MaximumResults [1..100]=25, CorrelationId [<=120])
  + GrantedPermissions : IReadOnlyCollection<string>
  + UseEphEngine       : bool = true   // false = pure LLM answer, no pipeline
```

### 6.2 Response

```
WideSearchResponse(
  WideExecutionId, Query, StatusCode, TerminationReasonCode,
  DepthReached, LlmCallCount, FinalConfidence, AnswerVerificationCode,
  FinalAnswer, Branches, Evidence, SuggestedActions, DurationMilliseconds)
  + ExternalReferences    : WideExternalReferenceDto[]     // LLM real-world links (never enterprise-verified)
  + InterpretiveResults   : WideInterpretiveResultDto[]    // full per-branch LLM result sets
  + ExternalKnowledge     : WideExternalKnowledgeSnippet[] // Stage 2.5 live snippets
  + QueryContract         : WideQueryContract?             // V2.1
  + Candidates            : WideCandidateDto[]             // V2.1
  + EvidenceCoverage      : decimal                        // V2.1
  + ExternalEvidenceCount / EnterpriseEvidenceCount : int  // V2.1
```

### 6.3 Branch

```
WideBranchDto(WideBranchId, ParentWideBranchId, LevelNumber, BranchCode, DisplayName,
  Interpretation, CapabilityCode, SearchText, GroundingStatusCode, EvidenceCount,
  Confidence, ContinueNarrowing, StopReason, IsEliminated, EliminationReason, SortOrder)
  + BranchStateCode     : string = "ACTIVE"      // WideBranchStates constants
  + InterpretationPrior : decimal
  + EvidenceSupport     : decimal
  + EphConfidence       : decimal
```

`WideBranchStates`: `ACTIVE`, `SECONDARY`, `DORMANT`, `PRUNED`.

### 6.4 Query Contract (V2.1)

```
WideQueryContract(EntityType, GeographicConstraint, RequestedCount, RankingConcept,
  HardConstraints[], AmbiguousConcepts[], OutputRequirements[])
```

### 6.5 Candidates (V2.1)

```
WideCandidateDto(WideCandidateId, RankNumber, DisplayName, Detail, CompositeScore, BranchScores[])
  + EvidenceCoverage      : decimal   // share of dimensions the candidate has scores for
  + IsConstraintViolation : bool      // ruled out — kept visible, never hidden

WideCandidateBranchScoreDto(BranchDisplayName, EvidenceScore)
```

### 6.6 Supporting DTOs

- `WideExternalReferenceDto(Title, Url, Source, Summary, BranchDisplayName)`
- `WideInterpretiveResultDto(BranchDisplayName, Interpretation, Confidence, Items[])`
  with `DataVolatility` (`STABLE` | `TIME_SENSITIVE`) and `IsExternallyGrounded`
- `WideInterpretiveResultItemDto(RankNumber, Name, Detail)`
- `WideActionSuggestionDto(DisplayName, NavigationRoute, Rationale)`
- `WideConfiguration` — see §5

---

## 7. API Layer

Controller: `src/Ams.Api/Controllers/IntelligenceWideController.cs`
Route prefix: `api/intelligence_wide` — isolated from the classic
`api/intelligence/search/eph` endpoint.

| Endpoint | Method | Auth | Purpose |
|---|---|---|---|
| `search/eph_wide` | POST | `IntelligencePolicies.Search` | Wide variant of the classic EPH search (`EphSearchRequest`) |
| `search/dynamic` | POST | `IntelligencePolicies.Search` | The V2.1 dynamic progressive disambiguation pipeline (`WideSearchRequest`) |

Both endpoints overwrite `TenantId`, `UserId`, and `GrantedPermissions` from the
authenticated principal (`AuthenticatedRequestContext`) — client-supplied values
are never trusted.

---

## 8. Pipeline Stages — Step by Step

Entry point: `IntelligenceWideService.SearchDynamicAsync(WideSearchRequest, CancellationToken)`.

### Step 1 — Validation and Normalization
- Data-annotation validation plus a Guid-not-empty check on every Guid property.
- Query normalized/trimmed; `MaximumResults` clamped to 1–100; a `CorrelationId`
  of the form `wide-search:{guid}` is generated when blank.
- **EPH Engine off** (`UseEphEngine=false`): the request short-circuits to
  `SearchLlmOnlyAsync` — a pure LLM answer with no hierarchy, grounding, or elimination.

### Step 2 — Configuration Load and Execution Start
- `GetWideConfigurationAsync(tenantId)` loads all §5 settings (DB source of truth).
- The wide path is **knowledge-only**: the capability catalog is intentionally empty,
  which forces every branch onto the `INTERPRETIVE` reasoning path.
- `StartWideExecutionAsync` inserts the `EPH.WideExecution` row (`StatusCode=RUNNING`).

### Step 3 — Stage 0: Query Contract (V2.1, fail-soft)
- When `EnableQueryContract=true`, `ExtractQueryContractAsync` asks the LLM to split
  the question into hard constraints, ambiguous concepts, and output requirements
  (entity type, geographic constraint, requested count, ranking concept).
- A null contract silently degrades to V2 behavior (branch the whole query).
- Counts as one LLM call; the contract is later persisted as `QueryContractJson`.

### Step 4 — Stage 1: Level-1 Hierarchy Proposal
- `ProposeIntentAsync` frames the ambiguous intent and asks the LLM for a
  problem-specific Level-1 hierarchy (open-ended, not catalog-limited), informed
  by the query contract so **only ambiguity is branched** — hard constraints are
  passed through as filters, not interpretations.
- `MaterializeBranches` assigns IDs/levels/sort order and clamps confidences;
  branches are persisted via `SaveWideBranchesAsync`.

### Step 5 — Stage 2: Iterative Narrowing Loop
For each level (depth++):

1. **Ground each branch** (`GroundBranchAsync`) — collects evidence keyed per branch
   (deduplicated via `branchEvidenceKeys`); status becomes `GROUNDED` or `INTERPRETIVE`.
2. **Assign lifecycle state** from the interpretation prior (see §10). Key rule:
   the LLM percentage is a *retrieval allocation prior*, not truth — low priors
   demote to `SECONDARY`/`DORMANT` instead of eliminating. `PRUNED` at this stage
   is reserved for grounded branches with zero enterprise evidence and confidence
   below `TargetConfidence`.
3. **Persist outcomes** (`UpdateWideBranchOutcomeAsync`).
4. **Survivor selection**: `ACTIVE` and `SECONDARY` branches keep narrowing;
   `DORMANT` branches stay in the answer path but do not spawn deeper levels;
   `PRUNED` stops entirely. Zero survivors → `NO_SURVIVORS`.
5. **Loop exit checks** (minimum depth 2 enforced before natural exits):
   - `aggregateConfidence >= TargetConfidence` → `CONFIDENCE_REACHED`
   - all survivors have `ContinueNarrowing=false` → `LLM_COMPLETE`
   - `depth >= AbsoluteDepthCeiling` → `DEPTH_CEILING_REACHED`
   - `llmCalls + 2 > MaximumTotalLlmCalls` → `LLM_CALL_CEILING_REACHED`
6. **Propose the next level** (`ProposeNextLevelAsync`) from surviving branches and
   their grounding outcomes. A **degenerate-progress guard** drops any proposed
   branch whose `BranchCode` matches the current level (LLM merely rephrasing);
   if nothing new remains → `NO_PROGRESS`.

### Step 6 — Evidence Ranking
- Only evidence attached to **surviving** branches is ranked (`RankEvidence`);
  evidence from later-pruned branches never surfaces as authorized evidence.

### Step 7 — Stage 2.5: Live External Grounding (fail-soft)
- `GatherExternalKnowledgeAsync` retrieves fresh web snippets for `INTERPRETIVE`
  survivors so time-sensitive figures come from current sources rather than stale
  model memory. Disabled/failed retrieval degrades to interpretive-only answers.

### Step 8 — V2.1 Three-Score Computation and Reweight
For each surviving branch (see §9 for formulas):
1. `EvidenceSupport = ComputeEvidenceSupport(branch, evidence, externalKnowledge)`
2. `EphConfidence = clamp(PriorWeight × Prior + EvidenceWeight × Support, 0, 1)`
3. **Reweight**: the branch state is recomputed from `EphConfidence` — a `DORMANT`
   branch with strong evidence is *reactivated*; a high-prior branch without support
   is *demoted*. `PRUNED` is terminal and never reactivated.
4. Scores persisted via `UpdateWideBranchScoresAsync`.

### Step 9 — Stage 3: Answer Composition
- `ComposeAnswerAsync` composes the verified answer from surviving paths, ranked
  evidence, external knowledge, and the query contract.
- On `AiProviderUnavailableException`/`TimeoutException`: status `UNAVAILABLE`,
  verification falls back to `PARTIALLY_VERIFIED` (if evidence exists) or `INTERPRETIVE`.
- **Relevance validation**: only evidence the answer LLM judged relevant
  (`RelevantEvidenceNumbers`) is kept — keyword grounding can match superficially
  and must not surface or inflate confidence. `INTERPRETIVE` answers or empty
  relevant evidence cap the aggregate confidence at the answer's own confidence.

### Step 10 — V2.1 Candidate Competition
- `MapInterpretiveResults` builds the per-branch result sets; if non-empty and the
  LLM-call budget allows, `CompeteCandidatesAsync` runs the candidate engine (§11).

### Step 11 — Finalization
- `UpdateWideExecutionContractAsync` persists the contract JSON, evidence coverage,
  evidence counts, and candidate count; the execution row is completed with status,
  termination reason, depth, call count, confidence, verification code, answer,
  and duration. The full `WideSearchResponse` is returned.

---

## 9. Scoring Model

### 9.1 Interpretation Prior
The LLM's branch percentage, clamped to [0,1]. Frozen into
`InterpretationPrior` when the branch is graded. Controls retrieval budget only.

### 9.2 Evidence Support (deterministic)
```
enterpriseCount   = evidence rows attached to the branch
enterpriseSupport = 0                          if count = 0
				  = min(0.9, 0.5 + 0.2×(count−1))   otherwise
					// 1 item = 0.50, 2 = 0.70, 3+ ≈ 0.90 (saturating)

matchedSnippets   = external snippets whose retrieval query contains the branch DisplayName
externalSupport   = 0 if none, else
					clamp(max(snippet.Score), 0, 1) × min(1, 0.6 + 0.1×snippetCount)

EvidenceSupport   = clamp(max(enterpriseSupport, externalSupport), 0, 1)
```

### 9.3 EPH Confidence
```
EphConfidence = clamp(PriorWeight × InterpretationPrior
			  + EvidenceWeight × EvidenceSupport, 0, 1)
```
Defaults: `PriorWeight = 0.30`, `EvidenceWeight = 0.70` — evidence dominates priors.

### 9.4 Evidence Coverage (execution-level)
Share of surviving branches supported by at least one evidence item (external or
enterprise). Persisted on `EPH.WideExecution.EvidenceCoverage` and shown in the UI.

---

## 10. Branch Lifecycle States

| State | Entry Condition (initial, from prior) | Behavior |
|---|---|---|
| `ACTIVE` | prior ≥ `SecondaryBranchThreshold` (0.35) | Full retrieval budget; spawns deeper levels |
| `SECONDARY` | `DormantBranchThreshold` (0.20) ≤ prior < 0.35 | Reduced retrieval budget; still narrows |
| `DORMANT` | prior < 0.20 | Not searched deeper; stays in the answer path; **reactivatable** |
| `PRUNED` | grounded with zero enterprise evidence and confidence < `TargetConfidence`, or hard-constraint violation | Terminal; never reactivated; kept persisted and visible with reason |

**Reweight rule (post-evidence):** after `EphConfidence` is computed, the state is
recomputed against the same thresholds using `EphConfidence` instead of the prior —
evidence revises interpretation. `PRUNED` is excluded from reweighting.

Every non-`ACTIVE` state carries a human-readable `EliminationReason`, e.g.
*"Interpretation prior 18% below 20%: dormant, reactivatable if evidence supports it."*

---

## 11. Candidate Competition Engine

Implemented in `CompeteCandidatesAsync` (fail-soft — any LLM failure returns an
empty collection and never fails the search).

1. **Dimension selection** — up to 8 surviving `ACTIVE`/`SECONDARY` branches,
   ordered by `EphConfidence` descending.
2. **Candidate universe** — distinct item names harvested from the interpretive
   result sets, capped at `MaximumCandidates × 2`.
3. **LLM matrix scoring** — one governed call (`WIDE_CANDIDATE_MATRIX` on the
   `INTELLIGENCE_WIDE_ANSWER` feature) scores every candidate against every branch
   (0–1, forced differentiation), and flags hard-constraint violations with a reason.
4. **Composite score** (branch-importance-weighted):
   ```
   composite = Σ over scored branches ( EphConfidence_b / Σ EphConfidence × evidenceScore_b )
   ```
5. **Coverage penalty** — `coverage = scoredBranches / totalBranches`;
   `composite ×= coverage`. A candidate scored on only a fraction of the surviving
   dimensions must not compete equally — missing data is not strength; gaps pull
   ranking down, never up.
6. **Constraint engine** — violators get `CompositeScore = 0`, keep their reason,
   and remain **visible** (displayed as *"Ruled out: {reason}"*), sorted below all
   non-violators.
7. **Ranking and persistence** — ordered by violation flag then composite, top
   `MaximumCandidates` kept, `RankNumber` assigned, saved via
   `SaveWideCandidatesAsync` into `EPH.WideCandidate` + `EPH.WideCandidateBranchScore`.

---

## 12. Termination Reasons

| Code | Meaning |
|---|---|
| `LLM_COMPLETE` | Natural termination — no surviving branch wants further narrowing (min depth 2) |
| `CONFIDENCE_REACHED` | Aggregate confidence hit `TargetConfidence` (min depth 2) |
| `NO_SURVIVORS` | Every branch at a level was pruned |
| `NO_PROGRESS` | Degenerate-progress guard — the LLM only rephrased the current level |
| `DEPTH_CEILING_REACHED` | Runaway circuit breaker (`AbsoluteDepthCeiling`) — audited, never a functional limit |
| `LLM_CALL_CEILING_REACHED` | Cost circuit breaker (`MaximumTotalLlmCalls`) |

---

## 13. Repository and Persistence

`src/Ams.Infrastructure/Persistence/Repositories/IntelligenceWideRepository.cs`
(Dapper over `Microsoft.Data.SqlClient`):

| Method | Purpose |
|---|---|
| `GetWideConfigurationAsync(tenantId)` | Loads §5 settings (tenant override → platform default) |
| `StartWideExecutionAsync(record)` | Inserts the `EPH.WideExecution` row |
| `SaveWideBranchesAsync(branches, userId)` | Bulk-inserts branch rows including V2.1 columns |
| `UpdateWideBranchOutcomeAsync(...)` | Persists grounding status, evidence count, elimination |
| `UpdateWideBranchScoresAsync(tenantId, branchId, state, prior, support, ephConfidence)` | Persists V2.1 three-score results and final state |
| `SaveWideCandidatesAsync(candidates, userId)` | Persists candidates and the branch-score matrix |
| `UpdateWideExecutionContractAsync(...)` | Persists contract JSON, coverage, counts |
| `CompleteWideExecutionAsync(...)` | Final status/termination/answer/duration |

All queries are tenant-scoped; all decimal scores are clamped to fit
`DECIMAL(5,4)` before persistence.

---

## 14. Blazor UI

Page: `src/Ams.Web/Components/Pages/Intelligence/IntelligenceSearchEphWide.razor`
Route: `/intelligence/search/eph_wide`
Authorization: `IntelligencePolicies.Search`

Sections (top to bottom):

1. **Hero header** — module title and concept summary.
2. **Search row** — query input (`Enter` triggers search), the **EPH Engine toggle**
   (off = pure LLM answer), and the *Disambiguate & Answer* button.
3. **Loading / error states** — progress narrative; errors render as a Bootstrap
   `alert-danger` (with the standard red left border and shake animation) including
   the exception message.
4. **Grounded Answer panel** — the final answer with its verification label
   (`VERIFIED` / `PARTIALLY_VERIFIED` / `INTERPRETIVE`) and confidence.
5. **Query Contract chips** — entity type, geography, requested count, ranking
   concept, hard constraints vs ambiguous concepts vs output requirements.
6. **Candidate Competition** — ranked candidates with composite scores, per-branch
   score chips, evidence-coverage indicator, and visible *"Ruled out: …"* rows for
   constraint violators.
7. **Interpretation Branches** — per-level branch cards showing state badge
   (`BranchStateLabel(...)`), the three scores (Prior / Evidence / EPH), grounding
   status, and stop/demotion reasons.
8. **Authorized Evidence** — relevance-validated enterprise evidence, external
   references (real links, never enterprise-verified), and interpretive result sets
   with `TIME_SENSITIVE` volatility warnings.
9. **EPH Journey strip** — depth reached, LLM call count, active branch count
   (`ActiveBranchCount`), evidence coverage, external/enterprise evidence counts,
   candidate count, termination reason, and duration.

The page calls `POST api/intelligence_wide/search/dynamic` through the shared
`ApiClient` and renders `WideSearchResponse` directly — no client-side scoring or
business rules are duplicated in the UI.

---

## 15. Migrations and Deployment

Migrations are embedded resources in `Ams.Infrastructure.csproj` and registered in
`src/Ams.Infrastructure/Persistence/DatabaseMigrator.cs`, which applies them once
and tracks them in `dbo._Migrations`. **Both** the embedded-resource entry and the
migrator registry entry are required — an embedded script that is not registered
will never run (this caused the historical `Invalid column name 'BranchStateCode'`
runtime failure).

| Registry Key | Script | Adds |
|---|---|---|
| `0332_Intelligence_Wide_Dynamic_Hierarchy` | `0142_IntelligenceWideDynamicHierarchy.sql` | `EPH` schema, `WideExecution`, `WideBranch`, base settings, `AI.FeaturePolicy` seeds |
| `0333_Intelligence_Wide_Answer_Output_Budget` | `0143_IntelligenceWideAnswerOutputBudget.sql` | Answer output budget setting |
| `0334_Intelligence_Wide_External_Knowledge` | `0144_IntelligenceWideExternalKnowledge.sql` | Stage 2.5 external grounding settings |
| `0335_Intelligence_Wide_Answer_Input_Budget` | `0145_IntelligenceWideAnswerInputBudget.sql` | Answer input budget setting |
| `0336_Intelligence_Wide_V21` | `0146_IntelligenceWideV21.sql` | V2.1 columns, `WideCandidate`, `WideCandidateBranchScore`, V2.1 settings |

All scripts are idempotent (`COL_LENGTH`/`OBJECT_ID` guards and `MERGE` seeds) and
transactional (`SET XACT_ABORT ON; BEGIN TRANSACTION`). Migrations apply
automatically at host startup.

---

## 16. Failure Modes and Fail-Soft Behavior

| Failure | Behavior |
|---|---|
| Query contract LLM failure | Contract is null → V2 behavior (branch the whole query) |
| External grounding disabled/failed | Interpretive-only answers; `IsExternallyGrounded=false` |
| Answer LLM unavailable/timeout | Execution status `UNAVAILABLE`; verification falls back to `PARTIALLY_VERIFIED`/`INTERPRETIVE` |
| Candidate engine LLM failure | Empty candidate collection; search still succeeds |
| No survivors / no progress | Clean termination with an audited reason; partial results still returned |
| Runaway depth/cost | Circuit breakers stop the loop and record the ceiling reason |

Nothing in the optional stages can fail the core search; the only hard failures
are validation errors, authentication errors, and infrastructure exceptions
(surfaced through `ExceptionHandlingMiddleware`).

---

## 17. Security and Tenancy

- Both endpoints require the `IntelligencePolicies.Search` authorization policy.
- `TenantId`, `UserId`, and `GrantedPermissions` are always taken from the
  authenticated principal, never from the request body.
- Every table row and every repository query is tenant-scoped.
- All LLM calls go through the governed AI provider router with feature policies,
  correlation IDs, and usage context (`"Intelligent Search Wide"`), making every
  call auditable and budget-governed.
- Evidence is permission-filtered by the caller's `GrantedPermissions`.

---

## 18. End-to-End Walkthrough Example

Question: *"Top 5 safest cities in Southeast Asia for retirees"*

1. **Query Contract**: entity type = City; geographic constraint = Southeast Asia;
   requested count = 5; ranking concept = "safest … for retirees";
   hard constraints = [city, in Southeast Asia]; ambiguous concepts =
   ["safest", "for retirees"]; output = ranked list of 5.
2. **Level 1**: the LLM branches only the ambiguity — e.g. *Crime Safety (45%)*,
   *Healthcare Quality (25%)*, *Political Stability (18%)*, *Natural-Disaster Risk (12%)*.
3. **States from priors**: Crime = `ACTIVE`; Healthcare = `SECONDARY`;
   Political & Disaster = `DORMANT` (kept, not eliminated).
4. **Narrowing**: `ACTIVE`/`SECONDARY` branches spawn deeper levels; each level is
   grounded; the loop ends with `LLM_COMPLETE` when no survivor wants to continue.
5. **Three scores + reweight**: external snippets strongly support disaster-risk
   data → the `DORMANT` disaster branch's `EphConfidence` crosses 0.35 and it is
   **reactivated to `ACTIVE`**; a high-prior branch without support is demoted.
6. **Candidates**: cities harvested from interpretive result sets are scored against
   every surviving dimension. A city outside Southeast Asia is flagged
   `IsConstraintViolation=true` and shown as *"Ruled out: not in Southeast Asia"* at
   the bottom — never hidden. Composite = branch-weighted score × coverage.
7. **Answer**: a grounded, verification-labeled ranked list of 5 cities, with the
   query contract, branch journey, candidate matrix, evidence, and EPH Journey
   metrics all visible and fully persisted for audit.

---

*Document generated from the implementation as of migration `0336_Intelligence_Wide_V21`
on branch `upgrade-dotnet-10`. The database remains the source of truth for all
thresholds, weights, and toggles.*
