using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Xunit;

namespace Ams.Application.Tests;

public sealed class PolicyAccountingServiceTests
{
    [Fact]
    public async Task GetPolicyDashboardAsync_ForwardsTenantAndPolicyScope()
    {
        var tenantId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        var expected = new PolicyAccountingDashboardDto(policyId, Guid.NewGuid(), "POL-100", "AgencyBill", "Synchronized", "USD", 4500m, 150m, 90m, 4740m, 4740m, 15m, 675m, 3825m, 12, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        var repository = new FakePolicyAccountingRepository { Dashboard = expected };
        var service = new PolicyAccountingService(repository);

        var result = await service.GetPolicyDashboardAsync(tenantId, policyId);

        Assert.Same(expected, result);
        Assert.Equal(tenantId, repository.TenantId);
        Assert.Equal(policyId, repository.PolicyId);
    }

    [Fact]
    public async Task GetPolicyDashboardAsync_RejectsMissingTenantBeforeRepositoryAccess()
    {
        var repository = new FakePolicyAccountingRepository();
        var service = new PolicyAccountingService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetPolicyDashboardAsync(Guid.Empty, Guid.NewGuid()));

        Assert.Equal(0, repository.DashboardReadCount);
    }

    [Fact]
    public async Task RemitCarrierPayableAsync_ForwardsValidatedTenantRequest()
    {
        var carrierPayableId = Guid.NewGuid();
        var request = new RemitCarrierPayableRequest(Guid.NewGuid(), 1250m, DateOnly.FromDateTime(DateTime.UtcNow), "ACH-100", Guid.NewGuid());
        var repository = new FakePolicyAccountingRepository { RemittanceJournalEntryId = Guid.NewGuid() };
        var service = new PolicyAccountingService(repository);

        var result = await service.RemitCarrierPayableAsync(carrierPayableId, request);

        Assert.Equal(repository.RemittanceJournalEntryId, result);
        Assert.Equal(carrierPayableId, repository.CarrierPayableId);
        Assert.Same(request, repository.RemittanceRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RemitCarrierPayableAsync_RejectsNonPositiveAmountBeforeRepositoryAccess(decimal amount)
    {
        var repository = new FakePolicyAccountingRepository();
        var service = new PolicyAccountingService(repository);
        var request = new RemitCarrierPayableRequest(Guid.NewGuid(), amount, DateOnly.FromDateTime(DateTime.UtcNow), null, null);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.RemitCarrierPayableAsync(Guid.NewGuid(), request));

        Assert.Equal(0, repository.RemittanceCount);
    }

    [Fact]
    public async Task EmailInvoiceAsync_ForwardsValidatedPolicyInvoiceAndRecipient()
    {
        var policyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var request = new EmailPolicyInvoiceRequest(Guid.NewGuid(), "insured@example.com", Guid.NewGuid());
        var expected = new InvoiceDeliveryDispatchDto(Guid.NewGuid(), invoiceId, request.Recipient, "Queued", DateTime.UtcNow);
        var repository = new FakePolicyAccountingRepository { InvoiceDelivery = expected };
        var service = new PolicyAccountingService(repository);

        var result = await service.EmailInvoiceAsync(policyId, invoiceId, request);

        Assert.Same(expected, result);
        Assert.Equal(policyId, repository.EmailPolicyId);
        Assert.Equal(invoiceId, repository.EmailInvoiceId);
        Assert.Same(request, repository.EmailRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task EmailInvoiceAsync_RejectsInvalidRecipientBeforeRepositoryAccess(string recipient)
    {
        var repository = new FakePolicyAccountingRepository();
        var service = new PolicyAccountingService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() => service.EmailInvoiceAsync(Guid.NewGuid(), Guid.NewGuid(), new EmailPolicyInvoiceRequest(Guid.NewGuid(), recipient, null)));

        Assert.Equal(0, repository.EmailCount);
    }

    [Fact]
    public async Task GetPolicyDashboardAsync_RejectsMissingPolicyBeforeRepositoryAccess()
    {
        var repository = new FakePolicyAccountingRepository();
        var service = new PolicyAccountingService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetPolicyDashboardAsync(Guid.NewGuid(), Guid.Empty));

        Assert.Equal(0, repository.DashboardReadCount);
    }

    private sealed class FakePolicyAccountingRepository : IPolicyAccountingRepository
    {
        public PolicyAccountingDashboardDto? Dashboard { get; init; }
        public Guid TenantId { get; private set; }
        public Guid PolicyId { get; private set; }
        public int DashboardReadCount { get; private set; }
        public Guid RemittanceJournalEntryId { get; init; }
        public Guid CarrierPayableId { get; private set; }
        public RemitCarrierPayableRequest? RemittanceRequest { get; private set; }
        public int RemittanceCount { get; private set; }
        public InvoiceDeliveryDispatchDto? InvoiceDelivery { get; init; }
        public Guid EmailPolicyId { get; private set; }
        public Guid EmailInvoiceId { get; private set; }
        public EmailPolicyInvoiceRequest? EmailRequest { get; private set; }
        public int EmailCount { get; private set; }

        public Task ProcessPolicyCreatedEventAsync(Guid eventId, Guid tenantId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<PolicyAccountingDashboardDto?> GetPolicyDashboardAsync(Guid tenantId, Guid policyId, CancellationToken cancellationToken = default)
        {
            TenantId = tenantId;
            PolicyId = policyId;
            DashboardReadCount++;
            return Task.FromResult(Dashboard);
        }

        public Task<Guid> RemitCarrierPayableAsync(Guid carrierPayableId, RemitCarrierPayableRequest request, CancellationToken cancellationToken = default)
        {
            CarrierPayableId = carrierPayableId;
            RemittanceRequest = request;
            RemittanceCount++;
            return Task.FromResult(RemittanceJournalEntryId);
        }

        public Task<InvoiceDeliveryDispatchDto> EmailInvoiceAsync(Guid policyId, Guid invoiceId, EmailPolicyInvoiceRequest request, CancellationToken cancellationToken = default)
        {
            EmailPolicyId = policyId;
            EmailInvoiceId = invoiceId;
            EmailRequest = request;
            EmailCount++;
            return Task.FromResult(InvoiceDelivery!);
        }
    }
}
