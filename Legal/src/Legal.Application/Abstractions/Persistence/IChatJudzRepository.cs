using Legal.Application.Features.Intelligence;

namespace Legal.Application.Abstractions.Persistence;

// Persistent enterprise conversation history for Chat Judz (POLOXI.Legal_ChatJudzMessage).
// Self-contained and tenant/user scoped; isolated from Decision2 and the Wide search pipeline.
public interface IChatJudzRepository
{
    // Appends one persisted turn to a conversation. Returns the next sequence number used.
    Task AppendMessageAsync(
        Guid tenantId,
        Guid? userId,
        Guid conversationId,
        int sequence,
        string role,
        string content,
        string? modelCode,
        Guid? matterId,
        CancellationToken cancellationToken = default);

    // The current message count for a conversation (used to compute the next sequence number).
    Task<int> GetMessageCountAsync(Guid tenantId, Guid? userId, Guid conversationId, CancellationToken cancellationToken = default);

    // Full transcript of one conversation, oldest-first, scoped to the user.
    Task<IReadOnlyList<ChatJudzTurnDto>> GetConversationAsync(Guid tenantId, Guid? userId, Guid conversationId, CancellationToken cancellationToken = default);

    // The user's conversations, newest activity first. Optional matter filter.
    Task<IReadOnlyList<ChatJudzConversationSummaryDto>> GetConversationListAsync(Guid tenantId, Guid? userId, Guid? matterId, int take, CancellationToken cancellationToken = default);

    // Soft-deletes an entire conversation for the user.
    Task DeleteConversationAsync(Guid tenantId, Guid? userId, Guid conversationId, CancellationToken cancellationToken = default);

    // Active chat-capable model deployments the user can select in the toolbar.
    Task<IReadOnlyList<ChatJudzModelOptionDto>> GetModelOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    // The user's currently selected chat model code (null when none is saved).
    Task<string?> GetSelectedModelAsync(Guid tenantId, Guid? userId, CancellationToken cancellationToken = default);

    // Upserts the user's selected chat model code (tenant/user scoped).
    Task SaveSelectedModelAsync(Guid tenantId, Guid? userId, string modelCode, CancellationToken cancellationToken = default);

    // ── Citation Passages history ──────────────────────────────────────────────────────────────────
    // Persists one generation's ranked authority landscape as a batch (POLOXI.Legal_ChatJudzCitationPassage).
    Task AppendCitationBatchAsync(Guid tenantId, Guid? userId, ChatJudzCitationBatchRequest batch, CancellationToken cancellationToken = default);

    // The user's citation batches, newest first. Optional matter filter.
    Task<IReadOnlyList<ChatJudzCitationBatchSummaryDto>> GetCitationBatchesAsync(Guid tenantId, Guid? userId, Guid? matterId, int take, CancellationToken cancellationToken = default);

    // The full, reopenable batch (passages ordered as rendered), scoped to the user.
    Task<ChatJudzCitationBatchDto?> GetCitationBatchAsync(Guid tenantId, Guid? userId, Guid citationBatchId, CancellationToken cancellationToken = default);
}
