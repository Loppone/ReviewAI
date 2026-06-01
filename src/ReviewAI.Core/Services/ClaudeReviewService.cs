using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using ReviewAI.Core.Features.ReviewPullRequest;

namespace ReviewAI.Core.Services;

public sealed class ClaudeReviewService(AnthropicClient anthropicClient) : IClaudeReviewService
{
    private readonly AnthropicClient _anthropicClient = anthropicClient;

    public async Task<ReviewPullRequestResult> ReviewDiffAsync(string diff, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(diff))
        {
            return new ReviewPullRequestResult(
                Summary: "No diff provided.",
                SeverityScore: 0m,
                SecurityIssues: Array.Empty<string>(),
                PerformanceIssues: Array.Empty<string>(),
                NamingIssues: Array.Empty<string>(),
                PatternIssues: Array.Empty<string>());
        }

        var prompt = BuildPrompt(diff);

        var response = await _anthropicClient.Messages.GetClaudeMessageAsync(new MessageParameters
        {
            Model = "claude-3.5",
            Messages = new List<Message> { new Message(RoleType.User, prompt) },
            MaxTokens = 500,
            Temperature = 0.2m
        }, cancellationToken);

        var reviewText = response?.FirstMessage?.Text ?? string.Empty;
        return ParseReviewResponse(reviewText);
    }

    private static string BuildPrompt(string diff)
    {
        return $"You are a code review assistant. Analyze the following GitHub PR diff and provide a structured review. " +
               "Return only JSON with keys: securityIssues, performanceIssues, namingIssues, patternIssues, summary, severityScore. " +
               "Use short bullet points for each issue array. " +
               $"Here is the diff:\n\n{diff}";
    }

    private static ReviewPullRequestResult ParseReviewResponse(string reviewText)
    {
        // Fallback parser: look for JSON object inside the response.
        try
        {
            var document = System.Text.Json.JsonDocument.Parse(reviewText);
            var root = document.RootElement;
            return new ReviewPullRequestResult(
                Summary: root.GetProperty("summary").GetString() ?? "",
                SeverityScore: root.GetProperty("severityScore").GetDecimal(),
                SecurityIssues: ParseStringArray(root, "securityIssues"),
                PerformanceIssues: ParseStringArray(root, "performanceIssues"),
                NamingIssues: ParseStringArray(root, "namingIssues"),
                PatternIssues: ParseStringArray(root, "patternIssues"));
        }
        catch
        {
            return new ReviewPullRequestResult(
                Summary: reviewText,
                SeverityScore: 0m,
                SecurityIssues: Array.Empty<string>(),
                PerformanceIssues: Array.Empty<string>(),
                NamingIssues: Array.Empty<string>(),
                PatternIssues: Array.Empty<string>());
        }
    }

    private static IReadOnlyList<string> ParseStringArray(System.Text.Json.JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return property.EnumerateArray()
            .Where(item => item.ValueKind == System.Text.Json.JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }
}
