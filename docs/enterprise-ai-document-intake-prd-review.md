# AgencyBinder Enterprise AI Document Intake Platform PRD Review

## Review scope

This review maps the platform requirement set to the implementation in the AMS modular monolith. SQL Server remains authoritative, DMS documents remain legal evidence, AI output remains draft data, and official records are created only through application services after governed review.

Status definitions:

- **Complete**: implemented end to end and locally verifiable.
- **Partial**: core capability exists but an enterprise control or workflow remains incomplete.
- **Missing**: no effective implementation was found.
- **External validation required**: implementation exists but requires deployed Azure resources or operational evidence.

## Compliance matrix

| Capability | Status | Implementation evidence | Gap or required validation |
|---|---|---|---|
| DMS-backed evidence and manual upload | Complete | `EnterpriseDocumentUpload`, `DMS.Document`, `DMS.IntakeSessionDocument`, Submission Documents integration | Continue enforcing upload authorization and malware/content controls through the shared DMS boundary. |
| Tenant-scoped idempotent intake sessions | Complete | `DMS.IntakeSession`, unique tenant/idempotency index, `CreateSessionAsync` | Stable package keys intentionally reopen the existing package session; a new analysis cycle requires governed reprocess rather than duplicate creation. |
| Multi-document package intake | Partial | Multiple evidence links and document-specific OCR work items | Critical context, completion, and search identity defects identified during review are addressed by this hardening pass. |
| SQL-backed durable dispatch | Partial | `DMS.IntakeWorkItem`, attempts, leases, `BackgroundService` worker | Expired `PROCESSING` leases were not reclaimable before this review. Operational queue-depth and age metrics remain required. |
| Retry and dead-letter handling | Partial | Attempt counts, retry schedule, terminal statuses | Retry/dead-letter behavior exists; operator replay UX and alerting remain limited. |
| Azure Document Intelligence OCR | External validation required | Managed identity/API-key adapter, async operation polling, retained raw response | Requires deployed endpoint validation, timeout/load testing, cost monitoring, and service-specific integration tests. |
| Azure OpenAI structured extraction | External validation required | Approved prompt lookup, JSON-schema response format, token/duration capture | Requires deployed GPT-5-compatible deployment validation, content-policy handling tests, and prompt evaluation datasets. |
| Prompt governance and version evidence | Complete baseline | Approved/effective prompt selection, DB-backed evaluation suites/cases/runs/results, threshold-gated approvals, immutable approval evidence, operations UI | Production-quality evaluation datasets must be supplied by authorized domain owners. |
| Raw OCR/AI payload retention | Complete baseline | Private Blob storage, `IntakePayloadGovernance`, retention worker, legal holds, access audit, purge outcomes | Customer-managed key and environment retention-policy validation remain external. |
| AI execution lineage | Complete | Provider/model/prompt/input/output hashes, confidence, duration, token counts in `DMS.AiExecution` | Production dashboards and cost attribution remain operational work. |
| Insurance Knowledge normalization | Complete for current mappings | Worker `KnowledgeDocumentNormalizer` uses the existing concept resolver and persists normalized values/concept IDs | Mapping breadth and resolution quality require domain evaluation datasets. |
| Deterministic validation | Partial | Required Submission fields and low-confidence issues | Rule coverage is currently narrow; module-specific regulatory and business validation needs expansion. |
| Human field review and correction | Complete | Rowversion-protected field decisions and immutable review history | Bulk review and side-by-side source highlighting can improve reviewer efficiency. |
| Issue resolution | Partial | Rowversion-protected issue resolution | Session readiness was not recalculated after final issue resolution before this review; addressed in hardening. |
| Draft-only AI boundary | Complete | AI fields/issues remain in intake tables; no direct production writes from providers or worker | Maintain this boundary for every new module. |
| Governed Submission promotion | Complete | `DocumentIntakeService` requires READY, blocks open errors, builds reviewed draft, calls `SubmissionIntakeService` | Other modules preserve drafts but do not yet have promotion implementations. |
| Promotion idempotency | Complete | `DMS.IntakePromotion`, Submission source idempotency key, repeat-promotion return behavior | Failure recovery for a promotion left in `PROCESSING` should be operationally monitored. |
| Tenant isolation | Partial | Server-derived tenant context and tenant-fenced repositories | Prompt lookup cross-tenant risk identified during review is addressed by this hardening pass. External penetration testing remains required. |
| Permission authorization | Complete for intake APIs | Read/upload/review/reprocess/promote/admin policies and seeded tenant permissions | Shared DMS upload authorization must remain aligned with intake upload permissions. |
| Submission workspace integration | Complete | Manual upload, package/document analysis, session history, per-document status and review links | Optional polling/notifications can improve long-running processing UX. |
| Intake center, review, exceptions, history | Complete at baseline | Blazor routes, queues, detail/review workspace | Advanced assignment/SLA bulk operations remain roadmap items. |
| Search indexing | Partial | Azure AI Search adapter and pipeline stage | Multi-document records previously overwrote by session ID; addressed in hardening. Search schema/deployment validation is external. |
| Cancellation | Partial | Session and pending work cancellation | Leased work could still complete after cancellation before this review; addressed in hardening. Provider calls cannot be remotely revoked once accepted by Azure. |
| Auditability | Complete baseline | Review history, work attempts, AI execution lineage, payload access audit, replay history, prompt approval evidence | Broader enterprise SIEM export depends on OTLP/Azure Monitor configuration. |
| Observability | Complete baseline | OpenTelemetry activities/meters, OTLP export, structured logs, telemetry snapshots, DB-backed SLOs, persisted alerts, operations dashboard | Configure collector/Azure Monitor routing and production notification rules per environment. |
| Health and readiness | Complete baseline | `/health/ready` validates SQL, Blob, effective approved prompts, Azure provider/Search configuration, and malware settings | Active billable synthetic AI calls are intentionally excluded; validate deployed endpoints during release testing. |
| Retention, privacy, and compliance | Partial/External | Automated retention, legal holds, access audit, and purge workflow are implemented | Data residency, PII redaction, DSAR procedures, CMK policy, and regulator-specific retention remain environment/governance work. |
| Malware/content safety | Complete baseline/External | Defender for Storage tag polling, persisted scan evidence, quarantine state, fail-closed queue gate | Defender for Storage malware scanning must be enabled and authorized in Azure. |
| Disaster recovery and replay | Partial | Durable queue, expired-lease recovery, admin replay, rowversion, replay limits/history | Backup/restore, regional failover, and operational runbooks remain external. |
| Automated testing | Complete baseline | Contract/structured-output tests plus real SQL concurrent leasing and worker-crash recovery tests | SQL tests require `AMS_TEST_SQL_CONNECTION`; Azure adapter and browser tests still require deployed test resources. |

