using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Legal.Application;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence;
using Legal.Infrastructure.Persistence;
using Legal.Infrastructure.Persistence.Repositories;
using Legal.Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

await VerifyRuntimeAsync();
if(args.Length==0)return;
if(args.Length!=2||args[0] is not ("--apply" or "--verify"))
    throw new ArgumentException("Usage: AstraPromptContracts [--apply|--verify path-to-appsettings.json]. No model calls are made.");
using var config=JsonDocument.Parse(await File.ReadAllTextAsync(args[1]));
var connectionString=config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString()
    ??throw new InvalidOperationException("DefaultConnection is missing.");
var factory=new ConnectionFactory(connectionString);
using var connection=await factory.CreateOpenConnectionAsync();
Console.WriteLine($"Database: {connection.Database}");
var before=await UnchangedDataHashAsync(connection);
if(args[0]=="--apply")
{
    var inserted=await AstraPromptContractMigration.ApplyAsync(connection);
    Console.WriteLine($"Astra prompt versions inserted: {inserted}");
    Require(await AstraPromptContractMigration.ApplyAsync(connection)==0,"Migration must be idempotent.");
}
Require(before==await UnchangedDataHashAsync(connection),"Existing prompts, model deployments, feature policies, providers, and settings must remain unchanged.");
var rows=(await connection.QueryAsync<PromptRow>("SELECT TenantId,PromptCode,SystemInstructions,InputSchemaJson,OutputSchemaJson FROM AI.Legal_PromptDefinition WHERE VersionLabel=@VersionLabel AND IsDeleted=0 AND StatusCode=N'APPROVED';",new{AstraPromptContractMigration.VersionLabel})).ToArray();
var catalog=new PromptCatalog(factory);
foreach(var (code,contract) in IntelligenceWideService.AstraPromptContracts)
{
    var matches=rows.Where(row=>row.PromptCode==code+"_ASTRA").ToArray();
    Require(matches.Any(row=>row.TenantId is null),$"Missing global Astra registry row: {code}.");
    foreach(var row in matches)
    {
        Require(row.OutputSchemaJson==contract.OutputSchemaJson,$"DB/runtime output schema mismatch: {row.PromptCode}.");
        Require(row.InputSchemaJson==AstraPromptContractMigration.InputSchemaJson,$"DB input schema mismatch: {row.PromptCode}.");
        Require(row.SystemInstructions.Contains(contract.Instructions,StringComparison.Ordinal),$"Missing stage instructions: {row.PromptCode}.");
        Require(await catalog.GetSystemPromptAsync(row.TenantId??Guid.Empty,row.PromptCode)==row.SystemInstructions,$"Registry does not select the migrated prompt: {row.PromptCode}.");
    }
}
var tenants=(await connection.QueryAsync<Guid>("SELECT DISTINCT TenantId FROM AI.Legal_FeaturePolicy WHERE FeatureCode=N'INTELLIGENCE_WIDE_ANSWER_ASTRA' AND IsEnabled=1 AND IsDeleted=0;")).ToArray();
Require(tenants.Length>0,"No enabled Astra answer policy exists.");
var routes=new AiProviderRouteRepository(factory);
foreach(var tenant in tenants)
{
    var selected=await routes.GetRoutesAsync(tenant,"INTELLIGENCE_WIDE_ANSWER_ASTRA","CHAT",IntelligenceWideService.AstraModelCode);
    Require(selected.Count>0&&selected.All(route=>route.ModelCode==IntelligenceWideService.AstraModelCode),"Astra answer policy must resolve to Astra.");
}
Console.WriteLine($"PASS: {rows.Length} versioned Astra prompt rows match runtime JSON contracts and registry selection.");
Console.WriteLine($"PASS: Astra answer routes for {tenants.Length} tenants; existing configuration unchanged; no model requests sent.");

