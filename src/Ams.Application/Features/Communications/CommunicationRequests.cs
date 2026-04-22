namespace Ams.Application.Features.Communications;

public sealed record GetThreadsRequest(
    Guid TenantId,
    string? Channel = null,
    string? Status = null,
    string? AssignedTo = null,
    string? SearchTerm = null);

public sealed record SendMessageRequest(
    Guid TenantId,
    string AccountName,
    string? AccountId,
    string Channel,
    string Subject,
    string Body,
    string Priority,
    string? AssignedTo,
    string? Template);

public sealed record ReplyMessageRequest(
    Guid ThreadId,
    string SenderName,
    string Channel,
    string Body,
    string[]? Attachments = null);

public sealed record AssignThreadRequest(
    Guid ThreadId,
    string AssignedTo,
    string? Note = null);

public sealed record EscalateThreadRequest(
    Guid ThreadId,
    string EscalateTo,
    string Reason,
    string? Note = null);

public sealed record ResolveThreadRequest(Guid ThreadId);

public sealed record MarkReadRequest(Guid ThreadId);

// ── Templates ─────────────────────────────────────────────────────────
public sealed record CreateCommTemplateRequest(
    Guid TenantId,
    string Name,
    string Channel,
    string Category,
    string Language,
    string Status,
    string? Subject,
    string Body,
    bool IncludeOptOutFooter,
    bool TcpaNotice);

public sealed record UpdateCommTemplateRequest(
    Guid TemplateId,
    string Name,
    string Channel,
    string Category,
    string Language,
    string Status,
    string? Subject,
    string Body,
    bool IncludeOptOutFooter,
    bool TcpaNotice);
