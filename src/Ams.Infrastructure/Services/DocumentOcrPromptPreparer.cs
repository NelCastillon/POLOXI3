using System.Text;
using System.Text.Json;

namespace Ams.Infrastructure.Services;

internal static class DocumentOcrPromptPreparer
{
    internal static IReadOnlyList<string> PrepareChunks(string ocrJson, int maximumChunkCharacters)
    {
        if (maximumChunkCharacters <= 0)
            throw new AiSafetyViolationException("The configured AI input limit leaves no room for OCR text.");

        var pages = ExtractPages(ocrJson);
        var chunks = new List<string>();
        var current = new StringBuilder();

        foreach (var page in pages)
        {
            foreach (var segment in SplitPage(page.PageNumber, page.Text, maximumChunkCharacters))
            {
                var separatorLength = current.Length == 0 ? 0 : 2;
                if (current.Length > 0 && current.Length + separatorLength + segment.Length > maximumChunkCharacters)
                {
                    chunks.Add(current.ToString());
                    current.Clear();
                }

                if (current.Length > 0)
                    current.AppendLine().AppendLine();

                current.Append(segment);
            }
        }

        if (current.Length > 0)
            chunks.Add(current.ToString());

        return chunks.Count == 0 ? ["[Page 1]\nNo OCR text was extracted."] : chunks;
    }

    private static IReadOnlyList<OcrPageText> ExtractPages(string ocrJson)
    {
        try
        {
            using var document = JsonDocument.Parse(ocrJson);
            var root = document.RootElement;
            var analyzeResult = root.TryGetProperty("analyzeResult", out var analyze) ? analyze : root;
            var content = analyzeResult.TryGetProperty("content", out var contentNode) ? contentNode.GetString() ?? string.Empty : string.Empty;
            var pages = new List<OcrPageText>();

            if (analyzeResult.TryGetProperty("pages", out var pageArray) && pageArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var page in pageArray.EnumerateArray())
                {
                    var pageNumber = page.TryGetProperty("pageNumber", out var numberNode) && numberNode.TryGetInt32(out var number)
                        ? number
                        : pages.Count + 1;
                    var text = ExtractPageText(page, content);
                    if (!string.IsNullOrWhiteSpace(text))
                        pages.Add(new(pageNumber, NormalizeWhitespace(text)));
                }
            }

            if (pages.Count > 0)
                return pages;

            if (!string.IsNullOrWhiteSpace(content))
                return [new(1, NormalizeWhitespace(content))];
        }
        catch (JsonException)
        {
        }

        return [new(1, NormalizeWhitespace(ocrJson))];
    }

    private static string ExtractPageText(JsonElement page, string content)
    {
        if (!string.IsNullOrEmpty(content) && page.TryGetProperty("spans", out var spans) && spans.ValueKind == JsonValueKind.Array)
        {
            var text = new StringBuilder();
            foreach (var span in spans.EnumerateArray())
            {
                if (!span.TryGetProperty("offset", out var offsetNode) || !offsetNode.TryGetInt32(out var offset) ||
                    !span.TryGetProperty("length", out var lengthNode) || !lengthNode.TryGetInt32(out var length) ||
                    offset < 0 || length <= 0 || offset >= content.Length)
                    continue;

                var safeLength = Math.Min(length, content.Length - offset);
                if (text.Length > 0)
                    text.Append(' ');
                text.Append(content, offset, safeLength);
            }

            if (text.Length > 0)
                return text.ToString();
        }

        if (page.TryGetProperty("words", out var words) && words.ValueKind == JsonValueKind.Array)
            return string.Join(' ', words.EnumerateArray()
                .Select(word => word.TryGetProperty("content", out var value) ? value.GetString() : null)
                .Where(value => !string.IsNullOrWhiteSpace(value)));

        return string.Empty;
    }

    private static IEnumerable<string> SplitPage(int pageNumber, string text, int maximumChunkCharacters)
    {
        var firstHeader = $"[Page {pageNumber}]\n";
        if (firstHeader.Length + text.Length <= maximumChunkCharacters)
        {
            yield return firstHeader + text;
            yield break;
        }

        var part = 1;
        var offset = 0;
        while (offset < text.Length)
        {
            var header = $"[Page {pageNumber}, part {part}]\n";
            var available = maximumChunkCharacters - header.Length;
            if (available <= 0)
                throw new AiSafetyViolationException("The configured AI input limit is too small for page-aware OCR text.");

            var length = Math.Min(available, text.Length - offset);
            if (offset + length < text.Length)
            {
                var boundary = text.LastIndexOf(' ', offset + length - 1, length);
                if (boundary >= offset)
                    length = boundary - offset;
            }

            if (length <= 0)
                length = Math.Min(available, text.Length - offset);

            yield return header + text.Substring(offset, length).Trim();
            offset += length;
            while (offset < text.Length && char.IsWhiteSpace(text[offset]))
                offset++;
            part++;
        }
    }

    private static string NormalizeWhitespace(string value)
    {
        var normalized = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = normalized.Length > 0;
                continue;
            }

            if (pendingSpace)
                normalized.Append(' ');
            normalized.Append(character);
            pendingSpace = false;
        }

        return normalized.ToString().Trim();
    }

    private sealed record OcrPageText(int PageNumber, string Text);
}
