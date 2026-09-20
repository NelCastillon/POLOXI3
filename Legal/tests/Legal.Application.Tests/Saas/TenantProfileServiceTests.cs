using System.Reflection;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Saas;
using Xunit;

namespace Legal.Application.Tests.Saas;

public sealed class TenantProfileServiceTests
{
    [Fact]
    public async Task GetProfileAsync_ReturnsTenantProfile()
    {
        var tenantId = Guid.NewGuid();
        var (repository, stub) = CreateRepository();
        stub.Profile = new TenantProfileDto(tenantId, "Acme Legal", "acme-legal", "Active");
        var service = new TenantProfileService(repository);

        var result = await service.GetProfileAsync(tenantId);

        Assert.Equal(stub.Profile, result);
    }

    [Fact]
    public async Task UpdateProfileAsync_NormalizesAndPersistsValues()
    {
        var tenantId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var (repository, stub) = CreateRepository();
        stub.Profile = new TenantProfileDto(tenantId, "Old Name", "old-name", "Active");
        var service = new TenantProfileService(repository);

        var result = await service.UpdateProfileAsync(
            tenantId,
            actorUserId,
            new UpdateTenantProfileRequest("  New Name  ", "  New-Slug  "));

        Assert.Equal("New Name", result.Name);
        Assert.Equal("new-slug", result.Slug);
        Assert.Equal((tenantId, "New Name", "new-slug", actorUserId), stub.Update);
    }

    [Theory]
    [InlineData("", "valid-slug", "Organization name is required.")]
    [InlineData("Valid Name", "", "Workspace slug is required.")]
    [InlineData("Valid Name", "Invalid Slug", "Workspace slug may contain only lowercase letters, numbers, and single hyphens.")]
    [InlineData("Valid Name", "invalid--slug", "Workspace slug may contain only lowercase letters, numbers, and single hyphens.")]
    public async Task UpdateProfileAsync_RejectsInvalidInput(string name, string slug, string expectedMessage)
    {
        var tenantId = Guid.NewGuid();
        var (repository, stub) = CreateRepository();
        stub.Profile = new TenantProfileDto(tenantId, "Existing", "existing", "Active");
        var service = new TenantProfileService(repository);

        var exception = await Assert.ThrowsAsync<UserManagementForbiddenException>(
            () => service.UpdateProfileAsync(tenantId, null, new UpdateTenantProfileRequest(name, slug)));

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Null(stub.Update);
    }

    [Fact]
    public async Task UpdateProfileAsync_RejectsDuplicateSlug()
    {
        var tenantId = Guid.NewGuid();
        var (repository, stub) = CreateRepository();
        stub.Profile = new TenantProfileDto(tenantId, "Existing", "existing", "Active");
        stub.SlugExists = true;
        var service = new TenantProfileService(repository);

        var exception = await Assert.ThrowsAsync<UserManagementForbiddenException>(
            () => service.UpdateProfileAsync(tenantId, null, new UpdateTenantProfileRequest("New Name", "used-slug")));

        Assert.Equal("That workspace slug is already in use. Choose another.", exception.Message);
        Assert.Null(stub.Update);
    }

    private static (ISaasRepository Repository, StubSaasRepository Stub) CreateRepository()
    {
        var repository = DispatchProxy.Create<ISaasRepository, StubSaasRepository>();
        return (repository, (StubSaasRepository)(object)repository);
    }

    public class StubSaasRepository : DispatchProxy
    {
        public TenantProfileDto? Profile { get; set; }
        public bool SlugExists { get; set; }
        public (Guid TenantId, string Name, string Slug, Guid? ActorUserId)? Update { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            ArgumentNullException.ThrowIfNull(args);

            return targetMethod.Name switch
            {
                nameof(ISaasRepository.GetTenantProfileAsync) => Task.FromResult(Profile),
                nameof(ISaasRepository.TenantSlugExistsAsync) => Task.FromResult(SlugExists),
                nameof(ISaasRepository.UpdateTenantProfileAsync) => CaptureUpdate(args),
                _ => throw new NotSupportedException($"Unexpected repository call: {targetMethod.Name}")
            };
        }

        private Task CaptureUpdate(object?[] args)
        {
            Update = ((Guid)args[0]!, (string)args[1]!, (string)args[2]!, (Guid?)args[3]);
            return Task.CompletedTask;
        }
    }
}
