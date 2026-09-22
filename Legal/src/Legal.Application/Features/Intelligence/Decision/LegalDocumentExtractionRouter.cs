using Legal.Application.Abstractions.Intelligence;

namespace Legal.Application.Features.Intelligence.Decision;

public sealed class LegalDocumentExtractionRouter(
    INativeDocumentTextProvider nativeTextProvider,
    IDocumentExtractionProvider layoutProvider) : ILegalDocumentExtractionRouter
{
    public async Task<DocumentExtractionResult> ExtractAsync(DocumentExtractionRequest request, CancellationToken cancellationToken = default)
    {
        var native = request.PreferNativeText
            ? await nativeTextProvider.TryExtractAsync(request, cancellationToken)
            : null;
        if (native is null)
        {
            Reset(request.Content);
            return await layoutProvider.ExtractAsync(request, cancellationToken);
        }

        if (native.PagesRequiringFallback.Count == 0)
            return native;

        Reset(request.Content);
        var fallback = await layoutProvider.ExtractAsync(
            request with { PageNumbers = native.PagesRequiringFallback }, cancellationToken);
        return Merge(native, fallback);
    }

    private static DocumentExtractionResult Merge(DocumentExtractionResult native, DocumentExtractionResult fallback)
    {
        var fallbackPages = fallback.Pages.Where(HasUsableText).ToDictionary(page => page.PageNumber);
        var unresolved = native.PagesRequiringFallback.Where(page => !fallbackPages.ContainsKey(page)).ToArray();
        if (unresolved.Length > 0)
            throw new InvalidDataException($"Document extraction produced no usable content for page(s): {string.Join(", ", unresolved)}.");

        return new DocumentExtractionResult(
            "HYBRID_NATIVE_AZURE", $"{native.ModelCode}+{fallback.ModelCode}", fallback.ModelVersion, DateTime.UtcNow,
            native.Pages.Concat(fallbackPages.Values.Where(page => native.Pages.All(nativePage => nativePage.PageNumber != page.PageNumber)))
                .OrderBy(page => page.PageNumber).ToArray(),
            fallback.Sections, fallback.Tables, fallback.RawResultReference, [], fallback.ExtractedFigures);
    }

    private static bool HasUsableText(DocumentExtractedPage page) =>
        !string.IsNullOrWhiteSpace(page.Text) || page.Paragraphs.Any(paragraph => !string.IsNullOrWhiteSpace(paragraph.Text));

    private static void Reset(Stream content)
    {
        if (!content.CanSeek)
            throw new InvalidDataException("Extraction routing requires a seekable stream.");
        content.Position = 0;
    }
}