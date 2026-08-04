using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.DocumentIntake;
using Ams.Infrastructure.Services;
using System.Diagnostics;

namespace Ams.Worker.Documents;

public interface IDocumentIntakeProcessor
{
    Task<DocumentIntakeBatchResult> ProcessBatchAsync(string leaseOwner,CancellationToken cancellationToken=default);
}

public sealed record DocumentIntakeBatchResult(int Completed,int Retried,int Failed);

public sealed class DocumentIntakeProcessor : IDocumentIntakeProcessor
{
    private readonly IDocumentIntakeRepository _repository;private readonly IDocumentIntakeOperationsRepository _operations;private readonly IDocumentStorageService _storage;private readonly IDocumentIntakePayloadStore _payloads;private readonly IDocumentOcrProvider _ocr;private readonly IDocumentInterpretationProvider _interpretation;private readonly IDocumentKnowledgeNormalizer _normalizer;private readonly IDocumentSearchIndexer _search;
    public DocumentIntakeProcessor(IDocumentIntakeRepository repository,IDocumentIntakeOperationsRepository operations,IDocumentStorageService storage,IDocumentIntakePayloadStore payloads,IDocumentOcrProvider ocr,IDocumentInterpretationProvider interpretation,IDocumentKnowledgeNormalizer normalizer,IDocumentSearchIndexer search){_repository=repository;_operations=operations;_storage=storage;_payloads=payloads;_ocr=ocr;_interpretation=interpretation;_normalizer=normalizer;_search=search;}

    public async Task<DocumentIntakeBatchResult> ProcessBatchAsync(string leaseOwner,CancellationToken cancellationToken=default)
    {
        var completed=0;var retried=0;var failed=0;var items=await _repository.LeaseWorkItemsAsync(leaseOwner,10,TimeSpan.FromMinutes(5),cancellationToken);
        foreach(var item in items)
        {
            var started=Stopwatch.GetTimestamp();
            DocumentIntakeTelemetry.ActiveWork.Add(1,new KeyValuePair<string,object?>("document_intake.work_type",item.WorkTypeCode));
            using var activity=DocumentIntakeTelemetry.ActivitySource.StartActivity("document-intake.process",ActivityKind.Internal);
            activity?.SetTag("document_intake.session_id",item.IntakeSessionId).SetTag("document_intake.work_item_id",item.IntakeWorkItemId).SetTag("document_intake.work_type",item.WorkTypeCode).SetTag("document_intake.attempt",item.AttemptCount);
            try{var context=await _repository.GetProcessingContextAsync(item.IntakeWorkItemId,leaseOwner,cancellationToken)??throw new DocumentAiProviderException("INTAKE_CONTEXT_MISSING","Leased work item context was not found.",false);activity?.SetTag("document_intake.module",context.Session.ModuleCode).SetTag("tenant.id",context.Session.TenantId);await ProcessAsync(context,cancellationToken);await _repository.CompleteWorkItemAsync(item.IntakeWorkItemId,leaseOwner,cancellationToken);DocumentIntakeTelemetry.WorkCompleted.Add(1,DocumentIntakeTelemetry.Tags(context.Session.ModuleCode,item.WorkTypeCode));completed++;}
            catch(OperationCanceledException)when(cancellationToken.IsCancellationRequested){throw;}
            catch(DocumentAiProviderException ex){activity?.SetStatus(ActivityStatusCode.Error,ex.Code).SetTag("error.type",ex.Code);await _repository.FailWorkItemAsync(item.IntakeWorkItemId,leaseOwner,ex.Code,ex.Message,ex.Retryable,cancellationToken);if(ex.Retryable){DocumentIntakeTelemetry.WorkRetried.Add(1);retried++;}else{DocumentIntakeTelemetry.WorkFailed.Add(1);failed++;}}
            catch(Exception ex){activity?.SetStatus(ActivityStatusCode.Error,ex.GetType().Name);await _repository.FailWorkItemAsync(item.IntakeWorkItemId,leaseOwner,"UNHANDLED_PROCESSING_ERROR",ex.Message,true,cancellationToken);DocumentIntakeTelemetry.WorkRetried.Add(1);retried++;}
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
            case DocumentIntakeWorkTypes.Validation:await _repository.ValidateDraftAsync(context,token);break;
            case DocumentIntakeWorkTypes.SearchIndexing:await _search.IndexAsync(context,token);break;
            default:throw new DocumentAiProviderException("INTAKE_WORK_TYPE_UNSUPPORTED",$"Work type '{context.WorkItem.WorkTypeCode}' is unsupported.",false);
        }
    }

