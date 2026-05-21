namespace Ams.Application.Common.Dtos;

public sealed class DocumentPacketDto
{
    public Guid DocumentPacketId { get; set; }
    public Guid TenantId { get; set; }
    public string PacketName { get; set; } = string.Empty;
    public string PacketType { get; set; } = string.Empty;
    public string? PolicyNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool AiAssisted { get; set; }
    public string? Description { get; set; }
    public string? RecipientEmail { get; set; }
    public string? DeliveryMethod { get; set; }
    public DateTime? SentDateUtc { get; set; }
    public DateTime? MergedDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public int DocumentCount { get; set; }
    public int ReadyCount { get; set; }
    public int MissingCount { get; set; }
    public IReadOnlyList<DocumentPacketDocumentDto> Documents { get; set; } = [];
}

public sealed class DocumentPacketDocumentDto
{
    public Guid PacketDocumentId { get; set; }
    public Guid DocumentPacketId { get; set; }
    public Guid? DocumentId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string Status { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
