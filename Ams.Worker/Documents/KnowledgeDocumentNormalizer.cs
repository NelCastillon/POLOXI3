using Ams.Application.Abstractions.Services;
using Ams.Application.Features.DocumentIntake;
using Ams.Knowledge.Contracts.Concepts;

namespace Ams.Worker.Documents;

public sealed class KnowledgeDocumentNormalizer : IDocumentKnowledgeNormalizer
{
    private readonly IConceptResolver _resolver;
    public KnowledgeDocumentNormalizer(IConceptResolver resolver)=>_resolver=resolver;

    public async Task<IReadOnlyCollection<KnowledgeNormalizedField>> NormalizeAsync(KnowledgeNormalizationRequest request,CancellationToken cancellationToken=default)
    {
        var results=new List<KnowledgeNormalizedField>(request.Fields.Count);
        foreach(var field in request.Fields)
        {
            var scheme=ResolveScheme(field.Path);
            if(string.IsNullOrWhiteSpace(field.Value)||scheme is null){results.Add(new(field.EntityTypeCode,field.EntityKey,field.Path,field.Value,field.Value,null,"NOT_APPLICABLE",field.Confidence));continue;}
            var resolution=await _resolver.ResolveAsync(new(field.Value,scheme,null,null,State(field,request.Fields),null,request.TenantId),cancellationToken);
            var selected=resolution.Selected;results.Add(new(field.EntityTypeCode,field.EntityKey,field.Path,field.Value,selected?.PreferredLabel??field.Value,selected?.ConceptId,resolution.Resolved?"RESOLVED":resolution.RequiresReview?"REVIEW_REQUIRED":"UNMAPPED",selected?.Confidence??field.Confidence));
        }
        return results;
    }

    private static string? ResolveScheme(string path)
    {
        var value=path.ToUpperInvariant();
        if(value.Contains("LINEOFBUSINESS")||value.EndsWith(".LOB",StringComparison.Ordinal))return "LINE_OF_BUSINESS";
        if(value.Contains("COVERAGE"))return "COVERAGE";
        if(value.Contains("INDUSTRY")||value.Contains("NAICS"))return "INDUSTRY";
        if(value.EndsWith(".STATE",StringComparison.Ordinal)||value.Contains("STATECODE"))return "JURISDICTION";
        if(value.Contains("DOCUMENTTYPE"))return "DOCUMENT_TYPE";
        if(value.Contains("CARRIER"))return "CARRIER";
        return null;
    }

    private static string? State(ExtractedDocumentField current,IReadOnlyCollection<ExtractedDocumentField> fields)
        => current.Path.Contains("state",StringComparison.OrdinalIgnoreCase)?current.Value:fields.FirstOrDefault(field=>field.Path.EndsWith(".state",StringComparison.OrdinalIgnoreCase)||field.Path.EndsWith(".stateCode",StringComparison.OrdinalIgnoreCase))?.Value;
}
