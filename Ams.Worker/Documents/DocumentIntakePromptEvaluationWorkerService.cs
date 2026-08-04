using System.Diagnostics;
using System.Text.Json;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.DocumentIntake;

namespace Ams.Worker.Documents;

public sealed class DocumentIntakePromptEvaluationWorkerService(IServiceProvider services,ILogger<DocumentIntakePromptEvaluationWorkerService> logger):BackgroundService
{
    private readonly string _leaseOwner=$"{Environment.MachineName}:{Environment.ProcessId}:prompt-evaluation";
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope=services.CreateScope();var repository=scope.ServiceProvider.GetRequiredService<IDocumentIntakeOperationsRepository>();var payloads=scope.ServiceProvider.GetRequiredService<IDocumentIntakePayloadStore>();var provider=scope.ServiceProvider.GetRequiredService<IDocumentInterpretationProvider>();
                foreach(var run in await repository.LeasePromptEvaluationsAsync(_leaseOwner,5,stoppingToken))
                {
                    var results=new List<DocumentIntakePromptEvaluationCaseResult>();
                    foreach(var test in run.Cases)
                    {
                        var clock=Stopwatch.StartNew();
                        try
                        {
                            var input=await payloads.ReadJsonAsync(test.InputPayloadReference,stoppingToken);var output=await provider.InterpretAsync(new(run.TenantId??Guid.Empty,run.RunId,run.RunId,Guid.Empty,"PROMPT_EVALUATION",run.PromptCode,run.PromptVersion,run.SystemPrompt,run.OutputSchemaJson,input,run.CorrelationId),stoppingToken);var actual=await payloads.SaveJsonAsync(run.TenantId??Guid.Empty,run.RunId,"prompt-evaluation",output.OutputJson,stoppingToken);var score=Score(test.ExpectedOutputJson,output.OutputJson,test.EvaluationRulesJson);results.Add(new(test.CaseId,score>=0.999m?"PASSED":"FAILED",score,actual,score>=0.999m?null:BuildDifference(test.ExpectedOutputJson,output.OutputJson),null,null,clock.ElapsedMilliseconds));
                        }
                        catch(Exception ex){results.Add(new(test.CaseId,"ERROR",0,null,null,ex.GetType().Name,ex.Message,clock.ElapsedMilliseconds));}
                    }
                    await repository.CompletePromptEvaluationAsync(run.RunId,results,stoppingToken);
                }
            }
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception ex){logger.LogError(ex,"Document intake prompt evaluation cycle failed.");}
            await Task.Delay(TimeSpan.FromSeconds(30),stoppingToken);
        }
    }

    private static decimal Score(string expected,string actual,string rules)
    {
        using var expectedDocument=JsonDocument.Parse(expected);using var actualDocument=JsonDocument.Parse(actual);using var ruleDocument=JsonDocument.Parse(rules);var required=ruleDocument.RootElement.TryGetProperty("requiredPaths",out var paths)?paths.EnumerateArray().Select(path=>path.GetString()).Where(path=>!string.IsNullOrWhiteSpace(path)).Cast<string>().ToArray():[];if(required.Length==0)return JsonElement.DeepEquals(expectedDocument.RootElement,actualDocument.RootElement)?1m:0m;var matched=required.Count(path=>TryGet(expectedDocument.RootElement,path,out var expectedValue)&&TryGet(actualDocument.RootElement,path,out var actualValue)&&JsonElement.DeepEquals(expectedValue,actualValue));return decimal.Round((decimal)matched/required.Length,4);
    }
    private static bool TryGet(JsonElement root,string path,out JsonElement value){value=root;foreach(var part in path.Split('.',StringSplitOptions.RemoveEmptyEntries))if(value.ValueKind!=JsonValueKind.Object||!value.TryGetProperty(part,out value))return false;return true;}
    private static string BuildDifference(string expected,string actual)=>JsonSerializer.Serialize(new{expected=JsonDocument.Parse(expected).RootElement,actual=JsonDocument.Parse(actual).RootElement});
}
