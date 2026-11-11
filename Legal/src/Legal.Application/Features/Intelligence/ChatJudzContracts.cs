namespace Legal.Application.Features.Intelligence;

// Enterprise "Chat Judz" assistant contracts shared by Legal.Web (ApiClient) and Legal.Api
// (LegalChatController). The conversation transcript is sent on every turn so the stateless
// default-LLM route can produce a context-aware reply. Every turn is persisted to
// POLOXI.Legal_ChatJudzMessage so conversations are listable and reopenable (enterprise history).
public sealed record ChatJudzMessage(string Role, string Content);

// ConversationId ties a turn to its thread. Null/empty on the first turn of a brand-new conversation;
// the server mints one and returns it so the client can continue persisting into the same thread.
// ModelCode is the user-selected chat model (from the toolbar dropdown). Null/empty => the configured
// default feature-policy route answers the turn.
public sealed record ChatJudzRequest(
    IReadOnlyList<ChatJudzMessage> Messages,
    Guid? MatterId = null,
    Guid? ConversationId = null,
    string? ModelCode = null,
    Guid? RunId = null);

// Reply is the model's answer. Notice carries a friendly, actionable message when a turn could not be
// completed (e.g. an AI safety-guard rejection such as an over-length request); the UI shows it as a
// help banner rather than a chat bubble. Exactly one of Reply/Notice is populated for a given turn.
public sealed record ChatJudzResponse(string? Reply, string? ModelCode = null, Guid ConversationId = default)
{
    public string? Notice { get; init; }
}

// One persisted turn, oldest-first, used to reopen a saved conversation.
public sealed record ChatJudzTurnDto(string Role, string Content, string? ModelCode, DateTime CreatedDateUtc);

// A conversation summary card for the history sidebar (newest activity first).
public sealed record ChatJudzConversationSummaryDto(
    Guid ConversationId,
    string Title,
    int MessageCount,
    DateTime LastActivityUtc,
    Guid? MatterId,
    string? Preview = null);

// An active chat-capable model deployment the user can pick in the Chat Judz toolbar.
public sealed record ChatJudzModelOptionDto(string ModelCode, string DeploymentName, string? ModelFamily);

// The user's selected chat model code (null => configured default route). Used by GET/PUT model.
public sealed record ChatJudzSelectedModelDto(string? ModelCode);

// ── Dedicated Citation Passages history ───────────────────────────────────────────────────────────
// A single Judz citation generation is one reopenable "batch". Each ranked authority within a branch is
// one persisted passage row with its own metadata (persisted to POLOXI.Legal_ChatJudzCitationPassage).

// One label/value facet (e.g. "Doctrine": "Premises duty").
public sealed record ChatJudzCitationFacetDto(string Label, string Value);

// One ranked authority row captured for a batch — the rich card content shown in the Citations tab.
public sealed record ChatJudzCitationPassageDto(
    string BranchDisplayName,
    decimal? BranchConfidence,
    string? BranchInterpretation,
    int RankNumber,
    string AuthorityName,
    decimal? MatchScore,
    IReadOnlyList<ChatJudzCitationFacetDto> Facets,
    string? FitDetail,
    string? LinkedMatterPassage,
    int LinkedFactorCount);

// The write payload for persisting one generation's ranked authority landscape as a batch.
public sealed record ChatJudzCitationBatchRequest(
    Guid CitationBatchId,
    Guid? ConversationId,
    Guid? MatterId,
    string? BatchTitle,
    IReadOnlyList<ChatJudzCitationPassageDto> Passages);

// A batch summary card for the citation-history list (newest first).
public sealed record ChatJudzCitationBatchSummaryDto(
    Guid CitationBatchId,
    string Title,
    int PassageCount,
    int BranchCount,
    DateTime CreatedDateUtc,
    Guid? ConversationId,
    Guid? MatterId);

// The full reopenable batch: its passages, ordered as rendered.
public sealed record ChatJudzCitationBatchDto(
    Guid CitationBatchId,
    string? Title,
    DateTime CreatedDateUtc,
    Guid? ConversationId,
    Guid? MatterId,
    IReadOnlyList<ChatJudzCitationPassageDto> Passages);