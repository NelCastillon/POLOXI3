using Legal.Application.Security;
using Xunit;

namespace Legal.Application.Tests.Security;

public sealed class ActingIdentitySignatureTests
{
    private const string Secret = "test-only-shared-secret";

    [Fact]
    public void BuildPayload_IncludesRoleInCanonicalOrder()
    {
        var payload = ActingIdentitySignature.BuildPayload(
            1_700_000_000,
            "user-id",
            "User%20Name",
            "user%40example.com",
            "tenant-id",
            "OWNER",
            "matter:read,account:write");

        Assert.Equal(
            "1700000000\nuser-id\nUser%20Name\nuser%40example.com\ntenant-id\nOWNER\nmatter:read,account:write",
            payload);
    }

    [Fact]
    public void Verify_AcceptsSignatureWhenRoleIsUnchanged()
    {
        var now = DateTimeOffset.UtcNow;
        var timestamp = now.ToUnixTimeSeconds();
        var payload = ActingIdentitySignature.BuildPayload(
            timestamp, "user-id", "User", "user@example.com", "tenant-id", "OWNER", "matter:read");
        var signature = ActingIdentitySignature.Sign(payload, Secret);

        var verified = ActingIdentitySignature.Verify(payload, signature, Secret, timestamp, now);

        Assert.True(verified);
    }

    [Fact]
    public void Verify_RejectsSignatureWhenRoleIsChanged()
    {
        var now = DateTimeOffset.UtcNow;
        var timestamp = now.ToUnixTimeSeconds();
        var ownerPayload = ActingIdentitySignature.BuildPayload(
            timestamp, "user-id", "User", "user@example.com", "tenant-id", "OWNER", "matter:read");
        var memberPayload = ActingIdentitySignature.BuildPayload(
            timestamp, "user-id", "User", "user@example.com", "tenant-id", "MEMBER", "matter:read");
        var signature = ActingIdentitySignature.Sign(ownerPayload, Secret);

        var verified = ActingIdentitySignature.Verify(memberPayload, signature, Secret, timestamp, now);

        Assert.False(verified);
    }
}
