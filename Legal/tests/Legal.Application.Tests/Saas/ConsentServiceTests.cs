using System.Reflection;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Saas;
using Xunit;

namespace Legal.Application.Tests.Saas;

public sealed class ConsentServiceTests
{
    [Fact]
    public async Task RecordConsentForActiveAgreementsAsync_PersistsEachRequiredAgreementSnapshot()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var termsId = Guid.NewGuid();
        var privacyId = Guid.NewGuid();
        var (repository, stub) = CreateRepository();
        stub.Agreements =
        [
            Agreement(termsId, AgreementType.TermsOfService, "1.0", "terms-hash", requiresConsent: true, sortOrder: 1),
            Agreement(privacyId, AgreementType.PrivacyPolicy, "2.0", "privacy-hash", requiresConsent: true, sortOrder: 2),
            Agreement(Guid.NewGuid(), "Notice", "1.0", "notice-hash", requiresConsent: false, sortOrder: 3)
        ];
        var service = new ConsentService(repository);

        await service.RecordConsentForActiveAgreementsAsync(
            userId,
            tenantId,
            "owner@example.com",
            ConsentAcceptanceMethod.ClickwrapCheckbox,
            "203.0.113.10",
            "Test Browser",
            "correlation-123");

        Assert.Collection(
            stub.RecordedConsents,
            terms => AssertConsent(terms, userId, tenantId, termsId, AgreementType.TermsOfService, "1.0", "terms-hash"),
            privacy => AssertConsent(privacy, userId, tenantId, privacyId, AgreementType.PrivacyPolicy, "2.0", "privacy-hash"));
    }

    [Fact]
    public async Task RecordConsentForActiveAgreementsAsync_PropagatesPersistenceFailure()
    {
        var (repository, stub) = CreateRepository();
        stub.Agreements = [Agreement(Guid.NewGuid(), AgreementType.TermsOfService, "1.0", "hash", true, 1)];
        stub.RecordFailure = new InvalidOperationException("Consent insert failed.");
        var service = new ConsentService(repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordConsentForActiveAgreementsAsync(
                Guid.NewGuid(), null, "owner@example.com", ConsentAcceptanceMethod.ClickwrapCheckbox,
                null, null, "correlation-123"));

        Assert.Equal("Consent insert failed.", exception.Message);
    }

    private static LegalAgreementDto Agreement(
        Guid id,
        string type,
        string version,
        string hash,
        bool requiresConsent,
        int sortOrder)
        => new(id, type, version, type, "Agreement body", hash, DateTime.UtcNow, requiresConsent, sortOrder);

    private static void AssertConsent(
        RecordConsentRequest request,
        Guid userId,
        Guid tenantId,
        Guid agreementId,
        string agreementType,
        string version,
        string hash)
    {
        Assert.Equal(userId, request.UserId);
        Assert.Equal(tenantId, request.TenantId);
        Assert.Equal("owner@example.com", request.Email);
        Assert.Equal(agreementId, request.AgreementId);
        Assert.Equal(agreementType, request.AgreementType);
        Assert.Equal(version, request.AgreementVersion);
        Assert.Equal(hash, request.ContentHash);
        Assert.Equal(ConsentAcceptanceMethod.ClickwrapCheckbox, request.AcceptanceMethod);
        Assert.Equal("203.0.113.10", request.IpAddress);
        Assert.Equal("Test Browser", request.UserAgent);
        Assert.Equal("correlation-123", request.CorrelationId);
    }

    private static (ISaasRepository Repository, StubSaasRepository Stub) CreateRepository()
    {
        var repository = DispatchProxy.Create<ISaasRepository, StubSaasRepository>();
        return (repository, (StubSaasRepository)(object)repository);
    }

    public class StubSaasRepository : DispatchProxy
    {
        public IReadOnlyList<LegalAgreementDto> Agreements { get; set; } = [];
        public List<RecordConsentRequest> RecordedConsents { get; } = [];
        public Exception? RecordFailure { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            ArgumentNullException.ThrowIfNull(args);

            return targetMethod.Name switch
            {
                nameof(ISaasRepository.GetActiveAgreementsAsync) => Task.FromResult(Agreements),
                nameof(ISaasRepository.RecordConsentAsync) => Record((RecordConsentRequest)args[0]!),
                _ => throw new NotSupportedException($"Unexpected repository call: {targetMethod.Name}")
            };
        }

        private Task Record(RecordConsentRequest request)
        {
            if (RecordFailure is not null)
                return Task.FromException(RecordFailure);

            RecordedConsents.Add(request);
            return Task.CompletedTask;
        }
    }
}
