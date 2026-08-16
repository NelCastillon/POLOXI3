using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ams.Application.Abstractions.Intelligence;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.DocumentIntake;
using Ams.Application.Features.Platform;
using Ams.Infrastructure.Services;
using System.Diagnostics;

namespace Ams.Worker.Documents;

public interface IDocumentIntakeProcessor
{
    Task<DocumentIntakeBatchResult> ProcessBatchAsync(string leaseOwner,int batchSize,TimeSpan leaseDuration,bool malwareEnabled,bool malwareFailClosed,CancellationToken cancellationToken=default);
}

public sealed record DocumentIntakeBatchResult(int Completed,int Retried,int Failed);

public sealed class DocumentIntakeProcessor : IDocumentIntakeProcessor
{
    private readonly IDocumentIntakeRepository _repository;private readonly IDocumentIntakeOperationsRepository _operations;private readonly IDocumentStorageService _storage;private readonly IDocumentIntakePayloadStore _payloads;private readonly IDocumentOcrProvider _ocr;private readonly IDocumentInterpretationProvider _interpretation;private readonly IDocumentKnowledgeNormalizer _normalizer;private readonly IDocumentSearchIndexer _search;private readonly IRulesPlatformService _rules;private readonly IValidationPlatformService _validation;private readonly ILogger<DocumentIntakeProcessor> _logger;
    public DocumentIntakeProcessor(IDocumentIntakeRepository repository,IDocumentIntakeOperationsRepository operations,IDocumentStorageService storage,IDocumentIntakePayloadStore payloads,IDocumentOcrProvider ocr,IDocumentInterpretationProvider interpretation,IDocumentKnowledgeNormalizer normalizer,IDocumentSearchIndexer search,IRulesPlatformService rules,IValidationPlatformService validation,ILogger<DocumentIntakeProcessor> logger){_repository=repository;_operations=operations;_storage=storage;_payloads=payloads;_ocr=ocr;_interpretation=interpretation;_normalizer=normalizer;_search=search;_rules=rules;_validation=validation;_logger=logger;}

