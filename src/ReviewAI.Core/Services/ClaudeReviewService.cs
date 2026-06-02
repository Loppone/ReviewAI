using System.Text.Json;
using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Messaging;
using FluentResults;
using Microsoft.Extensions.Options;
using ReviewAI.Core.Common.Errors;
using ReviewAI.Core.Configuration;
using ReviewAI.Core.Features.ReviewPullRequest;
using Tool = Anthropic.SDK.Common.Tool;

namespace ReviewAI.Core.Services;

public sealed class ClaudeReviewService(AnthropicClient anthropicClient, IOptions<AnthropicOptions> options) : IClaudeReviewService
{
    private const string ToolName = "submit_code_review";

    /// <summary>
    /// JSON Schema for the review tool input. Mirrors <see cref="ReviewPullRequestResult"/>.
    /// Used with strict tool use so Claude is forced to emit a tool_use block whose input
    /// conforms exactly to this schema (no markdown fences, no textual preamble).
    /// </summary>
    private const string ToolInputSchema = """
    {
      "type": "object",
      "properties": {
        "summary": { "type": "string", "description": "Concise overall summary of the review." },
        "severityScore": { "type": "number", "description": "Overall severity score from 0 (no issues) to 10 (critical)." },
        "securityIssues": { "type": "array", "items": { "type": "string" }, "description": "Security issues as short bullet points." },
        "performanceIssues": { "type": "array", "items": { "type": "string" }, "description": "Performance issues as short bullet points." },
        "namingIssues": { "type": "array", "items": { "type": "string" }, "description": "Naming issues as short bullet points." },
        "patternIssues": { "type": "array", "items": { "type": "string" }, "description": "Design/pattern issues as short bullet points." }
      },
      "required": ["summary", "severityScore", "securityIssues", "performanceIssues", "namingIssues", "patternIssues"],
      "additionalProperties": false
    }
    """;

    private readonly AnthropicClient _anthropicClient = anthropicClient;
    private readonly AnthropicOptions _options = options.Value;

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

        MessageResponse response;
        try
        {
            response = await _anthropicClient.Messages.GetClaudeMessageAsync(new MessageParameters
            {
                Model = _options.Model,
                Messages = new List<Message> { new(RoleType.User, BuildPrompt(diff)) },
                MaxTokens = _options.MaxTokens,
                Temperature = _options.Temperature,
                Tools = new List<Tool> { BuildReviewTool() },
                ToolChoice = new ToolChoice { Type = ToolChoiceType.Tool, Name = ToolName }
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Honour cancellation semantics — this is not an external-service failure.
            throw;
        }
        catch (HttpRequestException ex)
        {
            return Result.Fail(new ExternalServiceError($"Network failure while contacting Claude: {ex.Message}"));
        }
        catch (Exception ex)
        {
            return Result.Fail(new ExternalServiceError($"Claude request failed: {ex.Message}"));
        }

        // A truncated response yields incomplete JSON regardless of the path below.
        if (string.Equals(response?.StopReason, "max_tokens", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail(new InvalidAiResponseError(
                "Claude response was truncated (max_tokens reached) before the review JSON was complete."));
        }

        return ExtractReviewJson(response) is { } reviewJson
            ? ParseReviewResponse(reviewJson)
            : Result.Fail(new InvalidAiResponseError("Claude returned no usable content to parse."));
    }

    private static string BuildPrompt(string diff)
    {
        return "You are a code review assistant. Analyze the following GitHub PR diff and provide a structured review. " +
               $"Call the {ToolName} tool with your findings. Use short bullet points for each issue array. " +
               $"Here is the diff:\n\n{diff}";
    }

    private static Tool BuildReviewTool()
    {
        var function = new Function(
            ToolName,
            "Submit the structured code review for the analyzed pull request diff.",
            ToolInputSchema)
        {
            Strict = true
        };

        return new Tool(function);
    }

    /// <summary>
    /// Returns the raw review JSON from the response. Primary path is the forced
    /// <c>tool_use</c> block; the fallback tolerantly extracts JSON from free text
    /// (stripping markdown fences and textual preambles) for the rare case Claude
    /// replies with plain text instead of the tool call. Returns <c>null</c> when no
    /// content is present at all.
    /// </summary>
    private static string? ExtractReviewJson(MessageResponse? response)
    {
        var toolUse = response?.Content?.OfType<ToolUseContent>()
            .FirstOrDefault(c => string.Equals(c.Name, ToolName, StringComparison.Ordinal));

        if (toolUse?.Input is { } input)
        {
            return input.ToJsonString();
        }

        var text = response?.FirstMessage?.Text;
        return string.IsNullOrWhiteSpace(text) ? null : ExtractJsonObject(text);
    }

    /// <summary>
    /// Tolerantly isolates the first JSON object in arbitrary text by slicing from the
    /// first '{' to the last '}'. This neutralises markdown code fences and textual
    /// preambles without a dedicated parser.
    /// </summary>
    private static string ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }

    private static Result<ReviewPullRequestResult> ParseReviewResponse(string reviewJson)
    {
        try
        {
            using var document = JsonDocument.Parse(reviewJson);
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
