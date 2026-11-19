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

    private DocumentExtractedPage Page(int number, string text, bool reliable) => new(
        number, text, LegalDocumentExtractionMethods.NativeText, 1m, null, null, null,
        BuildParagraphs(text), IsNativeTextReliable: reliable);

    // Splits a page's native text into paragraph-level passages on blank-line boundaries so each unit of
    // evidence is anchored to a discrete paragraph rather than an entire page. Lines are regrouped into
    // blocks separated by one or more blank lines; blocks shorter than the configured minimum are merged
    // into the previous block to avoid fragmenting on stray line breaks. When paragraph splitting yields a
    // single block (or is disabled via a 0 threshold) the whole page remains one passage.
    private IReadOnlyCollection<DocumentExtractedParagraph> BuildParagraphs(string text)
    {
        var minimum = options.Value.NativeTextMinimumCharactersPerParagraph;
        if (minimum <= 0 || string.IsNullOrWhiteSpace(text))
            return [new DocumentExtractedParagraph(1, text, null, null, 1m)];

        var blocks = new List<string>();
        var current = new StringBuilder();
        foreach (var rawLine in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                FlushBlock(blocks, current);
                continue;
            }
            if (current.Length > 0)
                current.Append('\n');
            current.Append(rawLine.TrimEnd());
        }
        FlushBlock(blocks, current);

        // Merge blocks that fall under the minimum length into the previous block so short headings or
        // stray wrapped lines stay attached to the paragraph they belong to.
        var merged = new List<string>();
        foreach (var block in blocks)
        {
            if (merged.Count > 0 && CountNonWhitespace(block) < minimum)
                merged[^1] = merged[^1] + "\n" + block;
            else
                merged.Add(block);
        }

        if (merged.Count <= 1)
            return [new DocumentExtractedParagraph(1, text, null, null, 1m)];

        var sequence = 0;
        return merged
            .Select(block => new DocumentExtractedParagraph(++sequence, block, null, null, 1m))
            .ToArray();
    }

    private static void FlushBlock(List<string> blocks, StringBuilder current)
    {
        if (current.Length == 0)
            return;
        var block = current.ToString().Trim();
        if (block.Length > 0)
            blocks.Add(block);
        current.Clear();
    }

    private static int CountNonWhitespace(string text) => text.Count(character => !char.IsWhiteSpace(character));

    private static DocumentExtractionResult Result(IReadOnlyCollection<DocumentExtractedPage> pages, IReadOnlyCollection<int> fallbackPages) => new(
        LegalDocumentExtractionMethods.NativeText, "native-text", "1", DateTime.UtcNow,
        pages, [], [], $"native:{Guid.NewGuid():N}", fallbackPages);
}
