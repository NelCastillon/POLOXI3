using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ams.Application.Abstractions.Intelligence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.DocumentIntake;

namespace Ams.Infrastructure.Services;

public sealed class AzureOpenAiDocumentInterpretationProvider(IAiProviderRouter router, IAiProviderRouteRepository routeRepository) : IDocumentInterpretationProvider
{
    public async Task<DocumentInterpretationResult> InterpretAsync(DocumentInterpretationRequest request,CancellationToken cancellationToken=default)
    {
        var context=new AiExecutionContext(request.ModuleCode,"DOCUMENT",request.DocumentId,$"DMS.IntakeSession/{request.IntakeSessionId}","DOCUMENT_OCR",request.DocumentId,$"DMS.IntakeSession/{request.IntakeSessionId}/Ocr",request.PromptCode);
        var safety=await routeRepository.GetSafetyPolicyAsync(request.TenantId,cancellationToken);
        var envelopeLength=BuildUserPrompt(request,string.Empty,999999,999999).Length;
        var availableCharacters=safety.MaximumInputCharacters-request.SystemPrompt.Length-envelopeLength-64;
        var chunks=DocumentOcrPromptPreparer.PrepareChunks(request.OcrJson,availableCharacters);
        var results=new List<ChunkResult>(chunks.Count);
        var inputHash=new IncrementalHashBuilder();

        for(var index=0;index<chunks.Count;index++)
        {
            var userPrompt=BuildUserPrompt(request,chunks[index],index+1,chunks.Count);
            inputHash.Append(userPrompt);
            var generated=await router.GenerateAsync(request.TenantId,request.PromptCode,request.SystemPrompt,userPrompt,request.OutputSchemaJson,request.CorrelationId,context,cancellationToken:cancellationToken);
            results.Add(ParseChunk(generated,chunks[index].Length,request.PromptCode));
        }

        var classification=AggregateClassification(results,request.PromptCode);
        var fields=AggregateFields(results);
        var warnings=AggregateWarnings(results);
        var outputJson=request.PromptCode=="DOCUMENT.CLASSIFICATION"
            ?JsonSerializer.Serialize(classification)
            :JsonSerializer.Serialize(new{fields,warnings});
        var first=results[0];
        return new(first.ProviderCode,first.ModelCode,request.PromptCode,request.PromptVersion,classification,fields,warnings,outputJson,SumTokens(results.Select(x=>x.InputTokenCount)),SumTokens(results.Select(x=>x.OutputTokenCount)),inputHash.GetHash(),results.Sum(x=>x.DurationMilliseconds));
    }

    private static string BuildUserPrompt(DocumentInterpretationRequest request,string chunk,int chunkNumber,int chunkCount)
        =>$"Module: {request.ModuleCode}\nCorrelation: {request.CorrelationId}\nOCR text chunk {chunkNumber} of {chunkCount}:\n{chunk}";

    private static readonly JsonSerializerOptions LenientOptions=new(){Converters={new LenientStringConverter()}};

