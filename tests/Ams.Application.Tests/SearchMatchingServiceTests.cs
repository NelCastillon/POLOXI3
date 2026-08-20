using System.ComponentModel.DataAnnotations;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Intelligence;
using Ams.Application.Features.Intelligence;
using Ams.Application.Features.SearchMatching;
using Ams.Application.Services;
using Xunit;

namespace Ams.Application.Tests;

public sealed class SearchMatchingServiceTests
{
    [Fact]
    public void Normalize_AppliesDatabaseTermsAndRemovesDiacritics()
    {
        NormalizationTermPolicy[] terms =
        [
            new("Global", "BusinessName", "the", "", "STOP_WORD"),
            new("Account", "BusinessName", "co", "company", "ABBREVIATION")
        ];

        var result = SearchMatchingAlgorithms.Normalize("Thé Acme Co.", "Account", "BusinessName", terms);

        Assert.Equal("acme company", result);
    }

    [Theory]
    [InlineData("Robert", "Rupert")]
    [InlineData("Ashcraft", "Ashcroft")]
    public void Soundex_ProducesSameCodeForPhoneticNames(string left, string right)
        => Assert.Equal(SearchMatchingAlgorithms.Soundex(left), SearchMatchingAlgorithms.Soundex(right));

    [Fact]
    public void DamerauLevenshteinDistance_RecognizesTransposition()
        => Assert.Equal(1, SearchMatchingAlgorithms.DamerauLevenshteinDistance("agency", "agenyc"));

    [Fact]
    public async Task FindMatchesAsync_BlocksCriticalIdentifierDiscrepancy()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeSearchMatchingRepository
        {
            Policy = Policy("ACCOUNT_DUPLICATE", "Account",
                new MatchFieldPolicy(Guid.NewGuid(), "Fein", "FEIN", "EXACT", 45, 100, false, true, true, true),
                new MatchFieldPolicy(Guid.NewGuid(), "BusinessName", "Legal Name", "NORMALIZED_EXACT", 55, 100, true, false, false, false)),
            Projections =
            [
                new(Guid.NewGuid(), Guid.NewGuid(), "Account", "Acme Company", null, "/accounts/1", "Intelligence.Search",
                    new Dictionary<string, string?> { ["Fein"] = "999999999", ["BusinessName"] = "Acme Company" })
            ]
        };
        var service = new EntityMatchingService(repository);

        var result = await service.FindMatchesAsync(new()
        {
            TenantId = tenantId,
            ProfileCode = "ACCOUNT_DUPLICATE",
            EntityTypeCode = "Account",
            CorrelationId = "test-critical-id",
            Fields = new Dictionary<string, string?> { ["Fein"] = "111111111", ["BusinessName"] = "Acme Company" }
        });

