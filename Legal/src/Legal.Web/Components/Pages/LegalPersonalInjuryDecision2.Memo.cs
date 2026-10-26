using Legal.Application.Features.Intelligence;

namespace Legal.Web.Components.Pages;

// On-demand attorney-style Decision Memorandum UI state for the Decision Intelligence Overview.
// This is a DEDICATED, user-triggered metered LLM call: it never runs on page load, it uses the saved
// analysis run as context, and it is guarded against double submission. The composed memo is advisory
// presentation only and openly discloses provisional status when readiness gates are open.
public partial class LegalPersonalInjuryDecision2
{
    internal DecisionMemoDto? _decisionMemo;
    internal bool _decisionMemoLoading;
    internal string? _decisionMemoError;

    // True when a saved analysis run is loaded and a memo can be generated against it.
    internal bool OvCanGenerateMemo => _loadedRunId is { } id && id != Guid.Empty && _response is not null;

    internal async Task GenerateDecisionMemoAsync()
    {
        if (_decisionMemoLoading) return;
        if (_loadedRunId is not { } runId || runId == Guid.Empty)
        {
            _decisionMemoError = "No saved analysis run is available to generate a memorandum.";
            return;
        }

        _decisionMemoLoading = true;
        _decisionMemoError = null;
        StateHasChanged();
        try
        {
            var memo = await Api.ComposeDecisionMemoAsync(runId);
            if (memo is null)
                _decisionMemoError = "No saved results were found for this run, so a memorandum could not be generated.";
            else
                _decisionMemo = memo;
        }
        catch (Exception ex)
        {
            _decisionMemoError = $"Memorandum generation failed: {ex.Message}";
        }
        finally
        {
            _decisionMemoLoading = false;
            StateHasChanged();
        }
    }
}
