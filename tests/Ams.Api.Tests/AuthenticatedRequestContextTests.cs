using System.Security.Claims;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.PolicyCertificates;
using Ams.Api.Controllers;
using Ams.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Ams.Api.Tests;

public sealed class AuthenticatedRequestContextTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void CanViewPolicy_AllowsMatchingTenantWithViewPermission()
    {
        var user = CreateUser(TenantA, "POLICY_VIEW");

        Assert.True(AuthenticatedRequestContext.CanViewPolicy(user, TenantA));
    }

    [Fact]
    public void CanViewPolicy_DeniesCrossTenantRequest()
    {
        var user = CreateUser(TenantA, "POLICY_VIEW");

        Assert.False(AuthenticatedRequestContext.CanViewPolicy(user, TenantB));
    }

    [Fact]
    public void CanViewPolicy_DeniesMissingTenantClaim()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("permission", "POLICY_VIEW")], "Test"));

        Assert.False(AuthenticatedRequestContext.CanViewPolicy(user, TenantA));
    }

    [Fact]
    public void CanManagePolicy_RequiresManagePermission()
    {
        Assert.False(AuthenticatedRequestContext.CanManagePolicy(CreateUser(TenantA, "POLICY_VIEW"), TenantA));
        Assert.True(AuthenticatedRequestContext.CanManagePolicy(CreateUser(TenantA, "POLICY_MANAGE"), TenantA));
    }

    [Fact]
    public void GetUserId_ReturnsAuthenticatedActor()
    {
        var user = CreateUser(TenantA, "POLICY_MANAGE");

        Assert.Equal(UserId, AuthenticatedRequestContext.GetUserId(user));
    }

    [Fact]
    public async Task CertificateCreate_ReplacesForgedActorWithAuthenticatedUser()
    {
        var service = new CapturingCertificateService();
        var controller = new PolicyCertificatesController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreateUser(TenantA, "POLICY_MANAGE") }
            }
        };
        var forgedActor = Guid.NewGuid();
        var request = new CreatePolicyCertificateRequest(
            TenantA, Guid.NewGuid(), "POL-1", "Account", "Holder", "Address", "ACORD25",
            DateTime.UtcNow, DateTime.UtcNow.AddYears(1), "General Liability", "CSR", "Issued",
            false, false, "Description", null, false, null, forgedActor);

        var result = await controller.Create(request, CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result);
        Assert.NotNull(service.CreatedRequest);
        Assert.Equal(UserId, service.CreatedRequest.CreatedByUserId);
        Assert.NotEqual(forgedActor, service.CreatedRequest.CreatedByUserId);
    }

    [Theory]
    [InlineData(typeof(PolicyCoveragesController))]
    [InlineData(typeof(PolicyEndorsementsController))]
    [InlineData(typeof(PolicyCertificatesController))]
    [InlineData(typeof(ServiceRequestsController))]
    public void ServicingController_RequiresAuthorization(Type controllerType)
        => Assert.NotNull(controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true).SingleOrDefault());

    private static ClaimsPrincipal CreateUser(Guid tenantId, string permission)
        => new(new ClaimsIdentity(
        [
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("permission", permission),
            new Claim(ClaimTypes.NameIdentifier, UserId.ToString())
        ], "Test"));

    private sealed class CapturingCertificateService : IPolicyCertificateService
    {
        public CreatePolicyCertificateRequest? CreatedRequest { get; private set; }

        public Task<Guid> CreateAsync(CreatePolicyCertificateRequest request, CancellationToken cancellationToken = default)
        {
            CreatedRequest = request;
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<PagedResult<PolicyCertificateDto>> SearchAsync(Guid tenantId, string? searchTerm, string? status, string? certificateType, int pageNumber = 1, int pageSize = 100, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<PolicyCertificateDto?> GetByIdAsync(Guid tenantId, Guid certificateId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<PolicyCertificateDto?> GetByNumberAsync(Guid tenantId, string certificateNumber, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task UpdateAsync(Guid certificateId, UpdatePolicyCertificateRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task RevokeAsync(Guid certificateId, RevokePolicyCertificateRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task RestoreAsync(Guid certificateId, RestorePolicyCertificateRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task MarkDeliveredAsync(Guid certificateId, PolicyCertificateActionRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task DeleteAsync(Guid certificateId, Guid tenantId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