static async Task VerifyRuntimeAsync()
{
    var service=new IntelligenceWideService(null!,null!,null!,null!,null!,new EchoCatalog(),null!,null!,NullLogger<IntelligenceWideService>.Instance);
    var promptMethod=typeof(IntelligenceWideService).GetMethod("GetWideSystemPromptAsync",BindingFlags.Instance|BindingFlags.NonPublic)!;
    var featureMethod=typeof(IntelligenceWideService).GetMethod("AnswerFeatureCode",BindingFlags.Static|BindingFlags.NonPublic)!;
    var models=new string?[]{null,"Auto","gpt-4.1-mini","gpt-5.6-sol","gpt-6-astra"," GPT-6-ASTRA "};
    foreach(var (code,contract) in IntelligenceWideService.AstraPromptContracts)
    {
        using var schema=JsonDocument.Parse(contract.OutputSchemaJson);
        Require(schema.RootElement.GetProperty("type").GetString()=="object",$"Output must be an object: {code}.");
        CheckSchema(schema.RootElement,schema.RootElement,code);
        foreach(var model in models)
        {
            var request=new WideSearchRequest(Guid.Empty,Guid.Empty,"contract verification",10,"offline-check"){ModelCode=model};
            var selected=await (Task<string>)promptMethod.Invoke(service,[request,code,CancellationToken.None])!;
            Require(selected==code,$"Unexpected prompt selection for model {model??"null"} and {code}: Astra must reuse the canonical Sol prompt.");
            var feature=(string)featureMethod.Invoke(null,[request])!;
            Require(feature=="INTELLIGENCE_WIDE_ANSWER","All models must reuse the shared Sol answer feature policy.");
        }
    }
    Console.WriteLine($"PASS: {IntelligenceWideService.AstraPromptContracts.Count} JSON contracts and {IntelligenceWideService.AstraPromptContracts.Count*models.Length} prompt/policy selection cases.");
}

static void CheckSchema(JsonElement node,JsonElement root,string code)
{
    if(node.ValueKind==JsonValueKind.Object)
    {
        if(node.TryGetProperty("$ref",out var reference))
        {
            var path=reference.GetString()!;
            Require(path.StartsWith("#/",StringComparison.Ordinal),$"Unsupported schema reference in {code}.");
            var target=root;
            foreach(var segment in path[2..].Split('/'))
                Require(target.TryGetProperty(segment.Replace("~1","/").Replace("~0","~"),out target),$"Broken schema reference {path} in {code}.");
        }
        if(node.TryGetProperty("required",out var required)&&node.TryGetProperty("properties",out var properties))
            foreach(var name in required.EnumerateArray())Require(properties.TryGetProperty(name.GetString()!,out _),$"Unknown required property in {code}.");
        foreach(var property in node.EnumerateObject())CheckSchema(property.Value,root,code);
    }
    else if(node.ValueKind==JsonValueKind.Array)
        foreach(var item in node.EnumerateArray())CheckSchema(item,root,code);
}

static async Task<string> UnchangedDataHashAsync(IDbConnection connection)
{
    string[] queries=[
        "SELECT * FROM AI.Legal_PromptDefinition WHERE VersionLabel<>@VersionLabel ORDER BY PromptDefinitionId FOR JSON PATH,INCLUDE_NULL_VALUES;",
        "SELECT * FROM AI.Legal_ModelDeployment ORDER BY ModelDeploymentId FOR JSON PATH,INCLUDE_NULL_VALUES;",
        "SELECT * FROM AI.Legal_FeaturePolicy ORDER BY FeaturePolicyId FOR JSON PATH,INCLUDE_NULL_VALUES;",
        "SELECT * FROM AI.Legal_Provider ORDER BY ProviderId FOR JSON PATH,INCLUDE_NULL_VALUES;",
        "SELECT * FROM Core.ConfigurationSetting ORDER BY SettingId FOR JSON PATH,INCLUDE_NULL_VALUES;"
    ];
    var data=new StringBuilder();
    foreach(var query in queries)
        data.Append(string.Concat(await connection.QueryAsync<string>(query,new{AstraPromptContractMigration.VersionLabel}))).Append('\n');
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data.ToString())));
}

static void Require(bool condition,string message)
{
    if(!condition)throw new InvalidOperationException(message);
}

sealed record PromptRow(Guid? TenantId,string PromptCode,string SystemInstructions,string InputSchemaJson,string OutputSchemaJson);
sealed class EchoCatalog:IPromptCatalog
{
    public Task<string> GetSystemPromptAsync(Guid tenantId,string promptCode,CancellationToken cancellationToken=default)=>Task.FromResult(promptCode);
}
sealed class ConnectionFactory(string connectionString):ISqlConnectionFactory
{
    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken=default)
    {
        var connection=new SqlConnection(connectionString);
        try{await connection.OpenAsync(cancellationToken);return connection;}
        catch{await connection.DisposeAsync();throw;}
    }
}
