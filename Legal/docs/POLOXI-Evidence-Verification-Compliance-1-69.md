# POLOXI Verified Decision Intelligence Compliance Matrix (1–69)

Status meanings: **Complete** = implemented and covered at the required architectural boundary; **Partial** = core behavior exists but one or more specified controls are absent; **Missing** = no conforming implementation was found; **External** = requires non-code evidence or infrastructure outside this repository.

| # | Status | Repository evidence / confirmed gap |
|---:|---|---|
| 1 | Partial | Deterministic authorization and fail-closed rules exist in `EvidenceVerificationAggregator` and tests, but the 20 invariants are not represented as a named regression set. |
| 2 | Complete | `LegalDecisionService.RunResearchLoopAsync` inserts independent verification before the existing signal gate, attachment, propagation and recompetition path; no second loop exists. |
| 3 | Complete | `ResearchNeedTypeCode`, `DecisionResearchNeedTypes` and `DecisionResearchNeedFactory.ClassifyResearchNeed` route legal versus matter needs. |
| 4 | Complete | `EvidenceSourceType` covers the specified legal and matter source classes and remains extensible. |
| 5 | Complete | Source-specific profiles expose explicit profile version plus required and optional factor sets while preserving profile applicability. |
| 6 | Complete | Every factor has state, reason, evidence, passage reference, method, verifier ID/version and evaluation time. |
| 7 | Complete | Proposition support has component decomposition, grounding text, passage reference and verifier provenance. |
| 8 | Complete | Identity/provenance/citation/passage execute before the semantic verifier and short-circuit deterministically. |
| 9 | Complete | `IVerificationCandidatePreScreen` controls semantic token expenditure; rejection is explicitly non-authoritative and unverifiable. |
| 10 | Complete | Missing/unusable passage yields not-evaluated/unverifiable behavior rather than unsupported authority. |
| 11 | Complete | The semantic request is bounded to proposition, profile requirements and untrusted source passage without winner/score/recommendation context. |
| 12 | Complete | One semantic call returns proposition support, statement role and holding; one repair retry is bounded. |
| 13 | Complete | JSON/schema transport, allowed enums, required object parsing, passage grounding and fail-closed invalid output are enforced. |
| 14 | Complete | Returned supporting passages must normalize to content contained in the supplied passage. |
| 15 | Complete | `LegalStatementRole` models court, party, procedural, quoted-authority and separate-opinion roles. |
| 16 | Complete | Holding is returned independently with passed/failed/inconclusive/not-applicable semantics and reason codes. |
| 17 | Partial | Authority is deterministic and never LLM-authorized, but provider/citator treatment and publication/precedential metadata are not represented. |
| 18 | Partial | Deepening is gated by ambiguity and decision materiality and disabled by default; production enablement remains intentionally pending benchmark evidence. |
| 19 | Complete | `PoloxiVerificationContract` bounds outcomes, passages, semantic factors, unresolved discriminators, rounds, tokens and prohibits external retrieval. |
| 20 | Missing | The production deepener is intentionally no-op; bounded candidate × factor competition is not implemented. |
| 21 | Missing | No explicit unresolved-discriminator-only adaptive narrowing contract exists. |
| 22 | Missing | No production resolution-deepening implementation records what must be true for one support outcome to beat another. |
| 23 | Complete | Deepening uses existing decision materiality and ambiguity; no second IV or entropy formula was introduced. |
| 24 | Complete | `EvidenceVerificationAggregator` alone derives verified/authorized state from required factor outcomes and full support. |
| 25 | Partial | Partial evidence is non-authoritative and attached distinctly, but automatic creation of a narrower follow-up need from unsupported components is absent. |
| 26 | Complete | `DecisionEvidenceAttachment` persistence is a thin provenance/authority boundary; it does not duplicate verification logic. |
| 27 | Complete | Authorized signals flow through the existing dependency propagation, recompetition, frontier and readiness architecture. |
| 28 | Complete | Authority gain and loss both use the existing closed loop; an UNVERIFIED result propagates only when a dependency was previously VERIFIED, while first-time failed evidence remains non-mutating. |
| 29 | Complete | Cache identity includes normalized proposition/passage, source provider/version, extraction version, profile code/version, semantic verifier version and prompt schema. |
| 30 | Complete | Immutable `EvidenceSourceSnapshot` persistence stores provider/version/reference, retrieval time, content/passage hashes, passage coordinates and extraction lineage. |
| 31 | Complete | Retrieved/pre-screen, mechanical/semantic/deepening/cache, token, latency, disposition, verified and authorized totals are persisted or deterministically projected per execution. |
| 32 | Complete | DB-backed input/output budgets fail closed with `VERIFICATION_BUDGET_EXHAUSTED`; schema repair is bounded. |
| 33 | Partial | Serializable locking and monotonically allocated verification versions prevent conflicting version commits; explicit caller-supplied expected-version reconciliation remains absent. |
| 34 | Partial | Snapshot, verification run and factor rows commit atomically; evidence authority, attachment and event still use the surrounding research-round boundary. |
| 35 | Complete | `VerificationFailureCodes` centralizes the specified technical failure taxonomy separately from epistemic support outcomes. |
| 36 | Complete | Prompt contract and serialized payload mark source content untrusted; schema/grounding and deterministic aggregation prevent source-controlled authorization. |
| 37 | Partial | Matter-document profiles and provenance checks reuse the engine; complete upload→scan→snapshot→OCR lineage integration is outside this verification slice. |
| 38 | Complete | Proposition is an input to semantic verification and cache identity, so the same passage can yield different support outcomes. |
| 39 | Partial | Contradiction is a first-class disposition and attachment state; explicit conflicting-fact graph fixtures from two matter documents are absent. |
| 40 | Complete | Existing output claim extraction/audit/transform controls remain active and separate. |
| 41 | Complete | Output authorization now preserves and persists claim→branch/candidate→evidence→attachment→verification→snapshot→passage lineage; semantic extraction remains the backstop. |
| 42 | Complete | Source snapshots, versioned verification runs, normalized factor rows, attachments, output provenance and verification telemetry are normalized and linked. |
| 43 | Complete | Structural graph verification, evidence lifecycle/status, support disposition, authorization and attachment status are separate concepts. |
| 44 | Partial | Support and authority axes are separate; a normalized processing-state axis for verification executions is absent. |
| 45 | Complete | The existing Blazor integrity-trace component receives read-only evidence drill-down, factor states/reasons/blockers, disposition totals, spend/cache/deepening metrics and decision-authorized totals. |
| 46 | Complete | The compact trace retains Research/Verify/Promote/Propagate/Recompete and expands Verify diagnostics without adding stages. |
| 47 | Complete | Trace projection is read-only and never creates authoritative verification state. |
| 48 | Partial | Positive/negative factor and ablation tests exist; provider-specific wrong-page/OCR and complete citator-treatment fixtures require their corresponding production providers. |
| 49 | Complete | Existing lexical/title-echo/unrelated-source false positives are permanent adversarial regression fixtures. |
| 50 | Complete | Required-factor ablations independently prove each required factor blocks authority. |
| 51 | Complete | Verified-evidence, failed-factor non-mutation and corruption-cascade tests cover authorization, attachment, propagation, recompetition and frontier effects. |
| 52 | Partial | Matter-document tests cover proposition scope and independently grounded contradictory documents; a live upload/security/OCR integration fixture remains outside this application test layer. |
| 53 | Complete | Injection fixtures cover imperative instructions, malicious JSON/system-role text, fake citation instructions and fabricated supporting passages. |
| 54 | Partial | Repeated-run stability tests measure deterministic disposition, authority, semantic-call and deepening consistency; live-provider variance requires a configured model environment. |
| 55 | Missing/External | No versioned attorney-reviewed semantic ground-truth corpus with rationale exists; expert labels require external review. |
| 56 | Partial | Controlled current/independent/signal-gate/closed-loop and deepening-gate ablations exist; live A–E model/cost comparison requires a configured provider and ground-truth corpus. |
| 57 | Partial | Authorization and token/latency/cache signals exist, but precision/recall by disposition, holding/authority accuracy and cost-per-correct-authorization reporting are absent. |
| 58 | Missing | No POLOXI incremental-value metric computes prevented false authorizations or correctly resolved ambiguity per incremental token. |
| 59 | Partial | Contracts, mechanical, semantic, authorization, graph integration, output provenance and staged DB flags exist; production deepening remains intentionally disabled pending measured external benchmark value. |
| 60 | Complete | DB-backed verification settings control enablement, shadow mode, mechanical/semantic paths, input/output limits, repair, deepening, authorization enforcement and cache. |
| 61 | Partial | Profile, factor, semantic, deepening, aggregator and verified-signal boundaries are separate; mechanical aggregation and deepening contracts are less explicit than specified. |
| 62 | Partial | Production orchestration now includes snapshot identity, pre-screen, hard semantic budget, bounded deepening, aggregation and atomic verification persistence; full authority/attachment/event atomicity remains incomplete. |
| 63 | Complete | No second graph/loop/scorer, verifier swarm, six-call design, verification-specific recompetition or LLM-controlled authorization was introduced. |
| 64 | Partial | The three-layer Evidence→Decision→Output architecture, snapshots, budgets, lineage, diagnostics and controlled tests are implemented; attorney-reviewed ground truth and live-provider deepening-value evidence remain external release gates. |
| 65 | Complete | `EpistemicDecisionBridge` always invokes `DeterministicOutputClaimExtractor` for substantive composed output, including when composer provenance is supplied; regression coverage confirms provenance cannot bypass independent decomposition. |
| 66 | Complete | `IOutputClaimProvenanceReconciler` deterministically reconciles each extracted claim with composer provenance and an authoritative proposition, assigning `Mapped`, `Unmapped`, or `ScopeExceeded` with explicit reason codes. |
| 67 | Complete | The output claim ledger carries claim text, materiality, source proposition, mapping state, disposition, evidence and attachment identifiers; `Legal_DecisionOutputClaimProvenance` persists reconciliation state with the complete evidence→verification→passage→snapshot lineage. |
| 68 | Complete | `OutputClaimDispositions.EnforceMappingInvariant` and the bridge audit backstop forbid `ALLOW` for every material unmapped or scope-exceeding claim and emit fail-closed authorization violations. |
| 69 | Complete | Composer provenance remains advisory while independently extracted claims are reconciled and audited before release; focused tests prove valid mapping, overreach detection, unmapped suppression and mandatory independent extraction. |

## Gap closure order

1. Versioned contracts, explicit failure taxonomy and DB-backed rollout/budget configuration.
2. Immutable source snapshots, complete cache identity and optimistic authority versioning.
3. Candidate pre-screen and bounded deepening contract with hard fail-closed budgets.
4. Atomic authoritative transition persistence and symmetric invalidation.
5. Output provenance and matter-document lineage.
6. Full telemetry and read-only Blazor diagnostics.
7. Expanded regression, nondeterminism and ablation/evaluation harnesses.

## External acceptance dependency

Item 55 cannot be completed by code alone. The repository can define and validate a versioned ground-truth fixture format, but difficult semantic labels and rationales must be approved by attorneys or qualified legal experts outside the automated system.
