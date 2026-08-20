# Insurance Knowledge Center

## Ownership and boundaries

The Insurance Knowledge Center is an in-process bounded context in the AMS modular monolith. It owns the `knowledge` SQL schema and these projects:

- `Ams.Knowledge.Contracts`: stable semantic resolution, hierarchy, mapping, validation, and integration-event contracts.
- `Ams.Knowledge.Domain`: concept, terminology, hierarchy, mapping, governance, versioning, and publication invariants.
- `Ams.Knowledge.Application`: commands, queries, repository ports, deterministic resolution, relational validation, and administration orchestration.
- `Ams.Knowledge.Infrastructure`: Dapper persistence, SQL migrations, tenant isolation, audit/outbox writes, and durable background processing.

`Ams.Api`, `Ams.Web`, and `Ams.Worker` are module hosts. Core AMS projects do not depend on Knowledge infrastructure, and Blazor never accesses Knowledge SQL directly.

## Migration order

Knowledge migrations run after core AMS migrations during API and Worker startup. `KnowledgeDatabaseMigrator` acquires the SQL application lock `Ams.Knowledge.DatabaseMigrator`, records completed scripts in `knowledge.__Migrations`, and executes embedded resources in ordinal filename order.

1. `0001_InsuranceSemanticLayerFoundation.sql` creates the schema, tables, indexes, operational configuration, permissions, worker leases, and synchronized lookup values.
2. `0002_InsuranceSemanticLayerMvpCatalog.sql` synchronizes governed schemes, concepts, labels, versions, and hierarchy closure data.
3. `0003_ImportStagingLifecycle.sql` adds the governed staged import lifecycle.
4. `0004_EnterpriseInsuranceCatalog.sql` expands the synthetic U.S. P&C, benefits, and life reference catalog and provisions overlays for existing demonstration tenants.

Migration scripts are idempotent. New migrations must be append-only, use the next zero-padded number, and preserve existing identifiers and published versions.

## Enterprise reference catalog

The enterprise catalog provides AMS-authored synthetic/reference terminology for products and lines of business, coverages, perils, policy and claim lifecycles, underwriting, accounting, billing, commissions, distribution, service requests, documents, parties, employee benefits, life insurance, and annuities. It includes searchable preferred labels and common abbreviations, governed snapshots, cross-scheme relationships, validation rules, a publication snapshot, and hierarchy closure.

This catalog is broad seed data for demonstrations, integration testing, search, and tenant configuration. It is not a licensed copy of ACORD, ISO, carrier, competitor AMS, rates, forms, underwriting manuals, or state filing content. External authority alignments must be added only from verified, licensed sources and must retain their provenance and licensing notes.

The migration discovers active `DEMO` and `ENT-*` tenants from `Core.Tenant`; it never creates tenants. Those tenants receive deterministic semantic settings and synthetic `LEGACY_AMS` mappings. Production tenant extensions and carrier mappings must be created through governed tenant workflows rather than added to the global catalog.

## Tenant and data rules

- System-defined concepts and schemes use `TenantId = NULL` and are visible to all tenants.
- Tenant extensions use a non-null `TenantId` and are only returned for the authenticated tenant.
- External mappings, reviews, imports, audit reads, and operational work are tenant-specific.
- API controllers derive tenant and actor identifiers from authenticated claims and overwrite client-supplied context.
- Published concepts are immutable; material changes require a new version.
- SQL rowversion values protect draft updates, review decisions, and publication operations.

## Permissions

- `Knowledge.Concepts.Read`
- `Knowledge.Concepts.Manage`
- `Knowledge.Mappings.Read`
- `Knowledge.Mappings.Manage`
- `Knowledge.Mappings.Approve`
- `Knowledge.Rules.Manage`
- `Knowledge.Publish`
- `Knowledge.Import`
- `Knowledge.Audit.Read`

Navigation visibility, Blazor routes, and API endpoints enforce permissions independently. `SYSTEM_ADMIN`, `TENANT_ADMIN`, and `NAV_ALL` retain administrative access under the host authorization conventions.

## Background operations

`KnowledgeWorkerService` is a `BackgroundService` hosted by the existing `Ams.Worker` executable. Polling, batch size, retry count, and lease duration are database-backed values in `knowledge.Configuration`.

The processor:

- leases queued/retry imports and semantic outbox messages with `UPDLOCK`, `READPAST`, and lease expiration;
- resolves only relative storage references beneath `Knowledge:ImportRootPath` and stages import JSON idempotently using the `(ImportJobId, RecordNumber)` uniqueness constraint;
- leaves successfully parsed imports in `STAGED`; a governed validation/apply pipeline must transition them to a completed state;
- rebuilds relational hierarchy closure after concept or hierarchy changes;
- invalidates tenant semantic policy caches after relevant concept/mapping changes;
- preserves correlation identifiers and persisted retry/error state;
- applies exponential retry delay and moves exhausted outbox work to `DEAD_LETTER`;
- reclaims expired processing leases after process interruption and fences completion/failure updates by lease owner.

The hierarchy closure replacement is transactional and serialized with the `Ams.Knowledge.HierarchyRebuild` SQL application lock.

Core AMS operations do not depend on the worker completing. Semantic enrichment remains advisory and fails open for core workflows.

## Deployment checks

1. Configure `ConnectionStrings:DefaultConnection` for API and Worker.
2. Deploy API and Worker binaries containing the same Knowledge migration assembly version.
3. Start one host and verify `knowledge.__Migrations` contains both current scripts.
4. Verify the permission seeds are assigned to the intended tenant roles.
5. Configure `Knowledge:ImportRootPath` and confirm the Worker can access only intended relative import storage references.
6. Monitor failed `knowledge.ImportJob` rows and `DEAD_LETTER` semantic outbox messages.
7. Validate `/health`, `/api/knowledge/dashboard`, and the `/admin/knowledge` permission boundary.

## Stage 2 extension seams

The following interfaces are the supported replacement or extraction boundaries:

- `IConceptResolver` and `IConceptResolutionRepository` for additional deterministic indexes or an optional search service.
- `IKnowledgeHierarchyService` and `IKnowledgeHierarchyRepository` for optional graph projection while SQL remains authoritative.
- `IExternalMappingService` and `IExternalMappingRepository` for carrier-specific adapters.
- `IKnowledgeValidationService`, `IKnowledgeValidationRuleRepository`, and `ISemanticRuleEvaluator` for richer relational rules.
- Knowledge integration events and `knowledge.SemanticOutboxMessage` for external subscribers.
- API contracts under `api/knowledge` for eventual process extraction without changing Blazor callers.

LLM suggestions, RDF/OWL export, and graph databases are deferred. They may propose or project semantic data but must not bypass approval, audit, versioning, tenant isolation, or the authoritative SQL model.

## Known governance follow-up

The relational foundation includes change requests/approvals, rule metadata, concept versions, semantic tags, publication items, and import validation errors. Full administration workflows for creating and approving those records, assembling publications, applying staged imports, and managing rules/tags are not yet exposed. Until those workflows are implemented, imports must remain `STAGED` and publication rows must be provisioned through a governed database process.
