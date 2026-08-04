using System.Data;
using System.Net;
using System.Reflection;
using System.Text;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.Intelligence;
using Ams.Infrastructure.Persistence;
using Ams.Infrastructure.Persistence.Repositories;
using Ams.Web.Services;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Ams.Application.Tests;

public sealed class IntelligencePlatformContractTests
{
    [Fact]
    public void Migration_DefinesCompleteTenantAwarePlatformWithoutFabricatedEvidence()
    {
        var assembly=typeof(DatabaseMigrator).Assembly;var resource=assembly.GetManifestResourceNames().Single(x=>x.EndsWith("0081_EnterpriseIntelligencePlatform.sql",StringComparison.Ordinal));var sql=Read(assembly,resource);
        foreach(var table in new[]{"AI.Provider","AI.ModelDeployment","AI.FeaturePolicy","AI.Execution","AI.ExecutionGroundingSource","AI.ExecutionFeedback","AI.Recommendation","AI.RecommendationWorkItem","AI.SearchDocument","AI.SearchPermission","AI.SearchQuery","AI.ReviewQueueItem","AI.EvaluationDefinition","AI.EvaluationRun","AI.ModuleMetricSnapshot"})Assert.Contains(table,sql,StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MERGE AI.Provider",sql,StringComparison.OrdinalIgnoreCase);Assert.Contains("MERGE AI.RecommendationType",sql,StringComparison.OrdinalIgnoreCase);Assert.Contains("MERGE AI.EvaluationDefinition",sql,StringComparison.OrdinalIgnoreCase);Assert.Contains("MERGE Core.ConfigurationSetting",sql,StringComparison.OrdinalIgnoreCase);Assert.Contains("INSERT(SettingId,TenantId,ScopeCode",sql,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("ConfigurationSettingId",sql,StringComparison.OrdinalIgnoreCase);Assert.Contains("LeaseExpiresDateUtc",sql,StringComparison.OrdinalIgnoreCase);Assert.Contains("WHERE existing.PermissionCode=source.PermissionCode",sql,StringComparison.OrdinalIgnoreCase);Assert.Contains("role.TenantId,role.RoleId,permission.PermissionId",sql,StringComparison.OrdinalIgnoreCase);Assert.Contains("Intelligence.Audit.Read",sql,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE FULLTEXT",sql,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("INSERT AI.Execution(",sql,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("INSERT AI.Recommendation(",sql,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("INSERT AI.EvaluationRun(",sql,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("INSERT INTO Core.Tenant",sql,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PersistenceAndWorker_EnforceTenantPermissionsLeasesAndDatabaseRules()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));var repository=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Persistence","Repositories","IntelligenceRepository.cs"));var worker=File.ReadAllText(Path.Combine(root,"Ams.Worker","Intelligence","IntelligenceWorkerProcessor.cs"));var service=File.ReadAllText(Path.Combine(root,"Ams.Worker","Intelligence","IntelligenceWorkerService.cs"));
        Assert.Contains("execution.TenantId=@TenantId",repository,StringComparison.Ordinal);Assert.Contains("permission.PrincipalTypeCode=N'USER'",repository,StringComparison.Ordinal);Assert.Contains("IAM.UserRole",repository,StringComparison.Ordinal);Assert.Contains("RowVersion=@RowVersion",repository,StringComparison.Ordinal);Assert.Contains("AI feature policy changed before this update",repository,StringComparison.Ordinal);Assert.Contains(") KnowledgeChangesToday",repository,StringComparison.Ordinal);Assert.Contains(") ImportJobsInProgress",repository,StringComparison.Ordinal);Assert.Contains(") WorkerQueueDepth",repository,StringComparison.Ordinal);Assert.Contains("ParseSearchIntent(request.Query)",repository,StringComparison.Ordinal);Assert.Contains("document.SourceCreatedDateUtc",repository,StringComparison.Ordinal);Assert.Contains("CASE WHEN @OrderByRecency=1 THEN SourceCreatedDateUtc END DESC",repository,StringComparison.Ordinal);Assert.Contains("new(\"SUBMISSION\",\"Submissions\",orderByRecency)",repository,StringComparison.Ordinal);Assert.Contains("UPDLOCK,READPAST,ROWLOCK",worker,StringComparison.Ordinal);Assert.Contains("AI.RecommendationRule",worker,StringComparison.Ordinal);Assert.Contains("SourceCreatedDateUtc=source.SourceCreatedDateUtc",worker,StringComparison.Ordinal);Assert.Contains("sp_getapplock",worker,StringComparison.Ordinal);Assert.Contains("target.IsDeleted=1",worker,StringComparison.Ordinal);Assert.Contains("LeaseExpiresDateUtc<SYSUTCDATETIME()",worker,StringComparison.Ordinal);Assert.Contains("IAM.RolePermission",worker,StringComparison.Ordinal);Assert.Contains(":BackgroundService",service,StringComparison.Ordinal);Assert.DoesNotContain("new RecommendationDto",service,StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApiClient_UsesTenantFreeRoutesAndPreservesDecisionConcurrency()
    {
        var requests=new List<CapturedRequest>();var handler=new StubHandler(requests,Json(HttpStatusCode.OK,"{\"items\":[],\"totalCount\":0,\"pageNumber\":1,\"pageSize\":50}"),new(HttpStatusCode.NoContent),Json(HttpStatusCode.OK,"{\"items\":[],\"totalCount\":0,\"pageNumber\":1,\"pageSize\":50}"),new(HttpStatusCode.NoContent));var client=new ApiClient(new HttpClient(handler){BaseAddress=new("https://ams.test/")});var id=Guid.NewGuid();var rowVersion=new byte[]{1,2,3,4,5,6,7,8};
        await client.SearchIntelligenceExecutionsAsync(pageSize:50);await client.SubmitIntelligenceFeedbackAsync(id,new(Guid.NewGuid(),id,"QUALITY",5,null,"Verified",Guid.NewGuid()));await client.SearchRecommendationsAsync(pageSize:50);await client.DecideRecommendationAsync(id,new(Guid.NewGuid(),id,"ACCEPT","Verified",Guid.NewGuid(),rowVersion));
        Assert.All(requests,request=>Assert.DoesNotContain("tenantId",request.Path,StringComparison.OrdinalIgnoreCase));Assert.Equal("api/intelligence/executions?searchTerm=&featureCode=&statusCode=&pageNumber=1&pageSize=50",requests[0].Path);Assert.Equal($"api/intelligence/executions/{id}/feedback",requests[1].Path);Assert.Contains(Convert.ToBase64String(rowVersion),requests[3].Body);
    }

    [Fact]
    public void BlazorWorkspaces_UseTypedApisAndValidDatabaseEmptyStates()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","..","src","Ams.Web","Components","Pages","Intelligence"));var files=Directory.GetFiles(root,"*.razor").Select(File.ReadAllText).ToArray();var source=string.Join(Environment.NewLine,files);
        Assert.Contains("Api.GetIntelligenceDashboardAsync",source,StringComparison.Ordinal);Assert.Contains("Api.IntelligenceSearchAsync",source,StringComparison.Ordinal);Assert.Contains("Api.SearchRecommendationsAsync",source,StringComparison.Ordinal);Assert.Contains("Api.SearchIntelligenceReviewQueueAsync",source,StringComparison.Ordinal);Assert.Contains("Api.SearchIntelligenceExecutionsAsync",source,StringComparison.Ordinal);Assert.Contains("Api.GetIntelligenceEvaluationDefinitionsAsync",source,StringComparison.Ordinal);Assert.Contains("Policy=\"@Ams.Web.Security.IntelligencePolicies.Recommend\"",source,StringComparison.Ordinal);Assert.Contains("Policy=\"@Ams.Web.Security.KnowledgePolicies.Import\"",source,StringComparison.Ordinal);Assert.Contains("No authorized indexed records matched",source,StringComparison.Ordinal);Assert.DoesNotContain("new List<RecommendationDto>",source,StringComparison.Ordinal);
    }

    [SqlIntegrationFact]
    [Trait("Category","SqlIntegration")]
    public async Task ExecutionSearch_IsStrictlyTenantScoped()
    {
        var connectionString=Environment.GetEnvironmentVariable("AMS_TEST_SQL_CONNECTION")!;var tenantA=Guid.NewGuid();var tenantB=Guid.NewGuid();var executionA=Guid.NewGuid();var executionB=Guid.NewGuid();await using(var connection=new SqlConnection(connectionString)){await connection.OpenAsync();const string insert="INSERT AI.Execution(ExecutionId,TenantId,FeatureCode,ModuleCode,StatusCode,CorrelationId,StartedDateUtc,CreatedDateUtc,IsDeleted) VALUES(@Id,@TenantId,N'TEST',N'TEST',N'COMPLETED',@Correlation,SYSUTCDATETIME(),SYSUTCDATETIME(),0);";await connection.ExecuteAsync(insert,new{Id=executionA,TenantId=tenantA,Correlation=executionA.ToString("N")});await connection.ExecuteAsync(insert,new{Id=executionB,TenantId=tenantB,Correlation=executionB.ToString("N")});}
        try{var repository=new IntelligenceRepository(new TestConnectionFactory(connectionString));var result=await repository.SearchExecutionsAsync(new(tenantA,null,null,null,null,null,1,50));Assert.Contains(result.Items,x=>x.ExecutionId==executionA);Assert.DoesNotContain(result.Items,x=>x.ExecutionId==executionB);}finally{await using var cleanup=new SqlConnection(connectionString);await cleanup.OpenAsync();await cleanup.ExecuteAsync("DELETE AI.Execution WHERE ExecutionId IN @Ids",new{Ids=new[]{executionA,executionB}});}
    }

    private static string Read(Assembly assembly,string resource){using var stream=assembly.GetManifestResourceStream(resource)!;using var reader=new StreamReader(stream);return reader.ReadToEnd();}
    private static HttpResponseMessage Json(HttpStatusCode status,string json)=>new(status){Content=new StringContent(json,Encoding.UTF8,"application/json")};
    private sealed record CapturedRequest(string Path,string Body);
    private sealed class StubHandler(List<CapturedRequest> requests,params HttpResponseMessage[] responses):HttpMessageHandler{private int _index;protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken){requests.Add(new(request.RequestUri!.PathAndQuery.TrimStart('/'),request.Content is null?string.Empty:await request.Content.ReadAsStringAsync(cancellationToken)));return responses[_index++];}}
    private sealed class TestConnectionFactory(string connectionString):ISqlConnectionFactory{public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken=default){var connection=new SqlConnection(connectionString);await connection.OpenAsync(cancellationToken);return connection;}}
}
