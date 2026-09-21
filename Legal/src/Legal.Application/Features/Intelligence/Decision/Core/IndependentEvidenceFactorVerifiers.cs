namespace Legal.Application.Features.Intelligence.Decision.Core;

public sealed class PlaywrightIdentityEvidenceVerifier(IWebSourceInspector inspector) : IIdentityEvidenceVerifier
{
    public EvidenceVerificationFactor Factor => EvidenceVerificationFactor.Identity;

    public async Task<VerificationCheckResult> VerifyAsync(EvidenceVerificationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SourceRef))
            return Failed("SOURCE_IDENTITY_REFERENCE_MISSING", "Source reference is missing.", request);

        if (ProviderArtifactVerification.TryValidate(request, out var providerReason))
            return new VerificationCheckResult
            {
                State = VerificationCheckState.Passed,
                ReasonCode = "SOURCE_IDENTITY_PROVIDER_VERIFIED",
                Reason = providerReason,
                VerifiedValue = request.SourceTitle,
                SourceRef = request.SourceRef,
                VerificationMethod = "AUTHORITATIVE_RETRIEVAL_ARTIFACT_V1",
                VerifierId = request.SourceProvider,
                VerifierVersion = request.SourceVersion,
            };

        var inspection = await inspector.InspectAsync(request.SourceRef, cancellationToken);
        var hasTitle = !string.IsNullOrWhiteSpace(request.SourceTitle);
        var titleMatches = hasTitle && !string.IsNullOrWhiteSpace(inspection.DocumentTitle)
            && (inspection.DocumentTitle.Contains(request.SourceTitle!, StringComparison.OrdinalIgnoreCase)
                || request.SourceTitle!.Contains(inspection.DocumentTitle, StringComparison.OrdinalIgnoreCase));
        var passed = inspection.Resolved && hasTitle && titleMatches;
        return new VerificationCheckResult
        {
            State = passed ? VerificationCheckState.Passed
                : inspection.Resolved ? VerificationCheckState.Inconclusive : VerificationCheckState.Failed,
            ReasonCode = passed ? "SOURCE_IDENTITY_BROWSER_VERIFIED"
                : inspection.Resolved ? "SOURCE_TITLE_NOT_CONFIRMED" : "SOURCE_PAGE_UNRESOLVED",
            Reason = passed
                ? "The live source resolved and its document title matched the claimed source title."
                : inspection.FailureReason ?? "The live document title did not establish the claimed source identity.",
            VerifiedValue = inspection.DocumentTitle,
            SourceRef = inspection.FinalUrl ?? request.SourceRef,
            VerificationMethod = inspection.VerificationMethod,
        };
    }

    private static VerificationCheckResult Failed(string code, string reason, EvidenceVerificationRequest request) => new()
    {
        State = VerificationCheckState.Failed,
        ReasonCode = code,
        Reason = reason,
        SourceRef = request.SourceRef,
        VerificationMethod = "MICROSOFT_PLAYWRIGHT_CHROMIUM_V1",
    };
}

public sealed class PlaywrightCitationEvidenceVerifier(IWebSourceInspector inspector) : ICitationEvidenceVerifier
{
    public EvidenceVerificationFactor Factor => EvidenceVerificationFactor.Citation;

