using Ams.Application.Abstractions.Intelligence;
using Ams.Infrastructure.Services;
using Xunit;

namespace Ams.Application.Tests;

public sealed class AzureOpenAiProviderHealthTests
{
    [Fact]
    public async Task CheckHealth_ValidatesRouteWithoutCallingUnsupportedModelListEndpoint()
    {
        var handler=new RejectingHandler();
        var provider=new AzureOpenAiProvider(new HttpClient(handler));
        var context=new AiProviderContext(Guid.NewGuid(),"AZURE_OPENAI","AZURE_OPENAI","gpt-4.1-mini","gpt-4.1-mini","https://agencybinder-1226-resource.cognitiveservices.azure.com/",null,"2024-10-21",30);

        var health=await provider.CheckHealthAsync(context);

        Assert.Equal("HEALTHY",health.StatusCode);
        Assert.Equal(0,handler.RequestCount);
    }

    [Fact]
    public async Task CheckHealth_RejectsAnUnresolvedEndpointReference()
    {
        const string variable="AMS_TEST_MISSING_AZURE_OPENAI_ENDPOINT";
        Environment.SetEnvironmentVariable(variable,null);
        var provider=new AzureOpenAiProvider(new HttpClient(new RejectingHandler()));
        var context=new AiProviderContext(Guid.NewGuid(),"AZURE_OPENAI","AZURE_OPENAI","gpt-4.1-mini","gpt-4.1-mini",$"env://{variable}",null,"2024-10-21",30);

        var health=await provider.CheckHealthAsync(context);

        Assert.Equal("UNHEALTHY",health.StatusCode);
        Assert.Contains(variable,health.Message,StringComparison.Ordinal);
    }

    private sealed class RejectingHandler:HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            RequestCount++;
            throw new InvalidOperationException("Health validation must not make a network request.");
        }
    }
}
