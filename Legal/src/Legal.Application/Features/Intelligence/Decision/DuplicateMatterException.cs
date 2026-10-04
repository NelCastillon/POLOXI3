namespace Legal.Application.Features.Intelligence.Decision;

// Thrown when a create request would duplicate an existing (non-deleted) matter for the
// same tenant. "Same matter" is defined as identical normalized Title + MatterType +
// Jurisdiction. The API layer maps this to HTTP 409 Conflict with a user-facing message.
public sealed class DuplicateMatterException(string message, System.Guid existingMatterId)
    : System.InvalidOperationException(message)
{
    public System.Guid ExistingMatterId { get; } = existingMatterId;
}
