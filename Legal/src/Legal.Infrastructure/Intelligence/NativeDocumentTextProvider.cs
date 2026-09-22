using System.Text;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;

namespace Legal.Infrastructure.Intelligence;

public sealed class NativeDocumentTextProvider(IOptions<DocumentIntelligenceOptions> options) : INativeDocumentTextProvider
{
    public async Task<DocumentExtractionResult?> TryExtractAsync(DocumentExtractionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(request.Content, Encoding.UTF8, true, 81920, leaveOpen: true);
            var text = await reader.ReadToEndAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(text) ? null : Result([Page(1, text, true)], []);
        }

        if (!request.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) &&
            !Path.GetExtension(request.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!request.Content.CanSeek)
            return null;
        request.Content.Position = 0;
        using var document = PdfDocument.Open(request.Content);
        var extractedPages = document.GetPages()
            .Select(page => (page.Number, Text: page.Text ?? string.Empty))
            .ToArray();
        var pages = extractedPages
            .Where(page => IsReliable(page.Text))
            .Select(page => Page(page.Number, page.Text, true))
            .ToArray();
        var fallbackPages = extractedPages
            .Where(page => !IsReliable(page.Text))
            .Select(page => page.Number)
            .ToArray();
        request.Content.Position = 0;
        return extractedPages.Length == 0 ? null : Result(pages, fallbackPages);
    }

    private bool IsReliable(string text)
    {
        var nonWhitespace = text.Count(character => !char.IsWhiteSpace(character));
        if (nonWhitespace < Math.Max(1, options.Value.NativeTextMinimumCharactersPerPage))
            return false;
        var readable = text.Count(character => !char.IsWhiteSpace(character) && !char.IsControl(character) &&
            (char.IsLetterOrDigit(character) || char.IsPunctuation(character) || char.IsSymbol(character)));
        return (decimal)readable / nonWhitespace >= Math.Clamp(options.Value.NativeTextMinimumReadableCharacterRatio, 0m, 1m);
    }

    private static DocumentExtractedPage Page(int number, string text, bool reliable) => new(
        number, text, LegalDocumentExtractionMethods.NativeText, 1m, null, null, null,
        [new DocumentExtractedParagraph(1, text, null, null, 1m)], IsNativeTextReliable: reliable);

    private static DocumentExtractionResult Result(IReadOnlyCollection<DocumentExtractedPage> pages, IReadOnlyCollection<int> fallbackPages) => new(
        LegalDocumentExtractionMethods.NativeText, "native-text", "1", DateTime.UtcNow,
        pages, [], [], $"native:{Guid.NewGuid():N}", fallbackPages);
}
