using System.Net;
using System.Text;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence;
using Legal.Infrastructure.Intelligence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class CourtListenerCitatorProviderTests
{
    private const string Identity = "Raffles v. Wichelhaus";

    // When CourtListener cannot locate the authority, the provider returns NOTHING for it so the
    // authority stays "Unknown" — it never fabricates a good-law status.
    [Fact]
    public async Task GetTreatments_AuthorityNotFound_ReturnsNothing()
    {
        using var client = new HttpClient(new SequenceHandler(["""{"results":[]}"""]));
        var provider = Create(client);

        var result = await provider.GetTreatmentsAsync(Guid.NewGuid(), [Identity]);

        Assert.False(result.ContainsKey(Identity));
    }

    // Located + no negative citing passage => advisory "Cited"/GoodLaw treatment, provenance-tagged.
    [Fact]
    public async Task GetTreatments_LocatedWithoutNegative_ReturnsAdvisoryGoodLaw()
    {
        using var client = new HttpClient(new SequenceHandler(
        [
            // 1) locate
            """{"results":[{"caseName":"Raffles v. Wichelhaus","court":"Court of Exchequer","absolute_url":"/opinion/1/","snippet":"Opinion text"}]}""",
            // 2) negative-treatment pass (none)
            """{"results":[]}""",
        ]));
        var provider = Create(client);

        var result = await provider.GetTreatmentsAsync(Guid.NewGuid(), [Identity]);

        var treatment = Assert.Single(result[Identity]);
        Assert.Equal("GoodLaw", treatment.FlagLevelCode);
        Assert.Equal("Cited", treatment.TreatmentCode);
        Assert.Equal(CourtListenerCitatorProvider.ProviderCode, treatment.SourceProviderCode);
        Assert.Equal(Guid.Empty, treatment.CitationTreatmentId);
    }

    // Located + a citing passage naming the authority with explicit negative language => Negative flag.
    [Fact]
    public async Task GetTreatments_NegativeCitingPassage_ReturnsNegativeTreatment()
    {
        using var client = new HttpClient(new SequenceHandler(
        [
            """{"results":[{"caseName":"Raffles v. Wichelhaus","court":"Court of Exchequer","absolute_url":"/opinion/1/","snippet":"Opinion text"}]}""",
            """{"results":[{"caseName":"Later Case","court":"Some Court","absolute_url":"/opinion/2/","snippet":"The holding in Raffles is overruled to the extent it conflicts with modern doctrine."}]}""",
        ]));
        var provider = Create(client);

        var result = await provider.GetTreatmentsAsync(Guid.NewGuid(), [Identity]);

        var treatment = Assert.Single(result[Identity]);
        Assert.Equal("Negative", treatment.FlagLevelCode);
        Assert.Equal("Overruled", treatment.TreatmentCode);
        Assert.Equal(CourtListenerCitatorProvider.ProviderCode, treatment.SourceProviderCode);
    }

    // A negative word in a passage that does NOT name the authority must not mis-attribute a treatment.
    [Fact]
    public async Task GetTreatments_NegativeWordWithoutAuthorityName_StaysGoodLawAdvisory()
    {
        using var client = new HttpClient(new SequenceHandler(
        [
            """{"results":[{"caseName":"Raffles v. Wichelhaus","court":"Court of Exchequer","absolute_url":"/opinion/1/","snippet":"Opinion text"}]}""",
            """{"results":[{"caseName":"Unrelated","court":"Some Court","absolute_url":"/opinion/2/","snippet":"Some other precedent was overruled last year."}]}""",
        ]));
        var provider = Create(client);

        var result = await provider.GetTreatmentsAsync(Guid.NewGuid(), [Identity]);

        var treatment = Assert.Single(result[Identity]);
        Assert.Equal("GoodLaw", treatment.FlagLevelCode);
    }

    // Provider disabled by configuration => no calls, nothing returned (everything stays Unknown).
    [Fact]
    public async Task GetTreatments_Disabled_ReturnsEmpty()
    {
        using var client = new HttpClient(new SequenceHandler(["""{"results":[]}"""]));
        var provider = Create(client, courtListenerEnabled: false);

        var result = await provider.GetTreatmentsAsync(Guid.NewGuid(), [Identity]);

        Assert.Empty(result);
    }

    private static CourtListenerCitatorProvider Create(HttpClient client, bool courtListenerEnabled = true)
        => new(client, new StubConfigurationSource(Configuration(courtListenerEnabled)),
            NullLogger<CourtListenerCitatorProvider>.Instance);

    private static WideLegalGroundingConfiguration Configuration(bool courtListenerEnabled) => new(
        true, 3, 5, 1, 10, courtListenerEnabled, "https://www.courtlistener.com", string.Empty,
        false, string.Empty, string.Empty, string.Empty, false, string.Empty);

    private sealed class StubConfigurationSource(WideLegalGroundingConfiguration configuration) : ILegalGroundingConfigurationSource
    {
        public Task<WideLegalGroundingConfiguration> GetConfigurationAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(configuration);
    }

    // Returns each queued JSON payload in order; repeats the last once the queue is exhausted.
    private sealed class SequenceHandler(IReadOnlyList<string> payloads) : HttpMessageHandler
    {
        private int _index;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var payload = payloads[Math.Min(_index, payloads.Count - 1)];
            _index++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            });
        }
    }
}