        Assert.Empty(result.Candidates);
        Assert.NotNull(repository.CompletedCandidates);
        Assert.Empty(repository.CompletedCandidates!);
    }

    [Fact]
    public async Task FindMatchesAsync_BlocksCandidateWhenRequiredDatabaseFieldIsMissing()
    {
        var repository = new FakeSearchMatchingRepository
        {
            Policy = Policy("ACCOUNT_DUPLICATE", "Account", new MatchFieldPolicy(Guid.NewGuid(), "BusinessName", "Legal Name", "NORMALIZED_EXACT", 100, 100, true, false, false, false)),
            Projections = [new(Guid.NewGuid(), Guid.NewGuid(), "Account", "Unnamed", null, null, "Intelligence.Search", new Dictionary<string, string?> { ["BusinessName"] = null })]
        };
        var service = new EntityMatchingService(repository);

        var result = await service.FindMatchesAsync(new EntityMatchRequest { TenantId = Guid.NewGuid(), ProfileCode = "ACCOUNT_DUPLICATE", EntityTypeCode = "Account", CorrelationId = "required-field", Fields = new Dictionary<string, string?> { ["BusinessName"] = "Acme" } });

        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task FindMatchesAsync_UsesDatabaseReviewPolicy()
    {
        var repository = new FakeSearchMatchingRepository
        {
            Policy = Policy("ACCOUNT_DUPLICATE", "Account", false, new MatchFieldPolicy(Guid.NewGuid(), "BusinessName", "Legal Name", "NORMALIZED_EXACT", 100, 100, true, false, false, false)),
            Projections = [new(Guid.NewGuid(), Guid.NewGuid(), "Account", "Acme", null, null, "Intelligence.Search", new Dictionary<string, string?> { ["BusinessName"] = "Acme" })]
        };
        var service = new EntityMatchingService(repository);

        var result = await service.FindMatchesAsync(new EntityMatchRequest { TenantId = Guid.NewGuid(), ProfileCode = "ACCOUNT_DUPLICATE", EntityTypeCode = "Account", CorrelationId = "review-policy", Fields = new Dictionary<string, string?> { ["BusinessName"] = "Acme" } });

        Assert.False(Assert.Single(result.Candidates).RequiresReview);
    }

    [Fact]
    public async Task SearchAsync_ForwardsTenantAndGrantedPermissions()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeSearchMatchingRepository
        {
            Policy = Policy("GLOBAL_ENTERPRISE_SEARCH", "Global",
                new MatchFieldPolicy(Guid.NewGuid(), "DisplayName", "Display Name", "DAMERAU_LEVENSHTEIN", 100, 50, true, false, false, false)),
            Projections =
            [
                new(Guid.NewGuid(), Guid.NewGuid(), "Account", "Acme", null, "/accounts/1", "Intelligence.Search",
                    new Dictionary<string, string?> { ["DisplayName"] = "Acme" })
            ]
        };
        var service = new EntityMatchingService(repository);

        await service.SearchAsync(new()
        {
            TenantId = tenantId,
            Query = "Acme",
            GrantedPermissions = ["Intelligence.Search"],
            MaximumResults = 5
        });

        Assert.Equal(tenantId, repository.SearchTenantId);
        Assert.Contains("Intelligence.Search", repository.SearchPermissions!);
    }

    [Fact]
    public async Task FindMatchesAsync_RejectsMissingTenantBeforeRepositoryAccess()
    {
        var repository = new FakeSearchMatchingRepository();
        var service = new EntityMatchingService(repository);

        await Assert.ThrowsAsync<ValidationException>(() => service.FindMatchesAsync(new()
        {
            ProfileCode = "ACCOUNT_DUPLICATE",
            EntityTypeCode = "Account",
            CorrelationId = "missing-tenant",
            Fields = new Dictionary<string, string?> { ["BusinessName"] = "Acme" }
        }));

        Assert.Equal(0, repository.PolicyReadCount);
    }

    [Fact]
    public void SemanticAdvisory_UsesExpandedTermOverlap()
        => Assert.Equal(33.3333m, SearchMatchingAlgorithms.Similarity("SEMANTIC_ADVISORY", "commercial auto", "auto fleet", "Global", "SearchText", []));

    [Fact]
    public async Task SearchAsync_ExpandsAndPersistsKnowledgeEvidence()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeSearchMatchingRepository
        {
            Policy = Policy("GLOBAL_ENTERPRISE_SEARCH", "Global",
                new MatchFieldPolicy(Guid.NewGuid(), "SearchText", "Semantic Search", "SEMANTIC_ADVISORY", 100, 30, false, false, false, false)),
            Projections = [new(Guid.NewGuid(), Guid.NewGuid(), "Policy", "Commercial Auto", null, "/policies/1", "Intelligence.Search", new Dictionary<string, string?> { ["SearchText"] = "commercial auto fleet" })]
        };
        var service = new EntityMatchingService(repository, new FakeSemanticExpander());

        var results = await service.SearchAsync(new EnterpriseFuzzySearchRequest { TenantId = tenantId, Query = "business vehicle insurance", GrantedPermissions = ["Intelligence.Search"], CorrelationId = "semantic-test" });

        Assert.Single(results);
        Assert.Equal("semantic-test", repository.SemanticCorrelationId);
        Assert.Contains("commercial auto", repository.SemanticTerms!);
        Assert.Contains("commercial auto", repository.SearchQuery!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchAsync_ReturnsSpellingDistanceMatchForTrpleA()
    {
        const string query="trple a";
        const string displayName="Triple A..  ";
        var entityId=Guid.NewGuid();
        var repository=new FakeSearchMatchingRepository
        {
            Policy=Policy(MatchProfileCodes.GlobalEnterpriseSearch,"Global",
                new MatchFieldPolicy(Guid.NewGuid(),"DisplayName","Display Name","DAMERAU_LEVENSHTEIN",35,60,true,false,false,false),
                new MatchFieldPolicy(Guid.NewGuid(),"SearchText","Search Text","TOKEN_JACCARD",65,30,false,false,false,false)),
            Projections=[new(Guid.NewGuid(),entityId,"Account",displayName,null,"/accounts/1","Intelligence.Search",new Dictionary<string,string?>{{"DisplayName",displayName},{"SearchText",displayName}})]
        };
        var service=new EntityMatchingService(repository);

        var result=Assert.Single(await service.SearchAsync(new(){TenantId=Guid.NewGuid(),Query=query,GrantedPermissions=["Intelligence.Search"]}));
        var editDistanceReason=Assert.Single(result.Reasons,reason=>reason.AlgorithmCode=="DAMERAU_LEVENSHTEIN");

        Assert.Equal(entityId,result.EntityId);
        Assert.True(result.Score>=60);
        Assert.Equal(1,SearchMatchingAlgorithms.DamerauLevenshteinDistance(SearchMatchingAlgorithms.Normalize(query,"Global","DisplayName",[]),SearchMatchingAlgorithms.Normalize(displayName,"Global","DisplayName",[])));
        Assert.Equal(87.5m,editDistanceReason.SimilarityScore);
        Assert.Equal("MATCH_SIGNAL",editDistanceReason.ReasonCode);
    }

    [Fact]
    public async Task SearchAsync_ReturnsSoundexEvidenceForPhoneticGlobalMatch()
    {
        var repository=new FakeSearchMatchingRepository
        {
            Policy=Policy(MatchProfileCodes.GlobalEnterpriseSearch,"Global",new MatchFieldPolicy(Guid.NewGuid(),"DisplayName","Display Name Phonetic","SOUNDEX",100,100,false,false,false,false)),
            Projections=[new(Guid.NewGuid(),Guid.NewGuid(),"Account","Rupert",null,"/accounts/1","Intelligence.Search",new Dictionary<string,string?>{{"DisplayName","Rupert"}})]
        };

        var result=Assert.Single(await new EntityMatchingService(repository).SearchAsync(new(){TenantId=Guid.NewGuid(),Query="Robert",GrantedPermissions=["Intelligence.Search"]}));

        Assert.Contains(result.Reasons,reason=>reason.AlgorithmCode=="SOUNDEX"&&reason.ReasonCode=="MATCH_SIGNAL"&&reason.SimilarityScore==100);
    }

    [Fact]
    public async Task FindModuleMatchesAsync_ResolvesEntityTypeFromDatabaseProfile()
    {
        var repository = new FakeSearchMatchingRepository { Policy = Policy(MatchProfileCodes.VehicleMatch, "Vehicle") };
        var service = new EntityMatchingService(repository);

        await service.FindModuleMatchesAsync(new ModuleMatchRequest { TenantId = Guid.NewGuid(), ProfileCode = MatchProfileCodes.VehicleMatch, CorrelationId = "vehicle-test", Fields = new Dictionary<string, string?> { ["Vin"] = "VIN123" } });

        Assert.Equal("Vehicle", repository.LastExecutionRequest!.EntityTypeCode);
    }

    [Theory]
    [InlineData(MatchReviewDecisionCodes.UseExisting)]
    [InlineData(MatchReviewDecisionCodes.CreateNew)]
    [InlineData(MatchReviewDecisionCodes.Compare)]
    [InlineData(MatchReviewDecisionCodes.MergeRequest)]
    public void ReviewDecisionCodes_AreGoverned(string decisionCode) => Assert.Contains(decisionCode, MatchReviewDecisionCodes.All);

    private static MatchPolicy Policy(string code, string entityType, params MatchFieldPolicy[] fields)
        => Policy(code, entityType, true, fields);

    private static MatchPolicy Policy(string code, string entityType, bool requiresReview, params MatchFieldPolicy[] fields)
        => new(Guid.NewGuid(), code, entityType, 95, 80, 60, 25, 12, requiresReview, fields, []);

    private sealed class FakeSearchMatchingRepository : ISearchMatchingRepository
    {
        public MatchPolicy? Policy { get; init; }
        public IReadOnlyList<MatchProjection> Projections { get; init; } = [];
        public int PolicyReadCount { get; private set; }
        public Guid? SearchTenantId { get; private set; }
        public IReadOnlyCollection<string>? SearchPermissions { get; private set; }
        public IReadOnlyList<MatchCandidate>? CompletedCandidates { get; private set; }
        public EntityMatchRequest? LastExecutionRequest { get; private set; }
        public string? SearchQuery { get; private set; }
        public string? SemanticCorrelationId { get; private set; }
        public IReadOnlyCollection<string>? SemanticTerms { get; private set; }

        public Task<MatchPolicy?> GetPolicyAsync(Guid tenantId, string profileCode, CancellationToken cancellationToken = default)
        {
            PolicyReadCount++;
            return Task.FromResult(Policy);
        }

        public Task<IReadOnlyList<MatchProjection>> GetCandidatesAsync(Guid tenantId, string entityTypeCode, IReadOnlyDictionary<string, string?> fields, int maximumCandidates, CancellationToken cancellationToken = default)
            => Task.FromResult(Projections);

        public Task<IReadOnlyList<MatchProjection>> SearchProjectionsAsync(Guid tenantId, string query, string originalQuery, IReadOnlyCollection<string> entityTypeCodes, IReadOnlyCollection<string> grantedPermissions, int maximumResults, CancellationToken cancellationToken = default)
        {
            SearchTenantId = tenantId;
            SearchQuery = query;
            SearchPermissions = grantedPermissions;
            return Task.FromResult(Projections);
        }

        public Task<Guid> BeginExecutionAsync(EntityMatchRequest request, MatchPolicy policy, CancellationToken cancellationToken = default)
        {
            LastExecutionRequest = request;
            return Task.FromResult(Guid.NewGuid());
        }

        public Task CompleteExecutionAsync(Guid matchExecutionId, IReadOnlyList<MatchCandidate> candidates, CancellationToken cancellationToken = default)
        {
            CompletedCandidates = candidates;
            return Task.CompletedTask;
        }

        public Task FailExecutionAsync(Guid matchExecutionId, string errorMessage, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> RefreshProjectionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task SaveSemanticEvidenceAsync(Guid tenantId, Guid? requestedByUserId, string correlationId, string query, IReadOnlyCollection<string> terms, IReadOnlyCollection<SemanticConceptMatchDto> concepts, CancellationToken cancellationToken = default)
        {
            SemanticCorrelationId = correlationId;
            SemanticTerms = terms;
            return Task.CompletedTask;
        }
        public Task<MatchReviewDecision> SaveReviewDecisionAsync(MatchReviewDecisionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MatchReviewDecision>> GetReviewDecisionsAsync(Guid tenantId, Guid matchExecutionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MatchReviewDecision>>([]);
        public Task<SemanticPreprocessingSettings> GetSemanticPreprocessingSettingsAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(new SemanticPreprocessingSettings(12, 3, 30));
        public Task<SearchMatchingAdministration> GetAdministrationAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Guid> SaveProfileAsync(Guid tenantId, Guid actorUserId, SaveMatchProfileSettingRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteProfileAsync(Guid tenantId, Guid actorUserId, Guid matchProfileId, byte[] rowVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Guid> SaveFieldRuleAsync(Guid tenantId, Guid actorUserId, SaveMatchFieldRuleSettingRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteFieldRuleAsync(Guid tenantId, Guid actorUserId, Guid matchFieldRuleId, byte[] rowVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Guid> SaveAlgorithmAsync(Guid tenantId, Guid actorUserId, SaveMatchAlgorithmSettingRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAlgorithmAsync(Guid tenantId, Guid actorUserId, Guid matchAlgorithmId, byte[] rowVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Guid> SaveNormalizationTermAsync(Guid tenantId, Guid actorUserId, SaveNormalizationTermSettingRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteNormalizationTermAsync(Guid tenantId, Guid actorUserId, Guid normalizationTermId, byte[] rowVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeSemanticExpander : ISemanticQueryExpander
    {
        public Task<SemanticQueryExpansion> ExpandAsync(Guid tenantId, string query, int maximumConcepts, CancellationToken cancellationToken = default)
            => Task.FromResult(new SemanticQueryExpansion(["commercial auto"], [new(Guid.NewGuid(), "LOB.COMMERCIAL_AUTO", "Commercial Auto", 1, 95, "TEST")]));
    }
}
