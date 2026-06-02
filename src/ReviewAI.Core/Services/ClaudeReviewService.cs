using System.Text.Json;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using FluentResults;
using ReviewAI.Core.Common.Errors;
using ReviewAI.Core.Features.ReviewPullRequest;

namespace ReviewAI.Core.Services;

public sealed class ClaudeReviewService(AnthropicClient anthropicClient) : IClaudeReviewService
{
    private readonly AnthropicClient _anthropicClient = anthropicClient;

    public async Task<Result<ReviewPullRequestResult>> ReviewDiffAsync(string diff, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(diff))
        {
            return Result.Ok(new ReviewPullRequestResult(
                Summary: "No diff provided.",
                SeverityScore: 0m,
                SecurityIssues: Array.Empty<string>(),
                PerformanceIssues: Array.Empty<string>(),
                NamingIssues: Array.Empty<string>(),
                PatternIssues: Array.Empty<string>()));
        }

        var prompt = BuildPrompt(diff);

        string reviewText;
        try
        {
            var response = await _anthropicClient.Messages.GetClaudeMessageAsync(new MessageParameters
            {
                Model = "claude-3.5",
                Messages = new List<Message> { new Message(RoleType.User, prompt) },
                MaxTokens = 500,
                Temperature = 0.2m
            }, cancellationToken);

            reviewText = response?.FirstMessage?.Text ?? string.Empty;
        }
        catch (HttpRequestException ex)
        {
            return Result.Fail(new ExternalServiceError($"Network failure while contacting Claude: {ex.Message}"));
        }
        catch (Exception ex)
        {
            return Result.Fail(new ExternalServiceError($"Claude request failed: {ex.Message}"));
        }

        return ParseReviewResponse(reviewText);
    }

    private static string BuildPrompt(string diff)
    {
        return $"You are a code review assistant. Analyze the following GitHub PR diff and provide a structured review. " +
               "Return only JSON with keys: securityIssues, performanceIssues, namingIssues, patternIssues, summary, severityScore. " +
               "Use short bullet points for each issue array. " +
               $"Here is the diff:\n\n{diff}";
    }

    private static Result<ReviewPullRequestResult> ParseReviewResponse(string reviewText)
    {
        try
        {
            using var document = JsonDocument.Parse(reviewText);
            var root = document.RootElement;
            return Result.Ok(new ReviewPullRequestResult(
                Summary: root.GetProperty("summary").GetString() ?? "",
                SeverityScore: root.GetProperty("severityScore").GetDecimal(),
                SecurityIssues: ParseStringArray(root, "securityIssues"),
                PerformanceIssues: ParseStringArray(root, "performanceIssues"),
                NamingIssues: ParseStringArray(root, "namingIssues"),
                PatternIssues: ParseStringArray(root, "patternIssues")));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return Result.Fail(new InvalidAiResponseError("Claude returned a response that is not valid JSON or is missing required fields."));
        }
    }

    private static IReadOnlyList<string> ParseStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }
}
