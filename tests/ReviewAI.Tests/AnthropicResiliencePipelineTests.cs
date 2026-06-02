using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ReviewAI.Api.Configuration;
using ReviewAI.Core.Configuration;

namespace ReviewAI.Tests;

public class AnthropicResiliencePipelineTests
{
    private static HttpClient BuildClient(ResilienceOptions options, SequenceHandler handler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAnthropicResilientHttpClient(options);
        services.AddHttpClient(AnthropicHttpClientExtensions.AnthropicClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(AnthropicHttpClientExtensions.AnthropicClientName);
    }

    [Fact]
    public async Task Pipeline_RetriesTransientFailures_ThenSucceeds()
    {
        var options = new ResilienceOptions { MaxRetries = 3, RetryBaseDelaySeconds = 0, AttemptTimeoutSeconds = 30, TotalTimeoutSeconds = 120 };
        var handler = new SequenceHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        var client = BuildClient(options, handler);

        var response = await client.GetAsync("https://localhost/test");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.CallCount.Should().Be(3);
    }

    [Fact]
    public async Task Pipeline_WhenFailuresPersist_StopsAfterMaxRetries()
    {
        var options = new ResilienceOptions { MaxRetries = 2, RetryBaseDelaySeconds = 0, AttemptTimeoutSeconds = 30, TotalTimeoutSeconds = 120 };
        var handler = new SequenceHandler(HttpStatusCode.ServiceUnavailable);
        var client = BuildClient(options, handler);

        var response = await client.GetAsync("https://localhost/test");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        handler.CallCount.Should().Be(3);
    }

    private sealed class SequenceHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statuses = new(statuses);

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var status = _statuses.Count > 0 ? _statuses.Dequeue() : HttpStatusCode.ServiceUnavailable;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }
}
