namespace Ams.Application.Common.Dtos;

public sealed class ProposalDto
{
    public Guid ProposalId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? PdfUrl { get; set; }
    public string? HtmlContent { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? GeneratedDateUtc { get; set; }
}
