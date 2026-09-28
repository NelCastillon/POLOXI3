using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Infrastructure.Intelligence;

/// <summary>
/// No-op document security scanner used when DocumentIntelligence:MalwareScannerProvider is
/// <c>Disabled</c> (the default). Malware scanning is OFF: this scanner does not execute any scan
/// and does not call any external endpoint. It reports a non-detected status so document intake can
/// proceed. It never bypasses a configured scanner — selecting the Http/DefenderForStorage provider
/// still enforces a real scan.
/// </summary>
public sealed class DisabledLegalDocumentSecurityScanner : ILegalDocumentSecurityScanner
{
    // NOT_DETECTED is treated as clean by intake (marks the version RECEIVED, not QUARANTINED).
    public Task<LegalDocumentSecurityScanResult> ScanAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new LegalDocumentSecurityScanResult("NOT_DETECTED"));
}
