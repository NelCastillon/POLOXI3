using Legal.Application.Features.Intelligence.Science;

namespace Legal.Application.Abstractions.Services;

// ── POLOXI Formalization Gate — service seam ──────────────────────────────────────────────────────────
// The application-layer entry point for the Research → Formalize → Math step. It runs a single governed
// AI stage that converts a surviving research idea/hypothesis into a clean Proof Contract (Assumptions ⇒
// Claim). The LLM only PROPOSES the contract; it is never treated as proven. Mirrors IMathReasoningService
// so the API + DI wiring follow the established Intelligence patterns.
public interface IFormalizationService
{
    Task<FormalizationResponse> FormalizeAsync(FormalizationRequest request, CancellationToken cancellationToken = default);
}
