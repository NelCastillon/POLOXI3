using Ams.Application.Common.Guards;
using Xunit;

namespace Ams.Application.Tests;

public sealed class TenantGuardTests
{
    private sealed record Parent(Guid TenantId);

    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static Func<Guid, CancellationToken, Task<Parent?>> Found(Parent parent)
        => (_, _) => Task.FromResult<Parent?>(parent);

    private static Func<Guid, CancellationToken, Task<Parent?>> NotFound()
        => (_, _) => Task.FromResult<Parent?>(null);

    // EnsureParentAsync ----------------------------------------------------

    [Fact]
    public async Task EnsureParentAsync_Throws_When_ParentId_Empty()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TenantGuard.EnsureParentAsync(Guid.Empty, TenantA, NotFound(), p => p.TenantId, "Account", "billing account"));

        Assert.Contains("requires a parent Account", ex.Message);
    }

    [Fact]
    public async Task EnsureParentAsync_Throws_When_Parent_Missing()
    {
        var parentId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TenantGuard.EnsureParentAsync(parentId, TenantA, NotFound(), p => p.TenantId, "Account", "billing account"));

        Assert.Contains("was not found", ex.Message);
    }

    [Fact]
    public async Task EnsureParentAsync_Throws_When_Parent_Cross_Tenant()
    {
        var parent = new Parent(TenantB);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TenantGuard.EnsureParentAsync(Guid.NewGuid(), TenantA, Found(parent), p => p.TenantId, "Account", "billing account"));

        Assert.Contains("different tenant", ex.Message);
    }

    [Fact]
    public async Task EnsureParentAsync_Returns_Parent_When_Valid()
    {
        var parent = new Parent(TenantA);

        var result = await TenantGuard.EnsureParentAsync(Guid.NewGuid(), TenantA, Found(parent), p => p.TenantId, "Account", "billing account");

        Assert.Same(parent, result);
    }

    [Fact]
    public async Task EnsureParentAsync_Skips_Tenant_Check_When_TenantId_Empty()
    {
        var parent = new Parent(TenantB);

        var result = await TenantGuard.EnsureParentAsync(Guid.NewGuid(), Guid.Empty, Found(parent), p => p.TenantId, "Account", "billing account");

        Assert.Same(parent, result);
    }

    // EnsureOptionalParentAsync --------------------------------------------

    [Fact]
    public async Task EnsureOptionalParentAsync_Returns_Null_When_Not_Supplied()
    {
        var result = await TenantGuard.EnsureOptionalParentAsync<Parent>(null, TenantA, NotFound(), p => p.TenantId, "Invoice", "payment");

        Assert.Null(result);
    }

    [Fact]
    public async Task EnsureOptionalParentAsync_Returns_Null_When_Empty_Guid()
    {
        var result = await TenantGuard.EnsureOptionalParentAsync<Parent>(Guid.Empty, TenantA, NotFound(), p => p.TenantId, "Invoice", "payment");

        Assert.Null(result);
    }

    [Fact]
    public async Task EnsureOptionalParentAsync_Throws_When_Supplied_But_Missing()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TenantGuard.EnsureOptionalParentAsync(Guid.NewGuid(), TenantA, NotFound(), p => p.TenantId, "Invoice", "payment"));

        Assert.Contains("was not found", ex.Message);
    }

    [Fact]
    public async Task EnsureOptionalParentAsync_Throws_When_Cross_Tenant()
    {
        var parent = new Parent(TenantB);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TenantGuard.EnsureOptionalParentAsync(Guid.NewGuid(), TenantA, Found(parent), p => p.TenantId, "Invoice", "payment"));

        Assert.Contains("different tenant", ex.Message);
    }

    [Fact]
    public async Task EnsureOptionalParentAsync_Returns_Parent_When_Valid()
    {
        var parent = new Parent(TenantA);

        var result = await TenantGuard.EnsureOptionalParentAsync(Guid.NewGuid(), TenantA, Found(parent), p => p.TenantId, "Invoice", "payment");

        Assert.Same(parent, result);
    }
}