    private sealed class LenientStringConverter:JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader,Type typeToConvert,JsonSerializerOptions options)=>reader.TokenType switch
        {
            JsonTokenType.String=>reader.GetString(),
            JsonTokenType.Null=>null,
            JsonTokenType.True=>"true",
            JsonTokenType.False=>"false",
            JsonTokenType.Number=>reader.TryGetInt64(out var integer)?integer.ToString(System.Globalization.CultureInfo.InvariantCulture):reader.GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture),
            _=>JsonDocument.ParseValue(ref reader).RootElement.GetRawText()
        };
        public override void Write(Utf8JsonWriter writer,string? value,JsonSerializerOptions options){if(value is null)writer.WriteNullValue();else writer.WriteStringValue(value);}
    }

    private static ChunkResult ParseChunk(AiGenerationResult result,int inputLength,string promptCode)
    {
        var content=result.StructuredOutputJson??result.Content;
        using var output=JsonDocument.Parse(content);
        var root=output.RootElement;
        var classificationNode=root.TryGetProperty("classification",out var nestedClassification)?nestedClassification:root;
        var classification=new DocumentClassificationOutput(
            promptCode=="DOCUMENT.CLASSIFICATION"&&classificationNode.TryGetProperty("documentTypeCode",out var typeNode)?typeNode.GetString()??"UNKNOWN":"UNKNOWN",
            classificationNode.TryGetProperty("confidence",out var confidenceNode)&&confidenceNode.TryGetDecimal(out var confidence)?confidence:0);
        var fields=root.TryGetProperty("fields",out var fieldNode)?JsonSerializer.Deserialize<List<ExtractedDocumentField>>(fieldNode.GetRawText(),LenientOptions)??[]:[];
        var warnings=ParseWarnings(root);
        return new(result.ProviderCode,result.ModelCode,classification,fields,warnings,inputLength,result.InputTokenCount,result.OutputTokenCount,(long)result.Duration.TotalMilliseconds);
    }

    private static IReadOnlyList<ExtractedDocumentWarning> ParseWarnings(JsonElement root)
    {
        if(!root.TryGetProperty("warnings",out var warningNode)||warningNode.ValueKind!=JsonValueKind.Array)return [];
        var warnings=new List<ExtractedDocumentWarning>();
        foreach(var warning in warningNode.EnumerateArray())
        {
            if(warning.ValueKind==JsonValueKind.String)
            {
                warnings.Add(new("DOCUMENT_AI_WARNING","WARNING",null,warning.GetString()??string.Empty));
                continue;
            }
            var parsed=JsonSerializer.Deserialize<ExtractedDocumentWarning>(warning.GetRawText());
            if(parsed is not null)warnings.Add(parsed);
        }
        return warnings;
    }

    private static DocumentClassificationOutput AggregateClassification(IReadOnlyCollection<ChunkResult> results,string promptCode)
    {
        if(promptCode!="DOCUMENT.CLASSIFICATION")return new("UNKNOWN",0);
        var totalWeight=results.Sum(x=>(long)Math.Max(1,x.InputLength));
        var winner=results
            .GroupBy(x=>x.Classification.DocumentTypeCode,StringComparer.OrdinalIgnoreCase)
            .Select(group=>new
            {
                DocumentTypeCode=group.Key,
                Score=group.Sum(x=>x.Classification.Confidence*Math.Max(1,x.InputLength))
            })
            .OrderByDescending(x=>x.Score)
            .ThenBy(x=>x.DocumentTypeCode,StringComparer.OrdinalIgnoreCase)
            .First();
        return new(winner.DocumentTypeCode,totalWeight==0?0:Math.Clamp(winner.Score/totalWeight,0,1));
    }

    private static IReadOnlyList<ExtractedDocumentField> AggregateFields(IEnumerable<ChunkResult> results)
        =>results.SelectMany(x=>x.Fields)
            .GroupBy(x=>(x.EntityTypeCode,x.EntityKey,x.Path))
            .Select(group=>group.OrderByDescending(x=>x.Confidence).ThenByDescending(x=>x.SourcePage.HasValue).First())
            .ToArray();

    private static IReadOnlyList<ExtractedDocumentWarning> AggregateWarnings(IEnumerable<ChunkResult> results)
        =>results.SelectMany(x=>x.Warnings)
            .DistinctBy(x=>(x.Code,x.SeverityCode,x.FieldPath,x.Message))
            .ToArray();

    private static int? SumTokens(IEnumerable<int?> values)
    {
        var materialized=values.ToArray();
        return materialized.Any(x=>x.HasValue)?materialized.Sum(x=>x??0):null;
    }

    private sealed record ChunkResult(string ProviderCode,string ModelCode,DocumentClassificationOutput Classification,IReadOnlyList<ExtractedDocumentField> Fields,IReadOnlyList<ExtractedDocumentWarning> Warnings,int InputLength,int? InputTokenCount,int? OutputTokenCount,long DurationMilliseconds);

    private sealed class IncrementalHashBuilder
    {
        private readonly IncrementalHash _hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        public void Append(string value)=>_hash.AppendData(Encoding.UTF8.GetBytes(value));
        public string GetHash()=>Convert.ToHexString(_hash.GetHashAndReset()).ToLowerInvariant();
    }
}
