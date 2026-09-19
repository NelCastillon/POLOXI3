using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Legal.Application.Security;

// ─────────────────────────────────────────────────────────────────────────────
// Shared HMAC signing for the forwarded acting-identity headers.
//
// The Web tier forwards the signed-in user's identity to the API via X-Acting-*
// headers. To prevent header spoofing in Production the Web tier signs a
// canonical payload with a shared secret and the API validates it. This helper
// lives in Legal.Application so both hosts produce and verify BYTE-IDENTICAL
// canonical payloads — any divergence would break validation.
// ─────────────────────────────────────────────────────────────────────────────
public static class ActingIdentitySignature
{
    public const string TimestampHeader = "X-Acting-Timestamp";
    public const string SignatureHeader = "X-Acting-Signature";

    // Default freshness window guarding against replay while tolerating clock skew.
    public static readonly TimeSpan DefaultClockSkew = TimeSpan.FromMinutes(5);

    // Canonical payload: newline-joined, fixed field order. Null/empty fields are
    // emitted as empty strings so both sides agree on the exact byte sequence.
    public static string BuildPayload(
        long unixTimeSeconds,
        string? userId,
        string? userName,
        string? userEmail,
        string? tenantId,
        string? permissions)
        => string.Join('\n',
            unixTimeSeconds.ToString(CultureInfo.InvariantCulture),
            userId ?? string.Empty,
            userName ?? string.Empty,
            userEmail ?? string.Empty,
            tenantId ?? string.Empty,
            permissions ?? string.Empty);

    public static string Sign(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hash);
    }

    // Constant-time verification of the signature and timestamp freshness.
    public static bool Verify(
        string payload,
        string? providedSignature,
        string secret,
        long unixTimeSeconds,
        DateTimeOffset now,
        TimeSpan? clockSkew = null)
    {
        if (string.IsNullOrWhiteSpace(providedSignature) || string.IsNullOrWhiteSpace(secret))
            return false;

        var window = clockSkew ?? DefaultClockSkew;
        var signedAt = DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds);
        if (signedAt > now + window || signedAt < now - window)
            return false;

        var expected = Sign(payload, secret);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(providedSignature));
    }
}
