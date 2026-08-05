using System.Text.Json;
using Ams.Application.Abstractions.Intelligence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.DocumentIntake;

namespace Ams.Infrastructure.Services;

public sealed class AzureOpenAiDocumentInterpretationProvider(IAiProviderRouter router) : IDocumentInterpretationProvider
{
    public async Task<DocumentInterpretationResult> InterpretAsync(DocumentInterpretationRequest request,CancellationToken cancellationToken=default)
    {
        var context=new AiExecutionContext(request.ModuleCode,"DOCUMENT",request.DocumentId,$"DMS.IntakeSession/{request.IntakeSessionId}","DOCUMENT_OCR",request.DocumentId,$"DMS.IntakeSession/{request.IntakeSessionId}/Ocr",request.PromptCode);
        var result=await router.GenerateAsync(request.TenantId,request.PromptCode,request.SystemPrompt,$"Module: {request.ModuleCode}\nCorrelation: {request.CorrelationId}\nOCR JSON:\n{request.OcrJson}",request.OutputSchemaJson,request.CorrelationId,context,cancellationToken);
        var content=result.StructuredOutputJson??result.Content;using var output=JsonDocument.Parse(content);
        var classification=output.RootElement.TryGetProperty("classification",out var classificationNode)?JsonSerializer.Deserialize<DocumentClassificationOutput>(classificationNode.GetRawText())!:new(request.PromptCode=="DOCUMENT.CLASSIFICATION"?output.RootElement.GetProperty("documentTypeCode").GetString()??"UNKNOWN":"UNKNOWN",output.RootElement.TryGetProperty("confidence",out var c)?c.GetDecimal():0);
        var fields=output.RootElement.TryGetProperty("fields",out var fieldNode)?JsonSerializer.Deserialize<List<ExtractedDocumentField>>(fieldNode.GetRawText())??[]:[];var warnings=output.RootElement.TryGetProperty("warnings",out var warningNode)?JsonSerializer.Deserialize<List<ExtractedDocumentWarning>>(warningNode.GetRawText())??[]:[];
        return new(result.ProviderCode,result.ModelCode,request.PromptCode,request.PromptVersion,classification,fields,warnings,content,result.InputTokenCount,result.OutputTokenCount,(long)result.Duration.TotalMilliseconds);
    }
}
