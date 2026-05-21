namespace Ams.Application.Common.Dtos;

public sealed record DocumentExceptionDto(
    Guid DocumentExceptionId,
    Guid TenantId,
    Guid? DocumentId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string ExceptionType,
    string ExceptionReason,
    string Status,
    string AiSuggestion,
    int AiConfidence,
    string? AssignedToName,
    string? CategoryCode,
    string? DocumentTypeCode,
    string? LinkedEntity,
    string? Tags,
    string? Notes,
    DateTime ReceivedDateUtc,
    DateTime? ResolvedDateUtc,
    DateTime CreatedDateUtc)
{
    public string FileSizeFormatted => FileSizeBytes switch
    {
        < 1024 => $"{FileSizeBytes} B",
        < 1048576 => $"{FileSizeBytes / 1024.0:F1} KB",
        < 1073741824 => $"{FileSizeBytes / 1048576.0:F1} MB",
        _ => $"{FileSizeBytes / 1073741824.0:F2} GB"
    };

    public bool IsResolved => string.Equals(Status, "Resolved", StringComparison.OrdinalIgnoreCase);
}
