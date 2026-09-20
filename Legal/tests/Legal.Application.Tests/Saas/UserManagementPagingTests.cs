using System.Reflection;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Saas;
using Xunit;

namespace Legal.Application.Tests.Saas;

public sealed class UserManagementPagingTests
{
    [Fact]
    public async Task PageMembersAsync_ForwardsFiltersAndClampsPaging()
    {
        var repository = DispatchProxy.Create<ISaasRepository, RepositoryHandler>();
        var handler = (RepositoryHandler)(object)repository;
        var service = new UserManagementService(repository, DispatchProxy.Create<IUserAccountCreator, AccountCreatorHandler>());
        var tenantId = Guid.NewGuid();

        await service.PageMembersAsync(tenantId, "alex", "Active", 0, 500);

        Assert.Equal(nameof(ISaasRepository.PageMembersAsync), handler.LastMethod);
        Assert.Equal(tenantId, handler.LastArguments![0]);
        Assert.Equal("alex", handler.LastArguments[1]);
        Assert.Equal("Active", handler.LastArguments[2]);
        Assert.Equal(1, handler.LastArguments[3]);
        Assert.Equal(100, handler.LastArguments[4]);
    }

    public class RepositoryHandler : DispatchProxy
    {
        public string? LastMethod { get; private set; }
        public object?[]? LastArguments { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            LastMethod = targetMethod?.Name;
            LastArguments = args;
            return targetMethod?.Name == nameof(ISaasRepository.PageMembersAsync)
                ? Task.FromResult(new MemberPageDto([], 0, 0, 0, 0, 1, 100))
                : throw new NotSupportedException(targetMethod?.Name);
        }
    }

    public class AccountCreatorHandler : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new NotSupportedException(targetMethod?.Name);
    }
}