    public async Task<VerificationCheckResult> VerifyAsync(EvidenceVerificationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SourceRef))
            return new VerificationCheckResult
            {
                State = VerificationCheckState.Failed,
                ReasonCode = "CITATION_REFERENCE_MISSING",
                Reason = "Citation reference is missing.",
                VerificationMethod = "MICROSOFT_PLAYWRIGHT_CHROMIUM_V1",
            };

        if (ProviderArtifactVerification.TryValidate(request, out var providerReason))
            return new VerificationCheckResult
            {
                State = VerificationCheckState.Passed,
                ReasonCode = "CITATION_PROVIDER_RESOLVED",
                Reason = providerReason,
                VerifiedValue = request.SourceRef,
                SourceRef = request.SourceRef,
                VerificationMethod = "AUTHORITATIVE_RETRIEVAL_ARTIFACT_V1",
                VerifierId = request.SourceProvider,
                VerifierVersion = request.SourceVersion,
            };

        var inspection = await inspector.InspectAsync(request.SourceRef, cancellationToken);
        return new VerificationCheckResult
        {
            State = inspection.Resolved ? VerificationCheckState.Passed : VerificationCheckState.Failed,
            ReasonCode = inspection.Resolved ? "CITATION_BROWSER_RESOLVED" : "CITATION_BROWSER_UNRESOLVED",
            Reason = inspection.Resolved
                ? $"Citation resolved with HTTP status {inspection.StatusCode}."
                : inspection.FailureReason ?? "Citation could not be resolved in the source browser.",
            VerifiedValue = inspection.FinalUrl,
            SourceRef = inspection.FinalUrl ?? request.SourceRef,
            VerificationMethod = inspection.VerificationMethod,
        };
    }
}

public sealed class PlaywrightPassageEvidenceVerifier(IWebSourceInspector inspector) : IPassageEvidenceVerifier
{
    public EvidenceVerificationFactor Factor => EvidenceVerificationFactor.Passage;

    public async Task<VerificationCheckResult> VerifyAsync(EvidenceVerificationRequest request, CancellationToken cancellationToken = default)
    {
        var hasText = !string.IsNullOrWhiteSpace(request.SourceText);
        var titleEcho = DecisionCoreMath.PassageEchoesTitle(request.SourceTitle, request.SourceText);
        if (!hasText || titleEcho || string.IsNullOrWhiteSpace(request.SourceRef))
            return new VerificationCheckResult
            {
                State = VerificationCheckState.Failed,
                ReasonCode = titleEcho ? "PASSAGE_TITLE_ECHO" : "PASSAGE_NOT_FOUND",
                Reason = titleEcho ? "Retrieved text only repeats the source title." : "No claimed passage was available.",
                SourceRef = request.SourceRef,
                VerificationMethod = "MICROSOFT_PLAYWRIGHT_CHROMIUM_V1",
            };

        if (ProviderArtifactVerification.TryValidate(request, out var providerReason))
            return new VerificationCheckResult
            {
                State = VerificationCheckState.Passed,
                ReasonCode = "PASSAGE_PROVIDER_ARTIFACT_LOCATED",
                Reason = providerReason,
                VerifiedValue = request.SourceText,
                SourceRef = request.SourceRef,
                SupportingPassage = request.SourceText,
                PassageRef = request.PassageRef ?? request.SourceRef,
                VerificationMethod = "AUTHORITATIVE_RETRIEVAL_ARTIFACT_V1",
                VerifierId = request.SourceProvider,
                VerifierVersion = request.SourceVersion,
            };

        var inspection = await inspector.InspectAsync(request.SourceRef, cancellationToken);
        var normalizedPage = Normalize(inspection.VisibleText);
        var normalizedPassage = Normalize(request.SourceText);
        var located = inspection.Resolved && normalizedPassage.Length > 0
            && normalizedPage.Contains(normalizedPassage, StringComparison.Ordinal);
        return new VerificationCheckResult
        {
            State = located ? VerificationCheckState.Passed
                : inspection.Resolved ? VerificationCheckState.Failed : VerificationCheckState.Error,
            ReasonCode = located ? "PASSAGE_BROWSER_LOCATED"
                : inspection.Resolved ? "PASSAGE_NOT_PRESENT_IN_SOURCE" : "PASSAGE_SOURCE_UNAVAILABLE",
            Reason = located
                ? "The claimed passage was located in the live source text."
                : inspection.FailureReason ?? "The claimed passage was not found in the resolved source text.",
            VerifiedValue = located ? request.SourceText : null,
            SourceRef = inspection.FinalUrl ?? request.SourceRef,
            SupportingPassage = located ? request.SourceText : null,
            VerificationMethod = inspection.VerificationMethod,
        };
    }

