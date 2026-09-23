using System.Reflection;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Saas;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legal.Application.Tests.Saas;

public sealed class ActivityServiceTests
{
    [Fact]
    public async Task ListAuditEventsForUserAsync_ForwardsTenantAndUserAndClampsTake()
    {
        var (repository, handler) = CreateRepository();
        var service = new ActivityService(repository, NullLogger<ActivityService>.Instance);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await service.ListAuditEventsForUserAsync(tenantId, userId, 30, 10_000);

        Assert.Equal(nameof(ISaasRepository.ListAuditEventsForUserAsync), handler.LastMethod);
        Assert.Equal(tenantId, handler.LastArguments![0]);
        Assert.Equal(userId, handler.LastArguments[1]);
        Assert.Equal(500, handler.LastArguments[3]);
        Assert.InRange((DateTime)handler.LastArguments[2]!, DateTime.UtcNow.AddDays(-30).AddSeconds(-2), DateTime.UtcNow.AddDays(-30).AddSeconds(2));
    }

    [Fact]
    public async Task PageAuditEventsForUserAsync_ForwardsSearchAndClampsPaging()
    {
        var (repository, handler) = CreateRepository();
        var service = new ActivityService(repository, NullLogger<ActivityService>.Instance);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await service.PageAuditEventsForUserAsync(tenantId, userId, 10_000, "matter", -4, 1_000);

        Assert.Equal(nameof(ISaasRepository.PageAuditEventsForUserAsync), handler.LastMethod);
        Assert.Equal(tenantId, handler.LastArguments![0]);
        Assert.Equal(userId, handler.LastArguments[1]);
        Assert.Equal("matter", handler.LastArguments[3]);
        Assert.Equal(1, handler.LastArguments[4]);
        Assert.Equal(100, handler.LastArguments[5]);
        Assert.InRange((DateTime)handler.LastArguments[2]!, DateTime.UtcNow.AddDays(-365).AddSeconds(-2), DateTime.UtcNow.AddDays(-365).AddSeconds(2));
    }

    [Fact]
    public async Task SummarizeUsageForUserAsync_ForwardsTenantAndUserAndClampsWindow()
    {
        var (repository, handler) = CreateRepository();
        var service = new ActivityService(repository, NullLogger<ActivityService>.Instance);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await service.SummarizeUsageForUserAsync(tenantId, userId, 10_000);

        Assert.Equal(nameof(ISaasRepository.SummarizeUsageForUserAsync), handler.LastMethod);
        Assert.Equal(tenantId, handler.LastArguments![0]);
        Assert.Equal(userId, handler.LastArguments[1]);
        Assert.InRange((DateTime)handler.LastArguments[2]!, DateTime.UtcNow.AddDays(-365).AddSeconds(-2), DateTime.UtcNow.AddDays(-365).AddSeconds(2));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task UserScopedQueries_RejectEmptyScope(bool emptyTenant, bool emptyUser)
    {
        var (repository, _) = CreateRepository();
        var service = new ActivityService(repository, NullLogger<ActivityService>.Instance);
        var tenantId = emptyTenant ? Guid.Empty : Guid.NewGuid();
        var userId = emptyUser ? Guid.Empty : Guid.NewGuid();

        await Assert.ThrowsAsync<UserManagementForbiddenException>(
            () => service.ListAuditEventsForUserAsync(tenantId, userId, 30, 100));
        await Assert.ThrowsAsync<UserManagementForbiddenException>(
            () => service.SummarizeUsageForUserAsync(tenantId, userId, 30));
    }

    private static (ISaasRepository Repository, RepositoryHandler Handler) CreateRepository()
    {
        var repository = DispatchProxy.Create<ISaasRepository, RepositoryHandler>();
        return (repository, (RepositoryHandler)(object)repository);
    }

    public class RepositoryHandler : DispatchProxy
    {
        public string? LastMethod { get; private set; }
        public object?[]? LastArguments { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            LastMethod = targetMethod?.Name;
            LastArguments = args;

            return targetMethod?.Name switch
            {
                nameof(ISaasRepository.ListAuditEventsForUserAsync) => Task.FromResult<IReadOnlyList<AuditEventDto>>([]),
                nameof(ISaasRepository.PageAuditEventsForUserAsync) => Task.FromResult(new PagedResultDto<AuditEventDto>([], 0, 1, 100)),
                nameof(ISaasRepository.SummarizeUsageForUserAsync) => Task.FromResult<IReadOnlyList<UsageSummaryDto>>([]),
                _ => throw new NotSupportedException(targetMethod?.Name)
            };
        }
    }
}
