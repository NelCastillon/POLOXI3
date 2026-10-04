namespace Legal.Application.Features.MatterLifecycle;

// Thrown when a requested stage transition is not an active directed edge in the matter's
// lifecycle graph. Transitions are never inferred from display order. Mapped to HTTP 409.
public sealed class InvalidMatterLifecycleTransitionException(string message)
    : System.InvalidOperationException(message);