    private static string Normalize(string? value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}

internal static class ProviderArtifactVerification
{
    private static readonly IReadOnlyDictionary<string, string[]> ProviderHosts =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["COURTLISTENER"] = ["courtlistener.com"],
            ["GOVINFO"] = ["govinfo.gov"],
            ["ECFR"] = ["ecfr.gov"],
            ["CORNELL_LII"] = ["law.cornell.edu"],
        };

    public static bool TryValidate(EvidenceVerificationRequest request, out string reason)
    {
        reason = string.Empty;
        if (!request.ProviderIdentityVerified
            || string.IsNullOrWhiteSpace(request.SourceProvider)
            || string.IsNullOrWhiteSpace(request.SourceVersion)
            || string.IsNullOrWhiteSpace(request.SourceTitle)
            || string.IsNullOrWhiteSpace(request.SourceText)
            || DecisionCoreMath.PassageEchoesTitle(request.SourceTitle, request.SourceText)
            || !Uri.TryCreate(request.SourceRef, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || !ProviderHosts.TryGetValue(request.SourceProvider.Trim(), out var allowedHosts)
            || !allowedHosts.Any(host => uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith($".{host}", StringComparison.OrdinalIgnoreCase)))
            return false;

        reason = $"The {request.SourceProvider} retrieval adapter returned a stable source URL, title, versioned provider artifact, and substantive source passage.";
        return true;
    }
}

public sealed class DeterministicPropositionSupportVerifier : IPropositionSupportVerifier
{
    private const double FullSupportFloor = 0.50;
    private const double PartialSupportFloor = 0.10;

    public EvidenceVerificationFactor Factor => EvidenceVerificationFactor.PropositionSupport;

    public Task<PropositionSupportResult> VerifyAsync(
        EvidenceVerificationRequest request,
        VerificationCheckResult passage,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (passage.State != VerificationCheckState.Passed || string.IsNullOrWhiteSpace(passage.SupportingPassage))
            return Task.FromResult(PropositionSupportResult.NotEvaluated(
                request.Proposition, "PROPOSITION_PASSAGE_UNAVAILABLE", "Proposition support requires a verified passage."));

        var support = DecisionCoreMath.Clamp01(
            DecisionCoreMath.PropositionSupport(request.Proposition, passage.SupportingPassage));
        var state = support >= FullSupportFloor
            ? PropositionSupportState.Supported
            : support >= PartialSupportFloor
                ? PropositionSupportState.PartiallySupported
                : PropositionSupportState.Unsupported;
        var reasonCode = state switch
        {
            PropositionSupportState.Supported => "PASSAGE_SUPPORTS_PROPOSITION",
            PropositionSupportState.PartiallySupported => "PASSAGE_PARTIALLY_SUPPORTS_PROPOSITION",
            _ => "PASSAGE_DOES_NOT_ENTAIL_PROPOSITION",
        };
        return Task.FromResult(new PropositionSupportResult
        {
            State = state,
            Proposition = request.Proposition,
            SupportingPassage = passage.SupportingPassage,
            SupportedComponents = state == PropositionSupportState.Supported ? [request.Proposition] : [],
            UnsupportedComponents = state == PropositionSupportState.Supported ? [] : [request.Proposition],
            ReasonCode = reasonCode,
            Reason = $"Deterministic proposition-support value {support:0.###}; full support requires {FullSupportFloor:0.##}.",
            VerificationMethod = "DETERMINISTIC_PROPOSITION_GUARDS_V1",
        });
    }
}

public sealed class DeterministicHoldingEvidenceVerifier : IHoldingEvidenceVerifier
{
    private static readonly string[] HoldingMarkers =
    [
        "we hold", "the court holds", "held that", "we conclude", "the court concludes",
        "judgment is affirmed", "judgment is reversed", "motion is granted", "motion is denied",
    ];

    public EvidenceVerificationFactor Factor => EvidenceVerificationFactor.Holding;

