using Legal.Application;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Intelligence.Epistemic;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;
using Legal.Infrastructure.Configuration;
using Legal.Infrastructure.Intelligence;
using Legal.Infrastructure.Persistence;
using Legal.Infrastructure.Persistence.ConnectionFactory;
using Legal.Infrastructure.Persistence.Repositories;
using Legal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Legal.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLegalInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Ensure Dapper can map SQL Server date/time columns (returned as DateTime/TimeSpan)
        // into DTO DateOnly?/TimeOnly? properties (e.g. PI profile child aggregates).
        DapperTypeHandlers.EnsureRegistered();

        services.Configure<SqlOptions>(options =>
        {
            options.ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        });
        services.AddOptions<DocumentIntelligenceOptions>()
            .Bind(configuration.GetSection(DocumentIntelligenceOptions.SectionName))
            .Validate(options => options.ModelId.Equals("prebuilt-layout", StringComparison.OrdinalIgnoreCase),
                "DocumentIntelligence:ModelId must be prebuilt-layout for legal-document ingestion.")
            .Validate(options => options.MaximumFileSizeBytes > 0, "DocumentIntelligence:MaximumFileSizeBytes must be positive.")
            .Validate(options => options.NativeTextMinimumCharactersPerPage > 0,
                "DocumentIntelligence:NativeTextMinimumCharactersPerPage must be positive.")
            .Validate(options => options.NativeTextMinimumReadableCharacterRatio is > 0 and <= 1,
                "DocumentIntelligence:NativeTextMinimumReadableCharacterRatio must be greater than zero and no greater than one.")
            .Validate(options => options.BinaryStoreProvider.Equals("FileSystem", StringComparison.OrdinalIgnoreCase) ||
                                 !string.IsNullOrWhiteSpace(options.BlobConnectionString) || Uri.TryCreate(options.BlobServiceUri, UriKind.Absolute, out _),
                "Azure Blob storage requires DocumentIntelligence:BlobConnectionString or BlobServiceUri.")
            .Validate(options => !options.BinaryStoreProvider.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase) || options.BlobRetentionDays > 0,
                "DocumentIntelligence:BlobRetentionDays must be positive for Azure Blob storage.")
            .Validate(options => !options.MalwareScannerProvider.Equals("DefenderForStorage", StringComparison.OrdinalIgnoreCase) ||
                                 !string.IsNullOrWhiteSpace(options.BlobConnectionString) || Uri.TryCreate(options.BlobServiceUri, UriKind.Absolute, out _),
                "Defender for Storage requires DocumentIntelligence:BlobConnectionString or BlobServiceUri.")
            .Validate(options => !options.MalwareScannerProvider.Equals("DefenderForStorage", StringComparison.OrdinalIgnoreCase) ||
                                 (options.DefenderScanTimeoutSeconds > 0 && options.DefenderScanPollSeconds > 0),
                "Defender scan timeout and poll interval must be positive.")
            .Validate(options => !options.SearchProjectionEnabled ||
                                 (Uri.TryCreate(options.SearchEndpoint, UriKind.Absolute, out _) && !string.IsNullOrWhiteSpace(options.SearchIndexName)),
                "Enabled Azure AI Search projection requires SearchEndpoint and SearchIndexName.")
            .ValidateOnStart();
        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddSingleton<LegalDatabaseMigrator>();

        services.AddScoped<IIntelligenceRepository, IntelligenceRepository>();
        services.AddScoped<IIntelligenceWideRepository, IntelligenceWideRepository>();
        services.AddScoped<IIntelligenceWide2Repository, IntelligenceWide2Repository>();
        services.AddScoped<IMathReasoningRepository, MathReasoningRepository>();
        services.AddScoped<IAiProviderRouteRepository, AiProviderRouteRepository>();
        services.AddScoped<IEpistemicClaimRepository, EpistemicClaimRepository>();
        services.AddScoped<IDecisionGovernanceRepository, DecisionGovernanceRepository>();
        services.AddScoped<IDecisionSupportSignalRepository, DecisionSupportSignalRepository>();
        services.AddScoped<ILegalDocumentCorpusRepository, LegalDocumentCorpusRepository>();
        services.AddScoped<IProviderPortalRepository, ProviderPortalRepository>();
        services.AddScoped<IAttorneyDecisionInputRepository, AttorneyDecisionInputRepository>();
        services.AddScoped<ILegalDecisionContractRepository, LegalDecisionContractRepository>();
        services.AddScoped<ILegalHierarchyExecutionRepository, LegalHierarchyExecutionRepository>();

        // Continuous Decision Integrity (Phase 1): change-awareness persistence + Matter Change Processor.
        services.AddScoped<IDecisionIntegrityRepository, LegalDecisionIntegrityRepository>();

        // Decision Channels: qualitative source-truth contributions bound to authoritative hierarchy nodes.
        services.AddScoped<IChannelContributionRepository, LegalChannelContributionRepository>();
        // Durable Hierarchy Node → Decision lineage map (migration 0368): the node→branch/candidate source-of-truth.
        services.AddScoped<IHierarchyNodeDecisionLineageRepository, HierarchyNodeDecisionLineageRepository>();

        // Enterprise error-log store: scoped Dapper repository plus a fail-soft service used by every
        // pipeline module (API request filter, Wide2 decision pipeline, retrievers, workers). The service
        // is a singleton so singleton pipeline components can capture errors; it opens its own DI scope
        // per write to reach the scoped repository/connection.
        services.AddScoped<IErrorLogRepository, ErrorLogRepository>();
        services.AddSingleton<IErrorLogService, ErrorLogService>();

        // POLOXI Epistemic Authority Layer (EA-1/EA-2): deterministic, stateless governance services.
        // Settings are DB-backed and runtime-effective: resolved per scope from Core.ConfigurationSetting
        // (tenant override -> platform default -> code default). A default tenant accessor returns null so
        // non-HTTP scopes (workers, migrations, tests) safely fall back to platform/code defaults; hosts
        // register an IEpistemicTenantAccessor that reads the authenticated tenant.
        services.TryAddScoped<IEpistemicTenantAccessor, NullEpistemicTenantAccessor>();
        services.AddScoped(sp =>
        {
            var tenantId = sp.GetRequiredService<IEpistemicTenantAccessor>().TenantId;
            if (tenantId is null || tenantId == Guid.Empty)
                return new EpistemicAuthoritySettings();
            var repo = sp.GetRequiredService<IIntelligenceWideRepository>();
            return repo.ResolveEpistemicSettingsAsync(tenantId.Value).GetAwaiter().GetResult();
        });
        services.AddSingleton<IClaimAuthorityGate, ClaimAuthorityGate>();
        services.AddSingleton<IClaimIdentityResolver, ClaimIdentityResolver>();
        services.AddSingleton<IClaimVerificationPrioritizer, ClaimVerificationPrioritizer>();

        // EA→Decision signal mapper (§12/§13): projects governed claim authority into domain-neutral
        // DecisionBranchSignal deltas for Candidate × Branch recompetition. Pure/deterministic.
        services.AddSingleton<IEpistemicDecisionSignalMapper, EpistemicDecisionSignalMapper>();

        // POLOXI Verified Decision Signals: deterministic projection of verified support signals into
        // domain-neutral DecisionBranchSignal deltas. Pure/stateless; reuses the recompetition engine.
        services.AddSingleton<IVerifiedDecisionSignalService, VerifiedDecisionSignalService>();

        // Material-signal extraction behind an interface (deterministic graph-based default; an
        // LLM-backed extractor can replace it later without touching the orchestrator).
        services.AddSingleton<IMaterialSignalExtractor, GraphMaterialSignalExtractor>();

        // EA-3: material-claim verification bridge into the V2.1 dependency-propagation loop (scoped:
        // depends on the scoped IEpistemicClaimRepository).
        services.AddScoped<IMaterialClaimVerificationService, MaterialClaimVerificationService>();

        // EA-4: closed-loop orchestrator (verify -> propagate -> research need). Scoped: composes the
        // scoped verification service.
        services.AddScoped<IEpistemicClosedLoopOrchestrator, EpistemicClosedLoopOrchestrator>();

        // EA-5: readiness blocking + output claim audit. Scoped: read authoritative claims through the
        // scoped IEpistemicClaimRepository.
        services.AddScoped<IDecisionReadinessEvaluator, DecisionReadinessEvaluator>();
        services.AddScoped<IOutputClaimAuditor, OutputClaimAuditor>();
        services.AddSingleton<IClaimExtractor, DeterministicOutputClaimExtractor>();
        services.AddSingleton<IOutputClaimProvenanceReconciler, OutputClaimProvenanceReconciler>();

        // EA-6: advisory bridge that projects the live V2 decision graph into authoritative EA claims
        // and runs readiness/output governance. Scoped: composes the scoped EA services.
        services.AddScoped<IEpistemicDecisionBridge, EpistemicDecisionBridge>();

        // Matter-proposition Information Value: advisory overlay that scores atomic matter fact
        // propositions on POLOXI's shared VIV scale by reusing the ClaimVerificationPrioritizer. Scoped:
        // composes the scoped corpus + epistemic-claim repositories.
        services.AddScoped<IMatterPropositionInformationValueService, MatterPropositionInformationValueService>();

        // Next Best Action: thin advisory composition over the proposition VIV frontier + evidence graph +
        // Domain Pack. POLOXI selects what matters, HRR supplies where/why, the Domain Pack supplies domain
        // actions, an LLM proposes and a deterministic gate selects. Never recomputes IV/ADV; fails soft.
        services.AddScoped<INextBestActionService, NextBestActionService>();

        // What To Resolve Next: a thin projection over the SAME proposition VIV / LegalADV frontier. It
        // surfaces ranked unresolved decision-material propositions with four explicit states and never
        // recomputes scores or generates an operational action (that is NBA's job). Fails soft to Blocked.
        services.AddScoped<IWhatToResolveNextService, WhatToResolveNextService>();

        // Attorney Decision Input (Human Intelligence) write path: the orchestration service and the
        // thin non-committing POLOXI preview adapter (§13/§15/§19). POLOXI remains the authoritative evaluator.
        services.AddScoped<IExistingPoloxiEvaluationAdapter, ExistingPoloxiEvaluationAdapter>();
        services.AddScoped<IAttorneyDecisionInputService, AttorneyDecisionInputService>();

        // LPI (Legal Proposition Intelligence) OPTIONAL ancestor-informed score INITIALIZER. Pure and
        // advisory; ancestor influence is DISABLED by default so the existing local-only initialization
        // is preserved until regression testing validates the blended path. POLOXI Core still owns the
        // authoritative recompute; this only initializes a candidate-specific starting score.
        var lpiOptions = new LpiScoreInitializerOptions();
        configuration.GetSection(LpiScoreInitializerOptions.SectionName).Bind(lpiOptions);
        services.AddSingleton(lpiOptions);
        services.AddScoped<ILpiScoreInitializer, LpiScoreInitializer>();

        // LPI Document-Retrieval proposition integration (Phase 2): the ONE shared insertion funnel used
        // by both the manual ADI path and the retrieval path. The service validates, optionally LPI-
        // initializes (CONTEXT_ONLY excluded), and atomically commits the proposition + placements +
        // change event + outbox reassessment. POLOXI Core still owns all scoring.
        services.AddScoped<ILpiPropositionIntegrationRepository, LpiPropositionIntegrationRepository>();
        services.AddScoped<IPropositionIntegrationService, Legal.Application.Features.Intelligence.Decision.Lpi.PropositionIntegrationService>();

        // Document-Retrieval sending half: DECISION_EXTRACTION_V1 extraction (parks for review) and the
        // attorney review/accept service that routes accepted propositions through the SHARED funnel above.
        services.AddScoped<IRetrievalPropositionExtractionService, Legal.Application.Features.Intelligence.Decision.Lpi.RetrievalPropositionExtractionService>();
        services.AddScoped<IRetrievalPropositionReviewService, Legal.Application.Features.Intelligence.Decision.Lpi.RetrievalPropositionReviewService>();

        // Document-Retrieval orchestration: condition-directed / document-directed passage fan-out that
        // builds the authoritative hierarchy context and invokes extraction per passage. Proposal-only —
        // it selects passages and parks propositions; it never scores candidates.
        services.AddScoped<IRetrievalOrchestrationService, Legal.Application.Features.Intelligence.Decision.Lpi.RetrievalOrchestrationService>();

        // Legal-Authority orchestration: matches VERIFIED legal-authority evidence to authoritative
        // hierarchy nodes and parks each as a proposition for attorney review. Proposal-only \u2014 it never
        // scores candidates; acceptance flows through the shared LPI integration funnel.
        services.AddScoped<ILegalAuthorityOrchestrationService, Legal.Application.Features.Intelligence.Decision.Lpi.LegalAuthorityOrchestrationService>();

        // Authoritative, server-side source of the decision identity revisions (contract/candidate/
        // hierarchy) and scoring-config version that every retrieved-proposition lifecycle operation
        // must carry. The UI never supplies these; this resolver loads them from POLOXI Core state.
        services.AddScoped<IDecisionRevisionResolver, Legal.Application.Features.Intelligence.Decision.Lpi.DecisionRevisionResolver>();

        // Decision Contract: first-class, versioned, DB-backed problem-specification workspace.
        services.AddScoped<ILegalDecisionContractService, LegalDecisionContractService>();


        services.AddScoped<IPromptCatalog, PromptCatalog>();
        services.AddScoped<IAiProviderRouter, AiProviderRouter>();
        // Disable the HttpClient-level timeout (default 100s) so the per-request timeout defined by the
        // route policy (AzureOpenAiProvider.CreateTimeout, up to 900s for reasoning models such as
        // gpt-5.6-sol) governs cancellation instead of prematurely aborting long reasoning completions.
        services.AddHttpClient<IAiProvider, AzureOpenAiProvider>(client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddHttpClient<IExternalKnowledgeProvider, TavilyExternalKnowledgeProvider>();

        // Legal-context grounding sources (selected when the LEGAL search context is used).
        services.AddHttpClient<ICourtListenerLegalSource, CourtListenerLegalRetriever>();
        services.AddHttpClient<IGovInfoLegalSource, GovInfoLegalRetriever>();
        services.AddHttpClient<ICornellLiiLegalSource, CornellLiiLegalRetriever>();
        services.AddSingleton<ILegalJurisdictionDetector, LegalJurisdictionDetector>();
        services.AddScoped<ILegalAuthoritySourceRegistry, LegalAuthoritySourceRegistry>();
        services.AddScoped<ILegalAuthorityDiscoveryConfiguration, LegalAuthorityDiscoveryConfiguration>();
        services.AddHttpClient<ILegalAuthoritySourceBootstrapper, LegalAuthoritySourceBootstrapper>();
        // Official statutory-authority hosts (Justia/FindLaw/etc.) sit behind bot-mitigation WAFs that
        // reject bare server requests with HTTP 403. Egress hardening (Option 1): (a) enable automatic
        // gzip/deflate/brotli decompression so the Accept-Encoding header the retriever advertises is
        // honest, and (b) route through an OPTIONAL forward proxy when one is configured
        // (Legal:OfficialAuthority:Proxy), so the app can egress from an allowlisted/non-blocked network
        // without any code change. When no proxy is configured the default egress is used unchanged.
        services.AddHttpClient<IOfficialLegalAuthoritySource, OfficialLegalAuthorityRetriever>()
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new System.Net.Http.HttpClientHandler
                {
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip
                        | System.Net.DecompressionMethods.Deflate
                        | System.Net.DecompressionMethods.Brotli,
                    // Session hardening: several official statutory hosts (e.g. California leginfo, a JSF app)
                    // now reject a "cold" GET to a section page that arrives without a JSESSIONID / WAF cookie
                    // first issued by visiting the site root. Enabling a shared cookie jar lets the retriever's
                    // per-host warm-up navigation collect those cookies and replay them on the document GET.
                    UseCookies = true,
                    CookieContainer = new System.Net.CookieContainer(),
                    AllowAutoRedirect = true,
                };
                var proxyUrl = configuration["Legal:OfficialAuthority:Proxy"];
                if (!string.IsNullOrWhiteSpace(proxyUrl))
                {
                    handler.Proxy = new System.Net.WebProxy(proxyUrl);
                    handler.UseProxy = true;
                }
                return handler;
            });
        services.AddScoped<ILegalRetriever, LegalRetriever>();
        services.AddSingleton<ILegalResearchPlanner, LegalResearchPlanner>();
        services.AddScoped<ILegalAuthorityRetrievalService, LegalAuthorityRetrievalService>();
        services.AddSingleton<IWebSourceInspector, PlaywrightWebSourceInspector>();
        services.AddSingleton<IEvidenceSourceClassifier, DeterministicEvidenceSourceClassifier>();
        services.AddSingleton<IVerificationProfileProvider, VerificationProfileProvider>();
        services.AddSingleton<IIdentityEvidenceVerifier, PlaywrightIdentityEvidenceVerifier>();
        services.AddSingleton<ICitationEvidenceVerifier, PlaywrightCitationEvidenceVerifier>();
        services.AddSingleton<IPassageEvidenceVerifier, PlaywrightPassageEvidenceVerifier>();
        services.AddSingleton<IVerificationCandidatePreScreen, DeterministicVerificationCandidatePreScreen>();
        services.AddSingleton<ISemanticVerificationCache, SemanticVerificationCache>();
        services.AddScoped<ISemanticEvidenceVerifier, StructuredLlmSemanticEvidenceVerifier>();
        services.AddSingleton<IPoloxiVerificationDeepener, DeterministicPoloxiVerificationDeepener>();
        services.AddSingleton<IAuthorityEvidenceVerifier, DeterministicAuthorityEvidenceVerifier>();
        services.AddSingleton<IEvidenceVerificationAggregator, EvidenceVerificationAggregator>();
        services.AddScoped<IIndependentEvidenceVerificationPipeline, IndependentEvidenceVerificationPipeline>();

        services.AddScoped<IAdaptiveRetriever, StandardPoloxiRetriever>();

        // ABV + ambiguity subsystems (mirrors Ams.Infrastructure registrations).
        services.AddScoped<Legal.Application.Abstractions.Persistence.IIntelligenceAbvRepository>(provider => provider.GetRequiredService<IIntelligenceRepository>() as Legal.Application.Abstractions.Persistence.IIntelligenceAbvRepository
            ?? throw new InvalidOperationException("The intelligence repository must support ABV persistence."));
        services.AddScoped<Legal.Application.Abstractions.Persistence.IIntelligenceAmbiguityRepository>(provider => provider.GetRequiredService<IIntelligenceRepository>() as Legal.Application.Abstractions.Persistence.IIntelligenceAmbiguityRepository
            ?? throw new InvalidOperationException("The intelligence repository must support ambiguity persistence."));
        services.AddSingleton<Legal.Application.Abstractions.Intelligence.IAbvGovernanceEngine, Legal.Application.Features.Intelligence.Abv.AbvGovernanceEngine>();
        services.AddScoped<Legal.Application.Abstractions.Intelligence.IAbvResolutionEngine, Legal.Application.Features.Intelligence.Abv.AbvResolutionEngine>();
        services.AddSingleton<Legal.Application.Abstractions.Intelligence.IQueryComplexityAnalyzer, Legal.Application.Features.Intelligence.Ambiguity.QueryComplexityAnalyzer>();
        services.AddSingleton<Legal.Application.Abstractions.Intelligence.IModelEscalationPolicy, Legal.Application.Features.Intelligence.Ambiguity.ModelEscalationPolicy>();
        services.AddSingleton<Legal.Application.Abstractions.Intelligence.IAmbiguityPromptStrategy, Legal.Application.Features.Intelligence.Ambiguity.LightAmbiguityPromptStrategy>();
        services.AddSingleton<Legal.Application.Abstractions.Intelligence.IAmbiguityPromptStrategy, Legal.Application.Features.Intelligence.Ambiguity.MediumAmbiguityPromptStrategy>();
        services.AddSingleton<Legal.Application.Abstractions.Intelligence.IAmbiguityPromptStrategy, Legal.Application.Features.Intelligence.Ambiguity.HeavyAmbiguityPromptStrategy>();
        services.AddSingleton<Legal.Application.Abstractions.Intelligence.IAmbiguityPromptSelector, Legal.Application.Features.Intelligence.Ambiguity.AmbiguityPromptSelector>();
        services.AddSingleton<Legal.Application.Abstractions.Intelligence.IHierarchyValidator, Legal.Application.Features.Intelligence.Ambiguity.HierarchyValidator>();
        services.AddSingleton<Legal.Application.Abstractions.Intelligence.IInterpretationStitcher, Legal.Application.Features.Intelligence.Ambiguity.InterpretationStitcher>();
        services.AddSingleton<Legal.Application.Abstractions.Intelligence.IAmbiguityNarrowingEngine, Legal.Application.Features.Intelligence.Ambiguity.AmbiguityNarrowingEngine>();
        services.AddScoped<Legal.Application.Abstractions.Intelligence.IAmbiguityModelRouter, Legal.Application.Features.Intelligence.Ambiguity.AmbiguityModelRouter>();
        services.AddScoped<Legal.Application.Abstractions.Intelligence.IAmbiguityResolutionEngine, Legal.Application.Features.Intelligence.Ambiguity.AmbiguityResolutionEngine>();

        services.AddScoped<IIntelligenceWideService, IntelligenceWideService>();
        services.AddScoped<IIntelligenceWide2Service, IntelligenceWide2Service>();

        // POLOXI Scientific Reasoning — Mathematics V1 pack.
        services.AddScoped<IMathReasoningService, MathReasoningService>();

        // POLOXI Formalization Gate (Research → Formalize → Math handoff).
        services.AddScoped<IFormalizationService, FormalizationService>();

        // POLOXI Legal Decision Intelligence (/legal/decision) — self-contained module.
        services.AddScoped<ILegalDecisionRepository, LegalDecisionRepository>();

        // Judz Matter Lifecycle — reusable operational stage engine (separate from POLOXI Core).
        services.AddScoped<Legal.Application.Abstractions.Persistence.IMatterLifecycleRepository, MatterLifecycleRepository>();
        services.AddScoped<Legal.Application.Abstractions.Services.IMatterLifecycleService, Legal.Application.MatterLifecycleService>();
        services.AddScoped<ILegalDecisionAiProvider, LegalDecisionAiProvider>();
        services.AddScoped<ILegalDecisionRetriever, LegalDecisionRetriever>();
        services.AddScoped<IDocumentExtractionProvider, AzureDocumentIntelligenceProvider>();
        services.AddSingleton<ILegalDocumentIntakeValidator, LegalDocumentIntakeValidator>();
        if (configuration[$"{DocumentIntelligenceOptions.SectionName}:BinaryStoreProvider"]?.Equals("FileSystem", StringComparison.OrdinalIgnoreCase) == true)
            services.AddSingleton<ILegalDocumentBinaryStore, FileSystemLegalDocumentBinaryStore>();
        else
            services.AddSingleton<ILegalDocumentBinaryStore, AzureBlobLegalDocumentBinaryStore>();
        if (configuration[$"{DocumentIntelligenceOptions.SectionName}:MalwareScannerProvider"]?.Equals("Http", StringComparison.OrdinalIgnoreCase) == true)
            services.AddHttpClient<ILegalDocumentSecurityScanner, HttpLegalDocumentSecurityScanner>();
        else if (configuration[$"{DocumentIntelligenceOptions.SectionName}:MalwareScannerProvider"]?.Equals("DefenderForStorage", StringComparison.OrdinalIgnoreCase) == true)
            services.AddSingleton<ILegalDocumentSecurityScanner, AzureDefenderLegalDocumentSecurityScanner>();
        else
            services.AddSingleton<ILegalDocumentSecurityScanner, DisabledLegalDocumentSecurityScanner>();
        services.AddSingleton<INativeDocumentTextProvider, NativeDocumentTextProvider>();
        services.AddScoped<ILegalDocumentExtractionRouter, Legal.Application.Features.Intelligence.Decision.LegalDocumentExtractionRouter>();

        // ── Media & Machine Evidence channel (Phase 3, Slice 1) ──────────────────────────────────────
        // Private immutable media byte store (separate from the document corpus). Provider selected the
        // same way as the document binary store; SQL carries only the opaque storage key.
        services.AddScoped<Legal.Application.Abstractions.Persistence.IMediaEvidenceRepository, Legal.Infrastructure.Persistence.Repositories.MediaEvidenceRepository>();
        if (configuration[$"{DocumentIntelligenceOptions.SectionName}:BinaryStoreProvider"]?.Equals("FileSystem", StringComparison.OrdinalIgnoreCase) == true)
            services.AddSingleton<Legal.Application.Abstractions.Intelligence.IMediaBinaryStore, Legal.Infrastructure.Intelligence.FileSystemMediaBinaryStore>();
        else
            services.AddSingleton<Legal.Application.Abstractions.Intelligence.IMediaBinaryStore, Legal.Infrastructure.Intelligence.AzureBlobMediaBinaryStore>();

        // Capability-aware PHOTO/AUDIO processors (typed HttpClients). Unavailable config → manual fallback.
        services.AddHttpClient<Legal.Application.Abstractions.Intelligence.IMediaImageAnalyzer, Legal.Infrastructure.Services.AzureOpenAiMediaImageAnalyzer>();
        services.AddHttpClient<Legal.Application.Abstractions.Intelligence.IMediaAudioTranscriber, Legal.Infrastructure.Services.AzureOpenAiMediaAudioTranscriber>();

        // Media orchestration: processes assets, anchors proposals, and parks them through the SHARED LPI
        // funnel. POLOXI Core still owns all scoring.
        services.AddScoped<Legal.Application.Abstractions.Intelligence.IMediaEvidenceOrchestrationService, Legal.Application.Features.Intelligence.Decision.Media.MediaEvidenceOrchestrationService>();
        services.AddScoped<ILegalDocumentSearchProjectionDispatcher, LegalDocumentSearchProjectionDispatcher>();
        services.AddScoped<ILegalDocumentSemanticInterpreter, Legal.Application.Features.Intelligence.Decision.LegalDocumentSemanticInterpreter>();
        services.AddScoped<Legal.Application.Abstractions.Intelligence.IDomainPackResolver, Legal.Application.Features.Intelligence.Decision.DomainPackResolver>();
        services.AddScoped<ILegalDocumentIntakeService, Legal.Application.Features.Intelligence.Decision.LegalDocumentIntakeService>();
        services.AddScoped<Legal.Application.Abstractions.Intelligence.ILegalMatterCorpusActivationService, Legal.Application.Features.Intelligence.Decision.LegalMatterCorpusActivationService>();
        services.AddScoped<Legal.Application.Abstractions.Intelligence.IMatterChangeProcessor, Legal.Application.Features.Intelligence.Decision.MatterChangeProcessor>();
        services.AddScoped<Legal.Application.Features.Intelligence.Decision.DecisionReevaluationService>();

        // Decision Channels: each channel emits qualitative contributions; POLOXI Wide2 owns the outcome.
        services.AddScoped<Legal.Application.Features.Intelligence.Decision.Channels.IDecisionChannel, Legal.Application.Features.Intelligence.Decision.Channels.DocumentEvidenceChannel>();
        services.AddScoped<Legal.Application.Features.Intelligence.Decision.Channels.IDecisionChannel, Legal.Application.Features.Intelligence.Decision.Channels.HumanIntelligenceChannel>();
        services.AddScoped<Legal.Application.Features.Intelligence.Decision.Channels.IDecisionChannel, Legal.Application.Features.Intelligence.Decision.Channels.LegalAuthorityChannel>();
        services.AddScoped<Legal.Application.Features.Intelligence.Decision.Channels.IDecisionChannel, Legal.Application.Features.Intelligence.Decision.Channels.InvestigationChannel>();
        services.AddScoped<Legal.Application.Features.Intelligence.Decision.Channels.IDecisionChannel, Legal.Application.Features.Intelligence.Decision.Channels.DecisionContractChannel>();
        services.AddScoped<Legal.Application.Features.Intelligence.Decision.Channels.IDecisionChannel, Legal.Application.Features.Intelligence.Decision.Channels.ExternalResearchChannel>();
        services.AddScoped<Legal.Application.Features.Intelligence.Decision.Channels.IDecisionChannelOrchestrator, Legal.Application.Features.Intelligence.Decision.Channels.DecisionChannelOrchestrator>();
        // Channel → typed recompetition bridge: rehydrates persisted contributions, prefetches durable
        // node lineage (0368), and runs the pure LegalChannelSignalAdapter to emit typed DecisionBranchSignals.
        services.AddScoped<Legal.Application.Features.Intelligence.Decision.Channels.IChannelContributionProjectionService, Legal.Application.Features.Intelligence.Decision.Channels.ChannelContributionProjectionService>();
        // Read-only Channel Scoring LPI trace: explains how each verified channel contribution's typed δ
        // affected the POLOXI candidate competition. Reuses the same contributions + lineage; no new scoring.
        services.AddScoped<Legal.Application.Features.Intelligence.Decision.Channels.IChannelScoringLpiService, Legal.Application.Features.Intelligence.Decision.Channels.ChannelScoringLpiService>();
        services.AddScoped<IDecisionReevaluationDispatcher, DecisionReevaluationDispatcher>();
        services.AddScoped<ILegalMatterContextRetriever, Legal.Application.Features.Intelligence.Decision.LegalMatterContextRetriever>();
        services.AddSingleton<IDecisionResearchSourceRouter, Legal.Application.Features.Intelligence.Decision.DecisionResearchSourceRouter>();
        services.AddScoped<Legal.Application.Features.Intelligence.Decision.Core.IDependencyPropagationService, Legal.Application.Features.Intelligence.Decision.Core.DependencyPropagationService>();
        services.AddScoped<Legal.Application.Features.Intelligence.Decision.Core.ILegalDecisionImpactMapper, Legal.Application.Features.Intelligence.Decision.Core.LegalDecisionImpactMapper>();
        services.AddScoped<ILegalDecisionService, LegalDecisionService>();

        // ── Judz.ai Early Access SaaS layer
        // ASP.NET Core Identity EF Core store DbContext over the AspNet* tables (migration 0246).
        // The Identity builder (AddIdentityCore/token providers/sign-in) is configured by the API host
        // which has the ASP.NET Core framework reference; here we only register the store DbContext.
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        services.AddDbContext<Legal.Infrastructure.Identity.JudzIdentityDbContext>(options =>
            options.UseSqlServer(connectionString));

        // SaaS data access + provider-agnostic services.
        services.AddScoped<ISaasRepository, Legal.Infrastructure.Persistence.Repositories.SaasRepository>();
        services.AddScoped<IJudzEmailSender, Legal.Infrastructure.Identity.SmtpEmailSender>();
        services.AddScoped<IEmailVerificationService, Legal.Application.Features.Saas.EmailVerificationService>();
        services.AddScoped<IWorkspaceProvisioningService, Legal.Application.Features.Saas.WorkspaceProvisioningService>();
        services.AddScoped<ITenantContextService, Legal.Application.Features.Saas.TenantContextService>();
        services.AddScoped<IEntitlementService, Legal.Application.Features.Saas.EntitlementService>();
        services.AddScoped<ICapabilityAuthorizationService, Legal.Application.Features.Saas.CapabilityAuthorizationService>();
        services.AddScoped<IUsageService, Legal.Application.Features.Saas.UsageService>();
        services.AddScoped<IIntelligenceExecutionService, Legal.Application.Features.Saas.IntelligenceExecutionService>();

        // Configuration control plane (migration 0248).
        services.AddScoped<IConfigurationRepository, Legal.Infrastructure.Persistence.Repositories.ConfigurationRepository>();
        services.AddScoped<IConfigurationResolver, Legal.Application.Features.Saas.ConfigurationResolver>();
        services.AddScoped<ITenantConfigurationService, Legal.Application.Features.Saas.TenantConfigurationService>();
        services.AddScoped<IPlatformConfigurationService, Legal.Application.Features.Saas.PlatformConfigurationService>();

        // User management (Super Admin /platform/users, Tenant Admin /admin/users).
        services.AddScoped<IUserAccountCreator, Legal.Infrastructure.Identity.IdentityUserAccountCreator>();
        services.AddScoped<IUserManagementService, Legal.Application.Features.Saas.UserManagementService>();

        // Owner-only organization/tenant profile (Account Settings /account/settings).
        services.AddScoped<ITenantProfileService, Legal.Application.Features.Saas.TenantProfileService>();

        // Invitations + transactional outbox (Phase B).
        services.AddScoped<IInvitationService, Legal.Application.Features.Saas.InvitationService>();
        services.AddScoped<IOutboxDispatcher, Legal.Infrastructure.Services.OutboxDispatcher>();

        // Tenant groups (Phase C).
        services.AddScoped<IGroupService, Legal.Application.Features.Saas.GroupService>();

        // Activity read surface: audit events, usage, login history.
        services.AddScoped<IActivityService, Legal.Application.Features.Saas.ActivityService>();

        // Legal clickwrap consent: agreements + consent evidence.
        services.AddScoped<IConsentService, Legal.Application.Features.Saas.ConsentService>();

        return services;
    }
}

// Default ambient-tenant accessor for non-HTTP scopes (workers, migrations, tests): no tenant, so
// EpistemicAuthoritySettings resolution falls back to Platform + code defaults. Hosts override this.
internal sealed class NullEpistemicTenantAccessor : IEpistemicTenantAccessor
{
    public Guid? TenantId => null;
}

