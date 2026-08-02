using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyLifecycle;
using Xunit;

namespace Ams.Application.Tests;

public sealed class PolicyLifecycleServiceTests
{
    [Fact]
    public async Task GetWorkspaceAsync_ForwardsTenantAndPolicyScope()
    {
        var tenantId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        var expected = new PolicyServicingWorkspaceDto { TenantId = tenantId, PolicyId = policyId };
        var repository = new FakePolicyLifecycleRepository { Workspace = expected };
        var service = new PolicyLifecycleService(repository);

        var result = await service.GetWorkspaceAsync(tenantId, policyId);

        Assert.Same(expected, result);
        Assert.Equal(tenantId, repository.TenantId);
        Assert.Equal(policyId, repository.PolicyId);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task GetWorkspaceAsync_RejectsMissingScopeBeforeRepositoryAccess(bool missingTenant, bool missingPolicy)
    {
        var repository = new FakePolicyLifecycleRepository();
        var service = new PolicyLifecycleService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetWorkspaceAsync(
            missingTenant ? Guid.Empty : Guid.NewGuid(),
            missingPolicy ? Guid.Empty : Guid.NewGuid()));

        Assert.Equal(0, repository.WorkspaceReadCount);
    }

    [Fact]
    public async Task CreateActivityAsync_NormalizesAndForwardsRequest()
    {
        var request = new CreatePolicyServicingActivityRequest
        {
            TenantId = Guid.NewGuid(),
            PolicyId = Guid.NewGuid(),
            ActivityTypeCode = "  Email ",
            Subject = "  Renewal documents requested ",
            Notes = "  Follow up tomorrow. ",
            ChannelCode = " Email ",
            OutcomeCode = " Pending "
        };
        var expected = new PolicyServicingActionResultDto { RecordId = Guid.NewGuid(), RecordTypeCode = "Activity" };
        var repository = new FakePolicyLifecycleRepository { ActionResult = expected };
        var service = new PolicyLifecycleService(repository);

        var result = await service.CreateActivityAsync(request);

        Assert.Same(expected, result);
        Assert.Same(request, repository.ActivityRequest);
        Assert.Equal("Email", request.ActivityTypeCode);
        Assert.Equal("Renewal documents requested", request.Subject);
        Assert.Equal("Follow up tomorrow.", request.Notes);
        Assert.Equal("Email", request.ChannelCode);
        Assert.Equal("Pending", request.OutcomeCode);
    }

    [Theory]
    [InlineData("", "Subject")]
    [InlineData("Email", "")]
    public async Task CreateActivityAsync_RejectsRequiredTextBeforeRepositoryAccess(string activityType, string subject)
    {
        var repository = new FakePolicyLifecycleRepository();
        var service = new PolicyLifecycleService(repository);
        var request = new CreatePolicyServicingActivityRequest
        {
            TenantId = Guid.NewGuid(),
            PolicyId = Guid.NewGuid(),
            ActivityTypeCode = activityType,
            Subject = subject
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateActivityAsync(request));

        Assert.Null(repository.ActivityRequest);
    }

    [Fact]
    public async Task SendCommunicationAsync_NormalizesAndForwardsRequest()
    {
        var request = new SendPolicyCommunicationRequest
        {
            TenantId = Guid.NewGuid(),
            PolicyId = Guid.NewGuid(),
            ChannelCode = " Email ",
            Recipient = " insured@example.com ",
            Subject = " Policy documents ",
            Body = " Attached documents are ready. "
        };
        var expected = new PolicyServicingActionResultDto { RecordId = Guid.NewGuid(), RecordTypeCode = "Communication", StatusCode = "Queued" };
        var repository = new FakePolicyLifecycleRepository { ActionResult = expected };
        var service = new PolicyLifecycleService(repository);

        var result = await service.SendCommunicationAsync(request);

        Assert.Same(expected, result);
        Assert.Same(request, repository.CommunicationRequest);
        Assert.Equal("Email", request.ChannelCode);
        Assert.Equal("insured@example.com", request.Recipient);
        Assert.Equal("Policy documents", request.Subject);
        Assert.Equal("Attached documents are ready.", request.Body);
    }

    [Theory]
    [InlineData("", "recipient", "subject", "body")]
    [InlineData("Email", "", "subject", "body")]
    [InlineData("Email", "recipient", "", "body")]
    [InlineData("Email", "recipient", "subject", "")]
    public async Task SendCommunicationAsync_RejectsRequiredTextBeforeRepositoryAccess(string channel, string recipient, string subject, string body)
    {
        var repository = new FakePolicyLifecycleRepository();
        var service = new PolicyLifecycleService(repository);
        var request = new SendPolicyCommunicationRequest
        {
            TenantId = Guid.NewGuid(),
            PolicyId = Guid.NewGuid(),
            ChannelCode = channel,
            Recipient = recipient,
            Subject = subject,
            Body = body
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendCommunicationAsync(request));

        Assert.Null(repository.CommunicationRequest);
    }

    private sealed class FakePolicyLifecycleRepository : IPolicyLifecycleRepository
    {
        public PolicyServicingWorkspaceDto? Workspace { get; set; }
        public PolicyServicingActionResultDto ActionResult { get; set; } = new();
        public Guid TenantId { get; private set; }
        public Guid PolicyId { get; private set; }
        public int WorkspaceReadCount { get; private set; }
        public CreatePolicyServicingActivityRequest? ActivityRequest { get; private set; }
        public SendPolicyCommunicationRequest? CommunicationRequest { get; private set; }

        public Task<PolicyServicingWorkspaceDto?> GetWorkspaceAsync(Guid tenantId, Guid policyId, CancellationToken cancellationToken = default)
        {
            TenantId = tenantId;
            PolicyId = policyId;
            WorkspaceReadCount++;
            return Task.FromResult(Workspace);
        }

        public Task<PolicyServicingActionResultDto> CreateActivityAsync(CreatePolicyServicingActivityRequest request, CancellationToken cancellationToken = default)
        {
            ActivityRequest = request;
            return Task.FromResult(ActionResult);
        }

        public Task<PolicyServicingActionResultDto> SendCommunicationAsync(SendPolicyCommunicationRequest request, CancellationToken cancellationToken = default)
        {
            CommunicationRequest = request;
            return Task.FromResult(ActionResult);
        }

        public Task<IReadOnlyList<PolicyLifecycleOptionDto>> GetOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PolicyLifecycleOptionDto>>([]);

        public Task<IReadOnlyList<PolicyLifecycleWorkbenchRowDto>> GetWorkbenchAsync(Guid tenantId, string? mode = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PolicyLifecycleWorkbenchRowDto>>([]);

        public Task<PolicyLifecycleDetailDto?> GetDetailAsync(Guid tenantId, Guid policyId, CancellationToken cancellationToken = default)
            => Task.FromResult<PolicyLifecycleDetailDto?>(null);

        public Task<Guid> CreateTransactionAsync(CreatePolicyLifecycleTransactionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid());

        public Task TransitionTransactionAsync(Guid policyTransactionId, TransitionPolicyLifecycleTransactionRequest request, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
