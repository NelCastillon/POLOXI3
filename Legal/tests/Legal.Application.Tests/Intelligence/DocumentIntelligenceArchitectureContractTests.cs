using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class DocumentIntelligenceArchitectureContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Migration0291_DefinesCanonicalCorpusTelemetryAndSafeDefaults()
    {
        var sql = Read("src", "Legal.Infrastructure", "Migrations", "0291_LegalDocumentIntelligenceThreeStageRetrieval.sql");

        Assert.Contains("POLOXI.Legal_MatterDocumentVersion", sql, StringComparison.Ordinal);
        Assert.Contains("Sha256Hash CHAR(64) NOT NULL", sql, StringComparison.Ordinal);
        Assert.Contains("POLOXI.Legal_DocumentPassage", sql, StringComparison.Ordinal);
        Assert.Contains("POLOXI.Legal_MatterEvidenceItem", sql, StringComparison.Ordinal);
        Assert.Contains("POLOXI.Legal_MatterFactProposition", sql, StringComparison.Ordinal);
        Assert.Contains("IsDecisionAuthoritative BIT NOT NULL", sql, StringComparison.Ordinal);
        Assert.Contains("POLOXI.Legal_DecisionRetrievalTelemetry", sql, StringComparison.Ordinal);
        Assert.Contains("Decision.LegacyUnconditionalRetrieval.Enabled", sql, StringComparison.Ordinal);
        Assert.Contains("N'false'", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DecisionService_RetrievesMatterContextBeforeDiscovery_AndContainsNoLegacyBypassMethods()
    {
        var source = Read("src", "Legal.Application", "LegalDecisionService.cs");
        var matterContext = source.IndexOf("matterContextRetriever.RetrieveAsync", StringComparison.Ordinal);
        var discovery = source.IndexOf("new DecisionAiRequest(route, discoveryPromptCode", StringComparison.Ordinal);

        Assert.True(matterContext >= 0 && discovery > matterContext);
        Assert.DoesNotContain("RetrieveEvidenceAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RetrieveForBranchAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Record(\"EVIDENCE_RETRIEVED\"", source, StringComparison.Ordinal);
        Assert.Contains("Record(\"EVIDENCE_RETRIEVAL_DEFERRED\"", source, StringComparison.Ordinal);
        Assert.Contains("DecisionResearchStates.RequiredPending", source, StringComparison.Ordinal);
        Assert.Contains("LEGACY_RETRIEVAL_BYPASS_IGNORED", source, StringComparison.Ordinal);
        Assert.Contains("evidenceVerificationPipeline.VerifyAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CorpusQueries_AreTenantScoped_AndLegacyFallbackRequiresUserReadPermission()
    {
        var source = Read("src", "Legal.Infrastructure", "Persistence", "Repositories", "LegalDocumentCorpusRepository.cs");

        Assert.Contains("document.TenantId=@TenantId", source, StringComparison.Ordinal);
        Assert.Contains("permission.PrincipalId=@UserId", source, StringComparison.Ordinal);
        Assert.Contains("permission.PermissionCode=N'READ'", source, StringComparison.Ordinal);
        Assert.Contains("document.DocumentTypeCode IN (SELECT [value] FROM OPENJSON(@DocumentTypesJson))", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CanonicalExtractionContracts_AreProviderNeutralAndSupportHybridLayoutProvenance()
    {
        var contracts = Read("src", "Legal.Application", "Features", "Intelligence", "Decision", "DocumentIntelligenceContracts.cs");

        Assert.DoesNotContain("Azure.", contracts, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyCollection<int>? PageNumbers", contracts, StringComparison.Ordinal);
        Assert.Contains("FallbackPageNumbers", contracts, StringComparison.Ordinal);
        Assert.Contains("DocumentExtractedSelectionMark", contracts, StringComparison.Ordinal);
        Assert.Contains("SourceSpanJson", contracts, StringComparison.Ordinal);
        Assert.Contains("IsNativeTextReliable", contracts, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractionRouter_UsesSelectiveFallbackAndPreservesNativePages()
    {
        var source = Read("src", "Legal.Application", "Features", "Intelligence", "Decision", "LegalDocumentExtractionRouter.cs");

        Assert.Contains("PageNumbers = native.PagesRequiringFallback", source, StringComparison.Ordinal);
        Assert.Contains("Merge(native, fallback)", source, StringComparison.Ordinal);
        Assert.Contains("HYBRID_NATIVE_AZURE", source, StringComparison.Ordinal);
        Assert.Contains("Where(HasUsableText)", source, StringComparison.Ordinal);
        Assert.Contains("paragraph.SourceSpanJson", Read("src", "Legal.Application", "Features", "Intelligence", "Decision", "LegalDocumentIntakeService.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void AzureProvider_UsesPrebuiltLayoutOptionsAndNormalizesLayoutArtifacts()
    {
        var source = Read("src", "Legal.Infrastructure", "Intelligence", "AzureDocumentIntelligenceProvider.cs");

        Assert.Contains("AnalyzeDocumentOptions", source, StringComparison.Ordinal);
        Assert.Contains("analyzeOptions.Pages", source, StringComparison.Ordinal);
        Assert.Contains("DocumentExtractedLine", source, StringComparison.Ordinal);
        Assert.Contains("DocumentExtractedWord", source, StringComparison.Ordinal);
        Assert.Contains("DocumentExtractedSelectionMark", source, StringComparison.Ordinal);
        Assert.Contains("DocumentExtractedFigure", source, StringComparison.Ordinal);
        Assert.True(source.IndexOf("public async Task<DocumentExtractionResult> ExtractAsync", StringComparison.Ordinal) < source.IndexOf("Uri.TryCreate(_options.Endpoint", StringComparison.Ordinal));
    }

    [Fact]
    public void Migration0292_AndRepositoryPersistImmutableNormalizedLayoutAndAudit()
    {
        var sql = Read("src", "Legal.Infrastructure", "Migrations", "0292_LegalDocumentNormalizedLayout.sql");
        var repository = Read("src", "Legal.Infrastructure", "Persistence", "Repositories", "LegalDocumentCorpusRepository.cs");

        Assert.Contains("POLOXI.Legal_DocumentPage", sql, StringComparison.Ordinal);
        Assert.Contains("POLOXI.Legal_DocumentLayoutArtifact", sql, StringComparison.Ordinal);
        Assert.Contains("SELECTION_MARK", sql, StringComparison.Ordinal);
        Assert.Contains("OPENJSON(@PagesJson)", repository, StringComparison.Ordinal);
        Assert.Contains("OPENJSON(@ArtifactsJson)", repository, StringComparison.Ordinal);
        Assert.Contains("N'EXTRACTION',N'COMPLETED'", repository, StringComparison.Ordinal);
        Assert.Contains("CorrelationId = correlationId", repository, StringComparison.Ordinal);
    }

    [Fact]
    public void FrozenStage1_UsesAzureWormDefenderAndSecureIntakeByDefault()
    {
        var storage = Read("src", "Legal.Infrastructure", "Intelligence", "AzureBlobLegalDocumentBinaryStore.cs");
        var scanner = Read("src", "Legal.Infrastructure", "Intelligence", "AzureDefenderLegalDocumentSecurityScanner.cs");
        var validator = Read("src", "Legal.Infrastructure", "Intelligence", "LegalDocumentIntakeValidator.cs");
        var configuration = Read("src", "Legal.Api", "appsettings.json");

        Assert.Contains("IfNoneMatch = ETag.All", storage, StringComparison.Ordinal);
        Assert.Contains("SetImmutabilityPolicyAsync", storage, StringComparison.Ordinal);
        Assert.Contains("SetLegalHoldAsync", storage, StringComparison.Ordinal);
        Assert.Contains("response.Value.VersionId", storage, StringComparison.Ordinal);
        Assert.Contains("GetPropertiesAsync", storage, StringComparison.Ordinal);
        Assert.Contains("properties.ImmutabilityPolicy", storage, StringComparison.Ordinal);
        Assert.Contains("Malware Scanning scan result", scanner, StringComparison.Ordinal);
        Assert.Contains("SCAN_TIMEOUT", scanner, StringComparison.Ordinal);
        Assert.Contains("INCONCLUSIVE", scanner, StringComparison.Ordinal);
        Assert.Contains("blob.Uri.AbsoluteUri", scanner, StringComparison.Ordinal);
        Assert.Contains("QuarantineReference", Read("src", "Legal.Application", "Features", "Intelligence", "Decision", "DocumentIntelligenceContracts.cs"), StringComparison.Ordinal);
        Assert.Contains("SignatureMatches", validator, StringComparison.Ordinal);
        Assert.Contains("\"BinaryStoreProvider\": \"AzureBlob\"", configuration, StringComparison.Ordinal);
        Assert.Contains("\"MalwareScannerProvider\": \"DefenderForStorage\"", configuration, StringComparison.Ordinal);
    }

    [Fact]
    public void SemanticGovernance_RequiresGroundedDomainConformingEvidence()
    {
        var source = Read("src", "Legal.Application", "Features", "Intelligence", "Decision", "LegalDocumentSemanticInterpreter.cs");

        Assert.Contains("item.PassageId.HasValue", source, StringComparison.Ordinal);
        Assert.Contains("conceptsByCode.ContainsKey", source, StringComparison.Ordinal);
        Assert.Contains("supportedFactKeys.Contains", source, StringComparison.Ordinal);
        Assert.Contains("FactStateCode = LegalFactStates.Alleged", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchProjection_IsTransactionalRetryableAndExplicitlyDerived()
    {
        var migration = Read("src", "Legal.Infrastructure", "Migrations", "0293_LegalDocumentStage1FrozenArchitecture.sql");
        var leaseRepair = Read("src", "Legal.Infrastructure", "Migrations", "0294_LegalDocumentSearchProjectionLeaseRepair.sql");
        var repository = Read("src", "Legal.Infrastructure", "Persistence", "Repositories", "LegalDocumentCorpusRepository.cs");
        var dispatcher = Read("src", "Legal.Infrastructure", "Intelligence", "LegalDocumentSearchProjectionDispatcher.cs");

        Assert.Contains("Legal_DocumentSearchProjectionOutbox", migration, StringComparison.Ordinal);
        Assert.Contains("Legal_DocumentSearchProjectionOutbox", repository, StringComparison.Ordinal);
        Assert.Contains("AI.Legal_SearchDocument", dispatcher, StringComparison.Ordinal);
        Assert.Contains("LEGAL_DOCUMENT_PASSAGE", dispatcher, StringComparison.Ordinal);
        Assert.Contains("MergeOrUploadDocumentsAsync", dispatcher, StringComparison.Ordinal);
        Assert.Contains("matterId", dispatcher, StringComparison.Ordinal);
        Assert.Contains("AI.Legal_SearchPermission", dispatcher, StringComparison.Ordinal);
        Assert.Contains("PrincipalTypeCode=N'USER'", dispatcher, StringComparison.Ordinal);
        Assert.DoesNotContain("PrincipalTypeCode=N'ROLE'", dispatcher, StringComparison.Ordinal);
        Assert.Contains("ProcessingStartedDateUtc", migration, StringComparison.Ordinal);
        Assert.Contains("COL_LENGTH", leaseRepair, StringComparison.Ordinal);
        Assert.Contains("ProcessingStartedDateUtc", leaseRepair, StringComparison.Ordinal);
        Assert.Contains("DATEADD(minute,-10", dispatcher, StringComparison.Ordinal);
        Assert.Contains("READPAST,ROWLOCK,READCOMMITTEDLOCK", dispatcher, StringComparison.Ordinal);
        Assert.Contains("DEAD_LETTER", dispatcher, StringComparison.Ordinal);
        Assert.Contains("CreateOrUpdateIndexAsync", dispatcher, StringComparison.Ordinal);
        Assert.Contains("SimpleField(IsFilterable = true)] Guid TenantId", dispatcher, StringComparison.Ordinal);
        Assert.Contains("SimpleField(IsFilterable = true)] Guid MatterId", dispatcher, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([RepositoryRoot, .. parts]));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Legal.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the Legal solution root.");
    }
}
