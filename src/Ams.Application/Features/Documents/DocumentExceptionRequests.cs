using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Documents;

public sealed record CreateDocumentExceptionRequest(
    [property: Required] Guid TenantId,
    Guid? DocumentId,
    [property: Required, StringLength(260)] string FileName,
    [property: Required, StringLength(200)] string ContentType,
    [property: Range(0, long.MaxValue)] long FileSizeBytes,
    [property: Required, StringLength(80)] string ExceptionType,
    [property: Required, StringLength(1000)] string ExceptionReason,
    [property: Required, StringLength(80)] string Status,
    [property: Required, StringLength(160)] string AiSuggestion,
    [property: Range(0, 100)] int AiConfidence,
    [property: StringLength(160)] string? AssignedToName,
    Guid? CreatedByUserId);

public sealed record ClassifyDocumentExceptionRequest(
    [property: Required] Guid DocumentExceptionId,
    [property: Required, StringLength(100)] string CategoryCode,
    [property: Required, StringLength(100)] string DocumentTypeCode,
    [property: StringLength(160)] string? LinkedEntity,
    [property: StringLength(500)] string? Tags,
    [property: StringLength(1000)] string? Notes,
    Guid? ModifiedByUserId);

public sealed record UpdateDocumentExceptionStatusRequest(
    [property: Required] Guid DocumentExceptionId,
    [property: Required, StringLength(80)] string Status,
    [property: StringLength(1000)] string? Notes,
    Guid? ModifiedByUserId);
