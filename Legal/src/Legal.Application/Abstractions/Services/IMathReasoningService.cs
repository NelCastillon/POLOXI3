using Legal.Application.Features.Intelligence.Science;

namespace Legal.Application.Abstractions.Services;

// ── POLOXI Math V1 — reasoning service seam (runnable slice) ─────────────────────────────────────────
// The application-layer entry point for the Mathematics domain pack. It runs the linear 8-stage
// discovery pipeline (contract → strategy → derivation → verification → counterexample → extraction →
// self-consistency → composition), delegating answer ACCEPTANCE to deterministic C# verification.
// Mirrors IIntelligenceWideService so the API + DI wiring follow the established Intelligence patterns.
public interface IMathReasoningService
{
    Task<MathSolveResponse> SolveAsync(MathSolveRequest request, CancellationToken cancellationToken = default);
}