## Confirmed critical findings

1. Expired `PROCESSING` leases were not eligible for leasing, allowing a worker crash to strand work permanently.
2. OCR execution lookup was session-wide and could supply one document's OCR payload to another document's interpretation stage.
3. Approved prompt lookup did not restrict prompt ownership to the current tenant or global prompts.
4. A single document's final stage could mark a multi-document session review-ready before sibling documents completed.
5. Cancellation only cancelled pending/retry rows; leased work could subsequently complete and advance a cancelled session.
6. Resolving the final blocking issue did not recalculate the session to `READY`.
7. Azure AI Search document IDs used only the intake session ID, so multiple documents in one package overwrote each other.

## Hardening completed during this review

- Expired processing leases are recovered into retry or dead-letter states, with the expired attempt closed and preserved.
- Leasing is restricted to active queued/processing sessions, and processing context requires an unexpired lease.
- Cancellation transactionally closes active attempts and cancels pending, processing, retry, failed, and dead-letter work.
- OCR context is document-specific for multi-document packages.
- Approved prompts are restricted to the current tenant or global ownership, with tenant overrides preferred.
- Deleted DMS evidence is excluded from processing context.
- Session review readiness is deferred until all sibling document work completes.
- Resolving an issue recalculates session status and warning/error counts transactionally.
- Azure AI Search uses a stable session-plus-document identity and retains the session ID separately.
- Production settings are synchronized idempotently through `Core.ConfigurationSetting`, preserving existing values and tenant overrides.
- OpenTelemetry tracing/metrics, readiness health, telemetry snapshots, SLO incidents, and an operations dashboard are implemented.
- Defender malware scan polling and fail-closed queue gating are implemented.
- Raw payload retention, purge, legal holds, and immutable access audit are implemented.
- Dead-letter operator replay is rowversion-protected, limited by DB settings, and audited.
- Prompt evaluation and passing-run approval gates are implemented with DB-backed suites and cases.

## Prioritized residual roadmap

### P0 — production safety

- Validate the completed critical hardening against a real SQL Server under concurrent worker load.
- Add SQL Server integration tests for lease expiry, concurrent workers, cancellation races, and multi-document ordering.
- Add immutable administrative audit events for create, attach, queue, reprocess, cancel, and promote.
- Confirm shared DMS upload malware/quarantine controls before production use.

### P1 — enterprise operations

- Add OpenTelemetry spans and metrics for queue depth, oldest item age, stage duration, retries, dead letters, provider throttling, token consumption, and cost.
- Add health checks for Blob, Document Intelligence, Azure OpenAI, Azure AI Search, SQL queue access, and approved prompts.
- Add alerting and operator replay/dead-letter tooling.
- Implement raw payload retention, purge, legal hold, and access-audit policies.

### P2 — workflow breadth and quality

- Add promotion application services for supported non-Submission modules.
- Expand deterministic validation and Knowledge mappings by module and jurisdiction.
- Add prompt evaluation datasets, approval workflow, regression thresholds, and model/version rollout controls.
- Add reviewer bulk actions, source highlighting, assignment SLAs, notifications, and processing auto-refresh.

## Validation evidence

The review is source-based and local-build/test based. Azure runtime behavior, networking, managed identity/RBAC, private endpoints, model deployment compatibility, search schema, capacity, cost, and disaster recovery require environment-level validation before production approval.
