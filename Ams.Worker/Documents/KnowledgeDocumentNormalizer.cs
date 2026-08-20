using Ams.Application.Abstractions.Services;
using Ams.Application.Features.DocumentIntake;
using Ams.Knowledge.Application.Abstractions.Persistence;
using Ams.Knowledge.Contracts.Concepts;

namespace Ams.Worker.Documents;

public sealed class KnowledgeDocumentNormalizer : IDocumentKnowledgeNormalizer
{
    private readonly IConceptResolver _resolver;
    private readonly IKnowledgeDocumentRoutingProvider _routing;
    public KnowledgeDocumentNormalizer(IConceptResolver resolver,IKnowledgeDocumentRoutingProvider routing){_resolver=resolver;_routing=routing;}

    public async Task<IReadOnlyCollection<KnowledgeNormalizedField>> NormalizeAsync(KnowledgeNormalizationRequest request,CancellationToken cancellationToken=default)
    {
        var results=new List<KnowledgeNormalizedField>(request.Fields.Count);
        var routes=await _routing.GetRoutesAsync(request.TenantId,cancellationToken);
        foreach(var field in request.Fields)
        {
            var scheme=ResolveScheme(field.Path,routes);
            if(string.IsNullOrWhiteSpace(field.Value)||scheme is null){results.Add(new(field.EntityTypeCode,field.EntityKey,field.Path,field.Value,field.Value,null,"NOT_APPLICABLE",field.Confidence));continue;}
            var resolution=await _resolver.ResolveAsync(new(field.Value,scheme,null,null,State(field,request.Fields),null,request.TenantId),cancellationToken);
            var selected=resolution.Selected;results.Add(new(field.EntityTypeCode,field.EntityKey,field.Path,field.Value,selected?.PreferredLabel??field.Value,selected?.ConceptId,resolution.Resolved?"RESOLVED":resolution.RequiresReview?"REVIEW_REQUIRED":"UNMAPPED",selected?.Confidence??field.Confidence));
        }
        return results;
    }

    private static string? ResolveScheme(string path,IReadOnlyCollection<DocumentFieldSchemeRoute> routes)
    {
        return routes.FirstOrDefault(route=>(!string.IsNullOrWhiteSpace(route.PathContains)&&path.Contains(route.PathContains,StringComparison.OrdinalIgnoreCase))||(!string.IsNullOrWhiteSpace(route.PathSuffix)&&path.EndsWith(route.PathSuffix,StringComparison.OrdinalIgnoreCase)))?.SchemeCode;
    }

    private static string? State(ExtractedDocumentField current,IReadOnlyCollection<ExtractedDocumentField> fields)
        => current.Path.Contains("state",StringComparison.OrdinalIgnoreCase)?current.Value:fields.FirstOrDefault(field=>field.Path.EndsWith(".state",StringComparison.OrdinalIgnoreCase)||field.Path.EndsWith(".stateCode",StringComparison.OrdinalIgnoreCase))?.Value;
}
