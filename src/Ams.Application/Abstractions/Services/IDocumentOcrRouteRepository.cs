namespace Ams.Application.Abstractions.Services;

public interface IDocumentOcrRouteRepository
{
    Task<DocumentOcrRoute?> GetRouteAsync(Guid? tenantId, CancellationToken cancellationToken = default);
}

public sealed record DocumentOcrRoute(
    string Endpoint,
    string ModelId,
    string ApiVersion,
    string? CredentialReference,
    int TimeoutSeconds);
