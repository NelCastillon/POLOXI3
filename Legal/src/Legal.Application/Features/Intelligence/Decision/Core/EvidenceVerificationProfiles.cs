namespace Legal.Application.Features.Intelligence.Decision.Core;

public static class EvidenceSourceTypeCodes
{
    public static string ToCode(EvidenceSourceType sourceType) => sourceType switch
    {
        EvidenceSourceType.CaseLaw => "CASE_LAW",
        EvidenceSourceType.Statute => "STATUTE",
        EvidenceSourceType.Regulation => "REGULATION",
        EvidenceSourceType.AdministrativeAuthority => "ADMINISTRATIVE_AUTHORITY",
        EvidenceSourceType.MatterDocument => "MATTER_DOCUMENT",
        EvidenceSourceType.Declaration => "DECLARATION",
        EvidenceSourceType.Deposition => "DEPOSITION",
        EvidenceSourceType.Contract => "CONTRACT",
        EvidenceSourceType.Correspondence => "CORRESPONDENCE",
        EvidenceSourceType.BusinessRecord => "BUSINESS_RECORD",
        EvidenceSourceType.SecondaryAuthority => "SECONDARY_AUTHORITY",
        EvidenceSourceType.GovernmentDocument => "GOVERNMENT_DOCUMENT",
        _ => "UNKNOWN",
    };
}

public sealed class DeterministicEvidenceSourceClassifier : IEvidenceSourceClassifier
{
    public EvidenceSourceType Classify(EvidenceVerificationRequest request)
    {
        if (request.DeclaredSourceType is { } declared && declared != EvidenceSourceType.Unknown)
            return declared;

        var text = $"{request.SourceTitle} {request.SourceRef}".ToLowerInvariant();
        if (ContainsAny(text, "courtlistener", " v. ", " versus ", "opinion", "court of appeals", "supreme court"))
            return EvidenceSourceType.CaseLaw;
        if (ContainsAny(text, "ecfr", "c.f.r", " cfr", "regulation", "final rule", "proposed rule"))
            return EvidenceSourceType.Regulation;
        if (ContainsAny(text, "uscode", "u.s.c", "statute", "public law", "code section"))
            return EvidenceSourceType.Statute;
        if (ContainsAny(text, "deposition", "declaration", "exhibit", "matter document", "uploaded"))
            return EvidenceSourceType.MatterDocument;
        if (ContainsAny(text, "law review", "treatise", "article", "practice guide"))
            return EvidenceSourceType.SecondaryAuthority;
        if (ContainsAny(text, ".gov", "govinfo", "department of", "commission", "agency"))
            return EvidenceSourceType.GovernmentDocument;
        return EvidenceSourceType.Unknown;
    }

    private static bool ContainsAny(string value, params string[] candidates) =>
        candidates.Any(value.Contains);
}

public sealed class VerificationProfileProvider : IVerificationProfileProvider
{
    private static readonly VerificationProfile CaseLaw = new(
        "CASE_LAW_V1", true, false, true, true, true, true, true, true);
    private static readonly VerificationProfile Statute = new(
        "STATUTE_V1", true, false, true, true, true, false, false, true);
    private static readonly VerificationProfile Regulation = new(
        "REGULATION_V1", true, false, true, true, true, false, false, true);
    private static readonly VerificationProfile Administrative = new(
        "ADMINISTRATIVE_AUTHORITY_V1", true, false, true, true, true, true, false, true);
    private static readonly VerificationProfile MatterDocument = new(
        "MATTER_DOCUMENT_V1", true, true, false, true, true, false, false, false);
    private static readonly VerificationProfile Secondary = new(
        "SECONDARY_AUTHORITY_V1", true, false, true, true, true, true, false, true);
    private static readonly VerificationProfile Government = new(
        "GOVERNMENT_DOCUMENT_V1", true, false, true, true, true, true, false, true);
    private static readonly VerificationProfile Unknown = new(
        "UNKNOWN_STRICT_V1", true, true, true, true, true, true, true, true);

    public VerificationProfile GetProfile(EvidenceSourceType sourceType) => sourceType switch
    {
        EvidenceSourceType.CaseLaw => CaseLaw,
        EvidenceSourceType.Statute => Statute,
        EvidenceSourceType.Regulation => Regulation,
        EvidenceSourceType.AdministrativeAuthority => Administrative,
        EvidenceSourceType.MatterDocument => MatterDocument,
        EvidenceSourceType.Declaration or EvidenceSourceType.Deposition or EvidenceSourceType.Contract
            or EvidenceSourceType.Correspondence or EvidenceSourceType.BusinessRecord => MatterDocument,
        EvidenceSourceType.SecondaryAuthority => Secondary,
        EvidenceSourceType.GovernmentDocument => Government,
        _ => Unknown,
    };
}
