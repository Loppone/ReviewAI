using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Polly.CircuitBreaker;
using ReviewAI.Api.Configuration;
using ReviewAI.Core.Configuration;

namespace ReviewAI.Tests;

/// <summary>
/// Provider-agnostic verification of the resilience pipeline. The same infrastructure exercises
/// both named clients ("Anthropic" and "GitHub") so the retry/timeout/circuit-breaker behaviour
/// is covered for both without duplicated suites.
/// </summary>
public class ResiliencePipelineTests
{
    public static TheoryData<string> ClientNames =>
    [
        ResilientHttpClientExtensions.AnthropicClientName,
        ResilientHttpClientExtensions.GitHubClientName
    ];

    private static ClientResilienceOptions Options(
        int maxRetries = 3,
        int minimumThroughput = 1000,
        int breakDurationSeconds = 30) => new()
    {
        MaxRetries = maxRetries,
        RetryBaseDelaySeconds = 0,
        AttemptTimeoutSeconds = 30,
        TotalTimeoutSeconds = 120,
        CircuitBreakerFailureRatio = 0.5,
        CircuitBreakerSamplingDurationSeconds = 30,
        CircuitBreakerMinimumThroughput = minimumThroughput,
        CircuitBreakerBreakDurationSeconds = breakDurationSeconds
    };

    private static HttpClient BuildClient(string clientName, ClientResilienceOptions options, StubHandler handler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddResilientHttpClient(clientName, options);
        services.AddHttpClient(clientName).ConfigurePrimaryHttpMessageHandler(() => handler);

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);
    }

    [Theory]
    [MemberData(nameof(ClientNames))]
    public async Task Pipeline_RetriesTransientFailures_ThenSucceeds(string clientName)
    {
        // High MinimumThroughput keeps the circuit breaker out of the way for this retry test.
        var handler = new StubHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        var client = BuildClient(clientName, Options(maxRetries: 3), handler);

        var response = await client.GetAsync("https://localhost/test");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.CallCount.Should().Be(3);
    }

    [Theory]
    [MemberData(nameof(ClientNames))]
    public async Task Pipeline_WhenFailuresPersist_StopsAfterMaxRetries(string clientName)
    {
        var handler = new StubHandler { DefaultStatus = HttpStatusCode.ServiceUnavailable };
        var client = BuildClient(clientName, Options(maxRetries: 2), handler);

        var response = await client.GetAsync("https://localhost/test");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        handler.CallCount.Should().Be(3);
    }

    [Theory]
    [MemberData(nameof(ClientNames))]
    public async Task Pipeline_RetriesRateLimited_HonouringRetryAfter(string clientName)
    {
        // Retry-After of 1s with a zero base delay: without honouring the header the gap between
        // attempts would be ~0; honouring it forces a ~1s wait. Asserting a >=700ms gap proves the
        // header (not the base delay) drove the retry delay, with margin to stay stable on CI.
        var handler = new StubHandler(HttpStatusCode.TooManyRequests, HttpStatusCode.OK)
        {
            RetryAfter = TimeSpan.FromSeconds(1)
        };
        var client = BuildClient(clientName, Options(maxRetries: 2), handler);

        var response = await client.GetAsync("https://localhost/test");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.CallCount.Should().Be(2);
        handler.DelayBetweenFirstTwoCalls.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(700));
    }

    [Theory]
    [MemberData(nameof(ClientNames))]
    public async Task CircuitBreaker_OpensAfterThreshold_ThenFailsFast(string clientName)
    {
        // No retries so each call is a single attempt; low throughput so two failures can open it.
        var handler = new StubHandler { DefaultStatus = HttpStatusCode.ServiceUnavailable };
        var client = BuildClient(clientName, Options(maxRetries: 0, minimumThroughput: 2), handler);

        await OpenCircuit(client);
        var callsWhenOpen = handler.CallCount;

        // Fail-fast: while open, the call short-circuits without reaching the handler.
        await Assert.ThrowsAsync<BrokenCircuitException>(() => client.GetAsync("https://localhost/test"));
        handler.CallCount.Should().Be(callsWhenOpen);
    }

    [Theory]
    [MemberData(nameof(ClientNames))]
    public async Task CircuitBreaker_RecoversAfterBreakDuration(string clientName)
    {
        var handler = new StubHandler { DefaultStatus = HttpStatusCode.ServiceUnavailable };
        var client = BuildClient(clientName, Options(maxRetries: 0, minimumThroughput: 2, breakDurationSeconds: 1), handler);

        await OpenCircuit(client);

        // Once the break duration elapses the circuit goes half-open; a healthy response closes it.
        await Task.Delay(TimeSpan.FromMilliseconds(1500));
        handler.DefaultStatus = HttpStatusCode.OK;

        var response = await client.GetAsync("https://localhost/test");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task OpenCircuit(HttpClient client)
    {
        for (var i = 0; i < 10; i++)
        {
            try
            {
                await client.GetAsync("https://localhost/test");
            }
            catch (BrokenCircuitException)
            {
                return;
            }
        }

        throw new InvalidOperationException("Circuit breaker did not open within the expected number of attempts.");
    }

    private sealed class StubHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statuses = new(statuses);
        private long _firstCallTimestamp;
        private long _secondCallTimestamp;

        public HttpStatusCode DefaultStatus { get; set; } = HttpStatusCode.ServiceUnavailable;

        public TimeSpan? RetryAfter { get; set; }

        public int CallCount { get; private set; }

        /// <summary>Elapsed time between the first and second handler invocations.</summary>
        public TimeSpan DelayBetweenFirstTwoCalls =>
            Stopwatch.GetElapsedTime(_firstCallTimestamp, _secondCallTimestamp);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            if (CallCount == 1)
            {
                _firstCallTimestamp = Stopwatch.GetTimestamp();
            }
            else if (CallCount == 2)
            {
                _secondCallTimestamp = Stopwatch.GetTimestamp();
            }

            var status = _statuses.Count > 0 ? _statuses.Dequeue() : DefaultStatus;
            var response = new HttpResponseMessage(status);
            if (RetryAfter is { } retryAfter && status == HttpStatusCode.TooManyRequests)
            {
                response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAfter);
            }

            return Task.FromResult(response);
        }
    }
}
