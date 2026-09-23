using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Infrastructure.Intelligence;

public sealed class UnavailableNativeDocumentTextProvider : INativeDocumentTextProvider
{
    public Task<DocumentExtractionResult?> TryExtractAsync(DocumentExtractionRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult<DocumentExtractionResult?>(null);
}