    private async Task OcrAsync(DocumentIntakeProcessingContext context,CancellationToken token)
    {
        if(context.Document is null||string.IsNullOrWhiteSpace(context.StoragePath))throw new DocumentAiProviderException("INTAKE_DOCUMENT_MISSING","OCR requires an attached evidence document.",false);var download=await _storage.DownloadAsync(context.StoragePath,token)??throw new DocumentAiProviderException("INTAKE_BLOB_MISSING","Evidence blob was not found.",false);await using var content=download.Content;var inputHash=await HashAsync(content,token);content.Position=0;var result=await _ocr.AnalyzeAsync(new(context.Session.TenantId,context.Session.IntakeSessionId,context.WorkItem.IntakeWorkItemId,context.Document.DocumentId,context.Document.FileName,download.ContentType,content,context.StoragePath,context.WorkItem.CorrelationId),token);var reference=await _payloads.SaveJsonAsync(context.Session.TenantId,context.Session.IntakeSessionId,"ocr",result.OutputJson,token);var settings=await _operations.GetSettingsAsync(context.Session.TenantId,token);await _operations.RegisterPayloadAsync(context.Session.TenantId,context.Session.IntakeSessionId,reference,"OCR",true,settings.PayloadRetentionDays,"DocumentIntakeWorker",context.WorkItem.CorrelationId,token);await _repository.SaveOcrResultAsync(context,result,reference,inputHash,Hash(result.OutputJson),token);
    }

    private async Task InterpretAsync(DocumentIntakeProcessingContext context,CancellationToken token)
    {
        if(string.IsNullOrWhiteSpace(context.OcrOutputReference))throw new DocumentAiProviderException("OCR_OUTPUT_MISSING","Interpretation requires retained OCR output.",false);if(string.IsNullOrWhiteSpace(context.PromptCode)||string.IsNullOrWhiteSpace(context.PromptVersion)||string.IsNullOrWhiteSpace(context.SystemPrompt)||string.IsNullOrWhiteSpace(context.OutputSchemaJson))throw new DocumentAiProviderException("APPROVED_PROMPT_MISSING","No active approved prompt exists for this processing stage.",false);await _operations.RecordPayloadAccessAsync(context.Session.TenantId,context.Session.IntakeSessionId,context.OcrOutputReference,"READ","WORKER","DocumentIntakeWorker","Interpret retained OCR payload.","ATTEMPTED",context.WorkItem.CorrelationId,token);var ocr=await _payloads.ReadJsonAsync(context.OcrOutputReference,token);await _operations.RecordPayloadAccessAsync(context.Session.TenantId,context.Session.IntakeSessionId,context.OcrOutputReference,"READ","WORKER","DocumentIntakeWorker","Interpret retained OCR payload.","SUCCEEDED",context.WorkItem.CorrelationId,token);var result=await _interpretation.InterpretAsync(new(context.Session.TenantId,context.Session.IntakeSessionId,context.WorkItem.IntakeWorkItemId,context.Document?.DocumentId??Guid.Empty,context.Session.ModuleCode,context.PromptCode,context.PromptVersion,context.SystemPrompt,context.OutputSchemaJson,ocr,context.WorkItem.CorrelationId),token);var reference=await _payloads.SaveJsonAsync(context.Session.TenantId,context.Session.IntakeSessionId,"interpretation",result.OutputJson,token);var settings=await _operations.GetSettingsAsync(context.Session.TenantId,token);await _operations.RegisterPayloadAsync(context.Session.TenantId,context.Session.IntakeSessionId,reference,"INTERPRETATION",true,settings.PayloadRetentionDays,"DocumentIntakeWorker",context.WorkItem.CorrelationId,token);await _repository.SaveInterpretationAsync(context,result,reference,Hash(ocr),Hash(result.OutputJson),token);
    }

    private async Task NormalizeAsync(DocumentIntakeProcessingContext context,CancellationToken token){var fields=await _repository.GetExtractedFieldsAsync(context.Session.TenantId,context.Session.IntakeSessionId,token);var normalized=await _normalizer.NormalizeAsync(new(context.Session.TenantId,context.Session.IntakeSessionId,context.Session.ModuleCode,fields,context.WorkItem.CorrelationId),token);await _repository.SaveNormalizedFieldsAsync(context,normalized,token);}
    private static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static async Task<string> HashAsync(Stream stream,CancellationToken token){var bytes=await SHA256.HashDataAsync(stream,token);return Convert.ToHexString(bytes).ToLowerInvariant();}
}