    public Task<VerificationCheckResult> VerifyAsync(
        EvidenceVerificationRequest request,
        VerificationCheckResult passage,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (passage.State != VerificationCheckState.Passed || string.IsNullOrWhiteSpace(passage.SupportingPassage))
            return Task.FromResult(VerificationCheckResult.NotEvaluated(
                "HOLDING_PASSAGE_UNAVAILABLE", "Holding verification requires a verified opinion passage."));

        var text = passage.SupportingPassage;
        var marker = HoldingMarkers.FirstOrDefault(m => text.Contains(m, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(new VerificationCheckResult
        {
            State = marker is null ? VerificationCheckState.Inconclusive : VerificationCheckState.Passed,
            ReasonCode = marker is null ? "HOLDING_NOT_ESTABLISHED" : "HOLDING_MARKER_ESTABLISHED",
            Reason = marker is null
                ? "The passage does not independently establish that the proposition is the court's holding."
                : "The passage contains a court-disposition marker attributable to a holding.",
            VerifiedValue = marker,
            SourceRef = request.SourceRef,
            SupportingPassage = text,
            VerificationMethod = "DETERMINISTIC_HOLDING_MARKERS_V1",
        });
    }
}

public sealed class DeterministicAuthorityEvidenceVerifier : IAuthorityEvidenceVerifier
{
    public EvidenceVerificationFactor Factor => EvidenceVerificationFactor.Authority;

    public Task<VerificationCheckResult> VerifyAsync(EvidenceVerificationRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.CutoffDate is { } cutoff && request.AuthorityDate is { } authorityDate && authorityDate > cutoff)
            return Task.FromResult(new VerificationCheckResult
            {
                State = VerificationCheckState.Failed,
                ReasonCode = "AUTHORITY_AFTER_CUTOFF_DATE",
                Reason = "Authority post-dates the permitted cutoff date.",
                VerifiedValue = authorityDate.ToString("O"),
                SourceRef = request.SourceRef,
                VerificationMethod = "DETERMINISTIC_AUTHORITY_DATE_V1",
            });

        if (request.ProviderIdentityVerified
            && request.DeclaredSourceType is EvidenceSourceType.Statute or EvidenceSourceType.Regulation or EvidenceSourceType.GovernmentDocument
            && request.SourceProvider is not null
            && (request.SourceProvider.Equals("GOVINFO", StringComparison.OrdinalIgnoreCase)
                || request.SourceProvider.Equals("ECFR", StringComparison.OrdinalIgnoreCase)))
            return Task.FromResult(new VerificationCheckResult
            {
                State = VerificationCheckState.Passed,
                ReasonCode = "OFFICIAL_PRIMARY_AUTHORITY_PROVIDER_VERIFIED",
                Reason = "The versioned retrieval artifact came from an official federal primary-authority provider.",
                VerifiedValue = request.SourceProvider,
                SourceRef = request.SourceRef,
                VerificationMethod = "OFFICIAL_PROVIDER_AUTHORITY_V1",
                VerifierId = request.SourceProvider,
                VerifierVersion = request.SourceVersion,
            });

        if (string.IsNullOrWhiteSpace(request.Jurisdiction))
            return Task.FromResult(new VerificationCheckResult
            {
                State = VerificationCheckState.Inconclusive,
                ReasonCode = "AUTHORITY_JURISDICTION_NOT_ESTABLISHED",
                Reason = "No matter jurisdiction was available to establish legal applicability.",
                SourceRef = request.SourceRef,
                VerificationMethod = "DETERMINISTIC_AUTHORITY_CONTEXT_V1",
            });

        return Task.FromResult(new VerificationCheckResult
        {
            State = VerificationCheckState.Inconclusive,
            ReasonCode = "AUTHORITY_APPLICABILITY_NOT_ESTABLISHED",
            Reason = "Jurisdiction is known, but court hierarchy, precedential status, and validity were not independently established.",
            VerifiedValue = request.Jurisdiction,
            SourceRef = request.SourceRef,
            VerificationMethod = "DETERMINISTIC_AUTHORITY_CONTEXT_V1",
        });
    }
}
