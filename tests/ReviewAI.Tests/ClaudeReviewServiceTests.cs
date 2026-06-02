using System.Net;
using System.Text;
using Anthropic.SDK;
using FluentAssertions;
using ReviewAI.Core.Common.Errors;
using ReviewAI.Core.Services;

namespace ReviewAI.Tests;

public class ClaudeReviewServiceTests
{
    [Fact]
    public async Task ReviewDiffAsync_WithEmptyDiff_ReturnsSuccessfulNoDiffResult()
    {
        var service = new ClaudeReviewService(CreateClient("ignored"));

        var result = await service.ReviewDiffAsync("   ", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Summary.Should().Be("No diff provided.");
    }

    [Fact]
    public async Task ReviewDiffAsync_WhenClaudeReturnsMalformedJson_ReturnsInvalidAiResponseError()
    {
        var service = new ClaudeReviewService(CreateClient("this is not valid json"));

        var result = await service.ReviewDiffAsync("diff --git a/Program.cs b/Program.cs", CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.HasError<InvalidAiResponseError>().Should().BeTrue();
    }

    private static AnthropicClient CreateClient(string assistantText)
    {
        var responseJson = $$"""
        {
          "id": "msg_test",
          "type": "message",
          "role": "assistant",
          "model": "claude-3.5",
          "content": [{ "type": "text", "text": {{System.Text.Json.JsonSerializer.Serialize(assistantText)}} }],
          "stop_reason": "end_turn",
          "stop_sequence": null,
          "usage": { "input_tokens": 1, "output_tokens": 1 }
        }
        """;

        var httpClient = new HttpClient(new StubHttpMessageHandler(responseJson));
        return new AnthropicClient(new APIAuthentication("test-key"), httpClient, null);
    }

    private sealed class StubHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        private readonly string _responseJson = responseJson;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
