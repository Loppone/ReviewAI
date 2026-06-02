using System.Net;
using System.Text;
using System.Text.Json;
using Anthropic.SDK;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReviewAI.Core.Common.Errors;
using ReviewAI.Core.Configuration;
using ReviewAI.Core.Services;

namespace ReviewAI.Tests;

public class ClaudeReviewServiceTests
{
    private const string ValidReviewInput = """
    {
      "summary": "Looks solid overall.",
      "severityScore": 3.5,
      "securityIssues": ["Hardcoded secret in Program.cs"],
      "performanceIssues": ["N+1 query in loop"],
      "namingIssues": [],
      "patternIssues": ["Consider extracting a strategy"]
    }
    """;

    [Fact]
    public async Task ReviewDiffAsync_WithEmptyDiff_ReturnsSuccessfulNoDiffResult()
    {
        var service = new ClaudeReviewService(CreateClient(ToolUseResponse(ValidReviewInput)), CreateOptions(), NullLogger<ClaudeReviewService>.Instance);

        var result = await service.ReviewDiffAsync("   ", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Summary.Should().Be("No diff provided.");
    }

    [Fact]
    public async Task ReviewDiffAsync_WhenToolUseReturnsValidReview_ReturnsParsedResult()
    {
        var service = new ClaudeReviewService(CreateClient(ToolUseResponse(ValidReviewInput)), CreateOptions(), NullLogger<ClaudeReviewService>.Instance);

        var result = await service.ReviewDiffAsync("diff --git a/Program.cs b/Program.cs", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Summary.Should().Be("Looks solid overall.");
        result.Value.SeverityScore.Should().Be(3.5m);
        result.Value.SecurityIssues.Should().ContainSingle().Which.Should().Be("Hardcoded secret in Program.cs");
        result.Value.NamingIssues.Should().BeEmpty();
        result.Value.PatternIssues.Should().ContainSingle();
    }

    [Fact]
    public async Task ReviewDiffAsync_WhenResponseIsTextWithFenceAndPreamble_ParsesViaFallback()
    {
        var noisyText = "Sure! Here is the review you asked for:\n\n```json\n" + ValidReviewInput + "\n```\nHope it helps.";
        var service = new ClaudeReviewService(CreateClient(TextResponse(noisyText)), CreateOptions(), NullLogger<ClaudeReviewService>.Instance);

        var result = await service.ReviewDiffAsync("diff --git a/Program.cs b/Program.cs", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Summary.Should().Be("Looks solid overall.");
        result.Value.PerformanceIssues.Should().ContainSingle().Which.Should().Be("N+1 query in loop");
    }

    [Fact]
    public async Task ReviewDiffAsync_WhenResponseTruncated_ReturnsInvalidAiResponseError()
    {
        var service = new ClaudeReviewService(
            CreateClient(ToolUseResponse(ValidReviewInput, stopReason: "max_tokens")), CreateOptions(), NullLogger<ClaudeReviewService>.Instance);

        var result = await service.ReviewDiffAsync("diff --git a/Program.cs b/Program.cs", CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.HasError<InvalidAiResponseError>().Should().BeTrue();
    }

    [Fact]
    public async Task ReviewDiffAsync_WhenToolUseInputMissingRequiredFields_ReturnsInvalidAiResponseError()
    {
        var service = new ClaudeReviewService(
            CreateClient(ToolUseResponse("""{ "summary": "incomplete" }""")), CreateOptions(), NullLogger<ClaudeReviewService>.Instance);

        var result = await service.ReviewDiffAsync("diff --git a/Program.cs b/Program.cs", CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.HasError<InvalidAiResponseError>().Should().BeTrue();
    }

    [Fact]
    public async Task ReviewDiffAsync_WhenClaudeReturnsNonJsonText_ReturnsInvalidAiResponseError()
    {
        var service = new ClaudeReviewService(CreateClient(TextResponse("this is not valid json")), CreateOptions(), NullLogger<ClaudeReviewService>.Instance);

        var result = await service.ReviewDiffAsync("diff --git a/Program.cs b/Program.cs", CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.HasError<InvalidAiResponseError>().Should().BeTrue();
    }

    private static IOptions<AnthropicOptions> CreateOptions() =>
        Options.Create(new AnthropicOptions { Model = "claude-sonnet-4-5", MaxTokens = 2048, Temperature = 0.2m });

    private static string ToolUseResponse(string inputJson, string stopReason = "tool_use") => $$"""
        {
          "id": "msg_test",
          "type": "message",
          "role": "assistant",
          "model": "claude-sonnet-4-5",
          "content": [{ "type": "tool_use", "id": "toolu_test", "name": "submit_code_review", "input": {{inputJson}} }],
          "stop_reason": "{{stopReason}}",
          "stop_sequence": null,
          "usage": { "input_tokens": 1, "output_tokens": 1 }
        }
        """;

    private static string TextResponse(string assistantText, string stopReason = "end_turn") => $$"""
        {
          "id": "msg_test",
          "type": "message",
          "role": "assistant",
          "model": "claude-sonnet-4-5",
          "content": [{ "type": "text", "text": {{JsonSerializer.Serialize(assistantText)}} }],
          "stop_reason": "{{stopReason}}",
          "stop_sequence": null,
          "usage": { "input_tokens": 1, "output_tokens": 1 }
        }
        """;

    private static AnthropicClient CreateClient(string responseJson)
    {
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
