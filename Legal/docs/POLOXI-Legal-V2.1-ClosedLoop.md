# POLOXI Legal V2.1 — Dependency-Propagation Closed Loop

This document describes the V2.1 closed-loop layer added on top of POLOXI Core and the
V2 typed legal dependency graph. It records the architecture, the authoritative invariant,
and the end-to-end flow.

## Authoritative invariant (do not violate)

**POLOXI Core is the only scorer.** The dependency graph is a deterministic causal-impact
layer. It answers exactly one question:

> "If this evidence / fact / authority / proposition changes, what parts of the current
> decision are affected?"

The graph NEVER assigns a candidate score. It only emits **domain-neutral signals**
(`DecisionBranchSignal`: a signed support delta and optional reopen request, keyed by
branch/candidate id). POLOXI Core consumes those signals and re-runs the *same* composite
scoring / entropy / margin / frontier / information-value math it used on the first pass.

This is not GraphRAG. The graph does not retrieve, rank, or answer. It propagates impact.

## Components

| Concern | Type | Location |
| --- | --- | --- |
| Graph impact propagation | `DecisionGraph.PropagateImpact` | `Application/.../Decision/Core/DecisionGraph.cs` |
| Propagation wrapper (single edge change) | `DependencyPropagationService` | `.../Core/DependencyPropagationService.cs` |
| Graph impact → authoritative branch/candidate signals | `LegalDecisionImpactMapper` | `.../Core/LegalDecisionImpactMapper.cs` |
| Authoritative re-scoring | `DecisionRecompetition.Run` | `.../Core/DecisionRecompetition.cs` |
| Outcome-directed research need | `DecisionResearchNeedFactory.Create` | `.../Core/DecisionResearchNeedFactory.cs` |
| Orchestration | `LegalDecisionService.ApplyVerificationChangeAsync` | `Application/LegalDecisionService.cs` |
| Persistence + idempotency + audit | `LegalDecisionRepository` | `Infrastructure/.../LegalDecisionRepository.cs` |
| API endpoint | `POST api/legal_decision/sessions/{sessionId}/verify` | `Api/Controllers/LegalDecisionController.cs` |
| Blazor cockpit action + readback | `LegalDecision.razor` | `Web/Components/Pages/LegalDecision.razor` |

## Database (migrations)

- `0220_LegalDecisionGraphLineage.sql` — lineage/version columns on graph nodes and edges so
  graph impact maps back to authoritative POLOXI branches/candidates/evidence.
- `0221_LegalDecisionClosedLoopTables.sql` — `Legal_DecisionDependencyEvent` (idempotency),
  `Legal_DecisionRecompetition`, `Legal_DecisionResearchNeed`, `Legal_DecisionFrontierSnapshot`.
- `0222_LegalDecisionClosedLoopConfig.sql` — V2.1 flags (`Decision.V2.UseDependencyPropagation`,
  `Decision.V2.UseGraphDrivenRecompetition`, `Decision.V2.UseGraphFrontierSignals`) and
  loop-safety limits (bounded reopens per branch, epsilon), default ON.

All new tables include the standard base/audit fields (`TenantId`, `CreatedDateUtc`,
`CreatedByUserId`, `ModifiedDateUtc`, `ModifiedByUserId`, `IsDeleted`). Every query and
mutation stays tenant-scoped.

## End-to-end flow (synchronous verify)

1. User marks a graph edge VERIFIED / INVALIDATED in the cockpit (or an API client posts to
   the verify endpoint) with an `IdempotencyKey`.
2. `LegalDecisionService.ApplyVerificationChangeAsync` checks the dependency-event idempotency
   key. A retried event is a no-op that returns the prior result (`AlreadyProcessed = true`).
3. `DependencyPropagationService.Apply` builds the graph model, applies the single edge status
   change, recomputes support, and returns a structured `DependencyImpact`.
4. `LegalDecisionImpactMapper.Map` translates the impact into domain-neutral
   `DecisionBranchSignal`s using lineage first, then deterministic branch-code prefix fallback.
   Signals without authoritative attachment are dropped.
5. `DecisionRecompetition.Run` folds branch deltas into owning candidates, re-scores
   verification/authority/evidence dimensions, recomputes composite/ceiling/entropy/margin,
   re-ranks, and applies bounded branch reopens.
6. `DecisionResearchNeedFactory.Create` selects the reopened or highest-IV open-frontier branch
   and emits the next investigation (or null if no open frontier remains).
7. The service persists the edge change, dependency event, re-ranked branches/candidates,
   session outcome, recompetition record, and a frontier snapshot, then returns a
   `DecisionClosedLoopResultDto` with an audit narrative (no chain-of-thought).

## Determinism & safety

- **Deterministic:** the same signal set applied to the same inputs yields the same ranking.
- **Idempotent:** dependency events are keyed; retries do not double-apply.
- **Bounded:** reopens are capped by loop-safety settings to prevent oscillation.
- **Flags-off baseline:** when V2.1 flags are off, propagation is skipped and the V1/V2 paths
  behave exactly as before.

## Tests

`Legal.Application.Tests/Intelligence/DecisionClosedLoopRecompetitionTests.cs`:
- `EssentialDependencyFailure_FlipsWinner_AndIsIdempotent` — an essential-dependency failure
  flips the winner, reopens the affected branch, and re-running is stable (idempotent).
- `NoSignals_LeavesRankingUnchanged` — with no signals the ranking is unchanged.
