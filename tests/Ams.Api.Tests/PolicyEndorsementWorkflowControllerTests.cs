using System.Security.Claims;
using Ams.Api.Controllers;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyEndorsements;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Ams.Api.Tests;

public sealed class PolicyEndorsementWorkflowControllerTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task GetWorkflowDetail_DeniesCrossTenantRequest()
    {
        var service = new CapturingPolicyEndorsementService();
        var controller = CreateController(service, CreateUser(TenantA, "ENDORSEMENT_VIEW"));

        var result = await controller.GetWorkflowDetail(Guid.NewGuid(), TenantB, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, service.WorkflowDetailCalls);
    }

    [Fact]
    public async Task CreateTransaction_ReplacesForgedActorAndDoesNotTrustBackdateFlag()
    {
        var service = new CapturingPolicyEndorsementService();
        var controller = CreateController(service, CreateUser(TenantA, "ENDORSEMENT_CREATE"));
        var request = new CreatePolicyEndorsementTransactionRequest
        {
            TenantId = TenantA,
            PolicyId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid(),
            AllowBackdate = true
        };

        var result = await controller.CreateTransaction(request, CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result);
        Assert.Same(request, service.CreatedTransaction);
        Assert.Equal(UserId, request.CreatedByUserId);
        Assert.False(request.AllowBackdate);
    }

    [Fact]
    public async Task Transition_ForwardsOnlyAuthenticatedActorAndPermissions()
    {
        var service = new CapturingPolicyEndorsementService();
        var controller = CreateController(service, CreateUser(TenantA, "ENDORSEMENT_SUBMIT"));
        var request = new TransitionPolicyEndorsementRequest
        {
            TenantId = TenantA,
            ActorUserId = Guid.NewGuid(),
            GrantedPermissions = ["NAV_ALL"]
        };

        var result = await controller.Transition(Guid.NewGuid(), request, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Same(request, service.TransitionRequest);
        Assert.Equal(UserId, request.ActorUserId);
        Assert.Equal(["ENDORSEMENT_SUBMIT"], request.GrantedPermissions);
    }

    [Fact]
    public async Task Reverse_GrantsBackdateOnlyFromAuthenticatedPermission()
    {
        var service = new CapturingPolicyEndorsementService();
        var controller = CreateController(service, CreateUser(TenantA, "ENDORSEMENT_REVERSE", "ENDORSEMENT_BACKDATE"));
        var request = new ReversePolicyEndorsementRequest
        {
            TenantId = TenantA,
            ActorUserId = Guid.NewGuid(),
            GrantedPermissions = [],
            AllowBackdate = false
        };

        var result = await controller.Reverse(Guid.NewGuid(), request, CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result);
        Assert.Same(request, service.ReverseRequest);
        Assert.Equal(UserId, request.ActorUserId);
        Assert.True(request.AllowBackdate);
        Assert.Contains("ENDORSEMENT_REVERSE", request.GrantedPermissions);
        Assert.Contains("ENDORSEMENT_BACKDATE", request.GrantedPermissions);
    }

    [Fact]
    public async Task DecideApproval_RequiresApprovalPermission()
    {
        var service = new CapturingPolicyEndorsementService();
        var controller = CreateController(service, CreateUser(TenantA, "ENDORSEMENT_VIEW"));

        var result = await controller.DecideApproval(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DecidePolicyEndorsementApprovalRequest { TenantId = TenantA },
            CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Null(service.ApprovalRequest);
    }

    private static PolicyEndorsementsController CreateController(IPolicyEndorsementService service, ClaimsPrincipal user)
        => new(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };

    private static ClaimsPrincipal CreateUser(Guid tenantId, params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new("tenant_id", tenantId.ToString()),
            new(ClaimTypes.NameIdentifier, UserId.ToString())
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private sealed class CapturingPolicyEndorsementService : IPolicyEndorsementService
    {
        public int WorkflowDetailCalls { get; private set; }
        public CreatePolicyEndorsementTransactionRequest? CreatedTransaction { get; private set; }
        public TransitionPolicyEndorsementRequest? TransitionRequest { get; private set; }
        public DecidePolicyEndorsementApprovalRequest? ApprovalRequest { get; private set; }
        public ReversePolicyEndorsementRequest? ReverseRequest { get; private set; }

        public Task<PolicyEndorsementWorkflowDetailDto?> GetWorkflowDetailAsync(Guid tenantId, Guid endorsementId, CancellationToken cancellationToken = default)
        {
            WorkflowDetailCalls++;
            return Task.FromResult<PolicyEndorsementWorkflowDetailDto?>(null);
        }

        public Task<Guid> CreateTransactionAsync(CreatePolicyEndorsementTransactionRequest request, CancellationToken cancellationToken = default)
        {
            CreatedTransaction = request;
            return Task.FromResult(Guid.NewGuid());
        }

        public Task TransitionAsync(Guid endorsementId, TransitionPolicyEndorsementRequest request, CancellationToken cancellationToken = default)
        {
            TransitionRequest = request;
            return Task.CompletedTask;
        }

        public Task DecideApprovalAsync(Guid endorsementId, Guid approvalId, DecidePolicyEndorsementApprovalRequest request, CancellationToken cancellationToken = default)
        {
            ApprovalRequest = request;
            return Task.CompletedTask;
        }

        public Task<Guid> ReverseAsync(Guid endorsementId, ReversePolicyEndorsementRequest request, CancellationToken cancellationToken = default)
        {
            ReverseRequest = request;
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<PolicyEndorsementCenterDto> GetCenterAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PolicyEndorsementOptionDto>> GetOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PolicyEndorsementDetailDto?> GetDetailAsync(Guid tenantId, Guid endorsementId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PolicyEndorsementPolicyWorkspaceDto?> GetPolicyWorkspaceAsync(Guid tenantId, Guid policyId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Guid> CreateAsync(CreatePolicyEndorsementRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveDraftAsync(Guid endorsementId, SavePolicyEndorsementDraftRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(Guid endorsementId, UpdatePolicyEndorsementRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateStatusAsync(Guid endorsementId, UpdatePolicyEndorsementStatusRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Guid> AddActivityAsync(AddPolicyEndorsementActivityRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Guid> UpsertDeltaAsync(UpsertPolicyEndorsementDeltaRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ArchiveAsync(Guid endorsementId, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
