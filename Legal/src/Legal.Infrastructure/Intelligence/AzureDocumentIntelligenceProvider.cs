using System.Text.Json;
using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Identity;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Legal.Infrastructure.Intelligence;

public sealed class AzureDocumentIntelligenceProvider : IDocumentExtractionProvider
{
    private readonly DocumentIntelligenceOptions _options;

    public AzureDocumentIntelligenceProvider(IOptions<DocumentIntelligenceOptions> options)
    {
        _options = options.Value;
    }

    public async Task<DocumentExtractionResult> ExtractAsync(DocumentExtractionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request.Content);
        if (!request.Content.CanRead)
            throw new ArgumentException("The document content stream must be readable.", nameof(request));
        if (!Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var endpoint))
            throw new InvalidOperationException("DocumentIntelligence:Endpoint must be an absolute URI when Azure layout extraction is required.");
        var client = string.IsNullOrWhiteSpace(_options.ApiKey)
            ? new DocumentIntelligenceClient(endpoint, new DefaultAzureCredential())
            : new DocumentIntelligenceClient(endpoint, new AzureKeyCredential(_options.ApiKey));

        var content = await BinaryData.FromStreamAsync(request.Content, cancellationToken);
        var analyzeOptions = new AnalyzeDocumentOptions(_options.ModelId, content);
        if (request.PageNumbers is { Count: > 0 })
            analyzeOptions.Pages = ToPageRanges(request.PageNumbers);
        var operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, analyzeOptions, cancellationToken);
        var result = operation.Value;

        var paragraphsByPage = result.Paragraphs
            .Select(paragraph => new
            {
                Paragraph = paragraph,
                PageNumber = paragraph.BoundingRegions.Count == 0 ? (int?)null : paragraph.BoundingRegions[0].PageNumber
            })
            .Where(item => item.PageNumber.HasValue)
            .GroupBy(item => item.PageNumber!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<DocumentExtractedParagraph>)group.Select((item, index) =>
                    new DocumentExtractedParagraph(
                        index + 1,
                        item.Paragraph.Content,
                        item.Paragraph.Role?.ToString(),
                        SerializeBoundingRegions(item.Paragraph.BoundingRegions),
                        null,
                        SerializeSpans(item.Paragraph.Spans))).ToArray());

        var pages = result.Pages.Select(page => new DocumentExtractedPage(
            page.PageNumber,
            string.Join(Environment.NewLine, page.Lines.Select(line => line.Content)),
            LegalDocumentExtractionMethods.AzureDocumentIntelligence,
            AverageConfidence(page.Words.Select(word => word.Confidence)),
            Convert.ToDecimal(page.Width),
            Convert.ToDecimal(page.Height),
            page.Unit.ToString(),
            paragraphsByPage.GetValueOrDefault(page.PageNumber, []),
            page.Lines.Select((line, index) => new DocumentExtractedLine(
                index + 1, line.Content, SerializePolygon(line.Polygon), SerializeSpans(line.Spans))).ToArray(),
            page.Words.Select((word, index) => new DocumentExtractedWord(
                index + 1, word.Content, Convert.ToDecimal(word.Confidence), SerializePolygon(word.Polygon), SerializeSpan(word.Span))).ToArray(),
            page.SelectionMarks.Select((mark, index) => new DocumentExtractedSelectionMark(
                index + 1, mark.State.ToString(), Convert.ToDecimal(mark.Confidence), SerializePolygon(mark.Polygon), SerializeSpan(mark.Span))).ToArray(),
            false)).ToArray();

        var sections = result.Sections.Select((section, index) =>
        {
            var elements = section.Elements?.ToArray() ?? [];
            return new DocumentExtractedSection(
                $"section/{index + 1}",
                null,
                null,
                null,
                string.Join(Environment.NewLine, elements),
                SerializeSpans(section.Spans));
        }).ToArray();

        var tables = result.Tables.Select(table => new DocumentExtractedTable(
            table.BoundingRegions.Count == 0 ? null : table.BoundingRegions[0].PageNumber,
            table.RowCount,
            table.ColumnCount,
            JsonSerializer.Serialize(table.Cells.Select(cell => new
            {
                cell.RowIndex,
                cell.ColumnIndex,
                cell.RowSpan,
                cell.ColumnSpan,
                cell.Content,
                Kind = cell.Kind?.ToString()
            })),
            SerializeBoundingRegions(table.BoundingRegions),
            SerializeSpans(table.Spans))).ToArray();

        var figures = result.Figures.Select((figure, index) => new DocumentExtractedFigure(
            string.IsNullOrWhiteSpace(figure.Id) ? $"figure/{index + 1}" : figure.Id,
            figure.BoundingRegions.Count == 0 ? null : figure.BoundingRegions[0].PageNumber,
            figure.Caption?.Content,
            SerializeBoundingRegions(figure.BoundingRegions),
            SerializeSpans(figure.Spans),
            JsonSerializer.Serialize(new
            {
                figure.Id,
                Elements = figure.Elements,
                Caption = figure.Caption?.Content,
                Footnotes = figure.Footnotes.Select(footnote => footnote.Content)
            }))).ToArray();

        return new DocumentExtractionResult(
            LegalDocumentExtractionMethods.AzureDocumentIntelligence,
            _options.ModelId,
            _options.ModelVersion,
            DateTime.UtcNow,
            pages,
            sections,
            tables,
            operation.Id,
            Figures: figures);
    }

    private static decimal? AverageConfidence(IEnumerable<float> values)
    {
        var confidences = values.ToArray();
        return confidences.Length == 0 ? null : Convert.ToDecimal(confidences.Average());
    }

    private static string? SerializeBoundingRegions(IEnumerable<BoundingRegion> regions)
    {
        var values = regions.Select(region => new
        {
            region.PageNumber,
            Polygon = region.Polygon
        }).ToArray();
        return values.Length == 0 ? null : JsonSerializer.Serialize(values);
    }

    private static string? SerializePolygon(IEnumerable<float> polygon)
    {
        var values = polygon.ToArray();
        return values.Length == 0 ? null : JsonSerializer.Serialize(values);
    }

    private static string SerializeSpan(DocumentSpan span) =>
        JsonSerializer.Serialize(new { span.Offset, span.Length });

    private static string? SerializeSpans(IEnumerable<DocumentSpan> spans)
    {
        var values = spans.Select(span => new { span.Offset, span.Length }).ToArray();
        return values.Length == 0 ? null : JsonSerializer.Serialize(values);
    }

    private static string ToPageRanges(IReadOnlyCollection<int> pageNumbers)
    {
        var pages = pageNumbers.Where(number => number > 0).Distinct().Order().ToArray();
        if (pages.Length == 0)
            throw new ArgumentException("At least one positive page number is required.", nameof(pageNumbers));

        var ranges = new List<string>();
        var start = pages[0];
        var end = start;
        foreach (var page in pages.Skip(1))
        {
            if (page == end + 1)
            {
                end = page;
                continue;
            }

            ranges.Add(start == end ? $"{start}" : $"{start}-{end}");
            start = end = page;
        }

        ranges.Add(start == end ? $"{start}" : $"{start}-{end}");
        return string.Join(',', ranges);
    }

}