    public async Task<DocumentIntakeBatchResult> ProcessBatchAsync(string leaseOwner,int batchSize,TimeSpan leaseDuration,bool malwareEnabled,bool malwareFailClosed,CancellationToken cancellationToken=default)
    {
        var completed=0;var retried=0;var failed=0;var items=await _repository.LeaseWorkItemsAsync(leaseOwner,Math.Clamp(batchSize,1,100),leaseDuration,malwareEnabled,malwareFailClosed,cancellationToken);
        foreach(var item in items)
        {
            var started=Stopwatch.GetTimestamp();
            DocumentIntakeTelemetry.ActiveWork.Add(1,new KeyValuePair<string,object?>("document_intake.work_type",item.WorkTypeCode));
            using var activity=DocumentIntakeTelemetry.ActivitySource.StartActivity("document-intake.process",ActivityKind.Internal);
            activity?.SetTag("document_intake.session_id",item.IntakeSessionId).SetTag("document_intake.work_item_id",item.IntakeWorkItemId).SetTag("document_intake.work_type",item.WorkTypeCode).SetTag("document_intake.attempt",item.AttemptCount);
            try{var context=await RunOcrStageAsync("INTAKE_CONTEXT_LOAD_FAILED","Processing context load",()=>_repository.GetProcessingContextAsync(item.IntakeWorkItemId,leaseOwner,cancellationToken))??throw new DocumentAiProviderException("INTAKE_CONTEXT_MISSING","Leased work item context was not found.",false);activity?.SetTag("document_intake.module",context.Session.ModuleCode).SetTag("tenant.id",context.Session.TenantId);await RunOcrStageAsync("INTAKE_STAGE_EXECUTION_FAILED",$"{item.WorkTypeCode} stage execution",()=>ProcessAsync(context,cancellationToken));await RunOcrStageAsync("INTAKE_WORK_ITEM_COMPLETE_FAILED","Work item completion",()=>_repository.CompleteWorkItemAsync(item.IntakeWorkItemId,leaseOwner,cancellationToken));DocumentIntakeTelemetry.WorkCompleted.Add(1,DocumentIntakeTelemetry.Tags(context.Session.ModuleCode,item.WorkTypeCode));completed++;}
            catch(OperationCanceledException)when(cancellationToken.IsCancellationRequested){throw;}
            catch(DocumentAiProviderException ex){activity?.SetStatus(ActivityStatusCode.Error,ex.Code).SetTag("error.type",ex.Code);await _repository.FailWorkItemAsync(item.IntakeWorkItemId,leaseOwner,ex.Code,ex.Message,ex.Retryable,cancellationToken);if(ex.Retryable){DocumentIntakeTelemetry.WorkRetried.Add(1);retried++;}else{DocumentIntakeTelemetry.WorkFailed.Add(1);failed++;}}
            catch(Exception ex){activity?.SetStatus(ActivityStatusCode.Error,ex.GetType().Name);_logger.LogError(ex,"Unhandled document intake failure for work item {WorkItemId} ({WorkTypeCode}).",item.IntakeWorkItemId,item.WorkTypeCode);await _repository.FailWorkItemAsync(item.IntakeWorkItemId,leaseOwner,"UNHANDLED_PROCESSING_ERROR",ex.ToString(),true,cancellationToken);DocumentIntakeTelemetry.WorkRetried.Add(1);retried++;}
            finally{DocumentIntakeTelemetry.ActiveWork.Add(-1,new KeyValuePair<string,object?>("document_intake.work_type",item.WorkTypeCode));DocumentIntakeTelemetry.WorkDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds,new KeyValuePair<string,object?>("document_intake.work_type",item.WorkTypeCode));}
        }
        return new(completed,retried,failed);
    }

    private async Task ProcessAsync(DocumentIntakeProcessingContext context,CancellationToken token)
    {
        switch(context.WorkItem.WorkTypeCode)
        {
            case DocumentIntakeWorkTypes.Ocr:await OcrAsync(context,token);break;
            case DocumentIntakeWorkTypes.Classification:
            case DocumentIntakeWorkTypes.Extraction:await InterpretAsync(context,token);break;
            case DocumentIntakeWorkTypes.KnowledgeMapping:await NormalizeAsync(context,token);break;
            case DocumentIntakeWorkTypes.Validation:await ValidateAsync(context,token);break;
            case DocumentIntakeWorkTypes.SearchIndexing:await _search.IndexAsync(context,token);break;
            default:throw new DocumentAiProviderException("INTAKE_WORK_TYPE_UNSUPPORTED",$"Work type '{context.WorkItem.WorkTypeCode}' is unsupported.",false);
        }
    }

    private async Task OcrAsync(DocumentIntakeProcessingContext context,CancellationToken token)
    {
        if(context.Document is null||string.IsNullOrWhiteSpace(context.StoragePath))throw new DocumentAiProviderException("INTAKE_DOCUMENT_MISSING","OCR requires an attached evidence document.",false);
        var download=await RunOcrStageAsync("INTAKE_BLOB_DOWNLOAD_FAILED","Blob download",()=>_storage.DownloadAsync(context.StoragePath,token))??throw new DocumentAiProviderException("INTAKE_BLOB_MISSING","Evidence blob was not found.",false);
        await using var source=download.Content;
        var capacity=download.FileSizeBytes is >0 and <=int.MaxValue?(int)download.FileSizeBytes.Value:0;
        await using var content=new MemoryStream(capacity);
        await RunOcrStageAsync("INTAKE_CONTENT_COPY_FAILED","Document stream copy",()=>source.CopyToAsync(content,token));
        content.Position=0;
        var inputHash=await RunOcrStageAsync("INTAKE_CONTENT_HASH_FAILED","Document hashing",()=>HashAsync(content,token));
        content.Position=0;
        var result=await RunOcrStageAsync("DOCUMENT_INTELLIGENCE_ANALYZE_FAILED","Document Intelligence analysis",()=>_ocr.AnalyzeAsync(new(context.Session.TenantId,context.Session.IntakeSessionId,context.WorkItem.IntakeWorkItemId,context.Document.DocumentId,context.Document.FileName,download.ContentType??"application/octet-stream",content,context.StoragePath,context.WorkItem.CorrelationId),token));
        var reference=await RunOcrStageAsync("INTAKE_OCR_PAYLOAD_SAVE_FAILED","OCR payload save",()=>_payloads.SaveJsonAsync(context.Session.TenantId,context.Session.IntakeSessionId,"ocr",result.OutputJson,token));
        var settings=await RunOcrStageAsync("INTAKE_SETTINGS_LOAD_FAILED","Intake settings load",()=>_operations.GetSettingsAsync(context.Session.TenantId,token));
        await RunOcrStageAsync("INTAKE_OCR_PAYLOAD_REGISTER_FAILED","OCR payload registration",()=>_operations.RegisterPayloadAsync(context.Session.TenantId,context.Session.IntakeSessionId,reference,"OCR",true,settings.PayloadRetentionDays,"DocumentIntakeWorker",context.WorkItem.CorrelationId,token));
        await RunOcrStageAsync("INTAKE_OCR_RESULT_SAVE_FAILED","OCR result persistence",()=>_repository.SaveOcrResultAsync(context,result,reference,inputHash,Hash(result.OutputJson),token));
    }

    private async Task InterpretAsync(DocumentIntakeProcessingContext context,CancellationToken token)
    {
        if(string.IsNullOrWhiteSpace(context.OcrOutputReference))throw new DocumentAiProviderException("OCR_OUTPUT_MISSING","Interpretation requires retained OCR output.",false);if(string.IsNullOrWhiteSpace(context.PromptCode)||string.IsNullOrWhiteSpace(context.PromptVersion)||string.IsNullOrWhiteSpace(context.SystemPrompt)||string.IsNullOrWhiteSpace(context.OutputSchemaJson))throw new DocumentAiProviderException("APPROVED_PROMPT_MISSING","No active approved prompt exists for this processing stage.",false);await _operations.RecordPayloadAccessAsync(context.Session.TenantId,context.Session.IntakeSessionId,context.OcrOutputReference,"READ","WORKER","DocumentIntakeWorker","Interpret retained OCR payload.","ATTEMPTED",context.WorkItem.CorrelationId,token);var ocr=await _payloads.ReadJsonAsync(context.Session.TenantId,context.Session.IntakeSessionId,context.OcrOutputReference,token);await _operations.RecordPayloadAccessAsync(context.Session.TenantId,context.Session.IntakeSessionId,context.OcrOutputReference,"READ","WORKER","DocumentIntakeWorker","Interpret retained OCR payload.","SUCCEEDED",context.WorkItem.CorrelationId,token);var result=await _interpretation.InterpretAsync(new(context.Session.TenantId,context.Session.IntakeSessionId,context.WorkItem.IntakeWorkItemId,context.Document?.DocumentId??Guid.Empty,context.Session.ModuleCode,context.PromptCode,context.PromptVersion,context.SystemPrompt,context.OutputSchemaJson,ocr,context.WorkItem.CorrelationId),token);var reference=await _payloads.SaveJsonAsync(context.Session.TenantId,context.Session.IntakeSessionId,"interpretation",result.OutputJson,token);var settings=await _operations.GetSettingsAsync(context.Session.TenantId,token);await _operations.RegisterPayloadAsync(context.Session.TenantId,context.Session.IntakeSessionId,reference,"INTERPRETATION",true,settings.PayloadRetentionDays,"DocumentIntakeWorker",context.WorkItem.CorrelationId,token);await _repository.SaveInterpretationAsync(context,result,reference,result.InputHashSha256,Hash(result.OutputJson),token);using var facts=JsonDocument.Parse(result.OutputJson);await _rules.EvaluateAsync(new(context.Session.TenantId,context.Session.ModuleCode,context.Session.TargetEntityId??context.Session.IntakeSessionId,context.Session.ModuleCode,$"{context.WorkItem.CorrelationId}:rules",facts.RootElement.Clone(),context.Session.CreatedByUserId),token);
    }

    private async Task NormalizeAsync(DocumentIntakeProcessingContext context,CancellationToken token){var fields=await _repository.GetExtractedFieldsAsync(context.Session.TenantId,context.Session.IntakeSessionId,token);var normalized=await _normalizer.NormalizeAsync(new(context.Session.TenantId,context.Session.IntakeSessionId,context.Session.ModuleCode,fields,context.WorkItem.CorrelationId),token);await _repository.SaveNormalizedFieldsAsync(context,normalized,token);}
    private async Task ValidateAsync(DocumentIntakeProcessingContext context,CancellationToken token){await _repository.ValidateDraftAsync(context,token);var fields=await _repository.GetExtractedFieldsAsync(context.Session.TenantId,context.Session.IntakeSessionId,token);var facts=JsonSerializer.SerializeToElement(new{fields});await _validation.ValidateAsync(new(context.Session.TenantId,context.Session.ModuleCode,context.Session.TargetEntityId??context.Session.IntakeSessionId,context.Session.ModuleCode,null,null,null,$"{context.WorkItem.CorrelationId}:validation",facts,context.Session.CreatedByUserId),token);}
    private static async Task<T> RunOcrStageAsync<T>(string code,string stage,Func<Task<T>> action){try{return await action();}catch(OperationCanceledException){throw;}catch(DocumentAiProviderException){throw;}catch(AiSafetyViolationException ex){throw new DocumentAiProviderException("AI_SAFETY_POLICY_VIOLATION",ex.Message,false);}catch(Exception ex){throw new DocumentAiProviderException(code,$"{stage} failed ({ex.GetType().Name}): {ex.Message}",true);}}
    private static async Task RunOcrStageAsync(string code,string stage,Func<Task> action){try{await action();}catch(OperationCanceledException){throw;}catch(DocumentAiProviderException){throw;}catch(AiSafetyViolationException ex){throw new DocumentAiProviderException("AI_SAFETY_POLICY_VIOLATION",ex.Message,false);}catch(Exception ex){throw new DocumentAiProviderException(code,$"{stage} failed ({ex.GetType().Name}): {ex.Message}",true);}}
    private static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static async Task<string> HashAsync(Stream stream,CancellationToken token){var bytes=await SHA256.HashDataAsync(stream,token);return Convert.ToHexString(bytes).ToLowerInvariant();}
}
