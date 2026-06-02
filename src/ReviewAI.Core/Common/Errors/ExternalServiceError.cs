using FluentResults;

namespace ReviewAI.Core.Common.Errors;

/// <summary>
/// Represents a failure from an external dependency: GitHub API, the Anthropic SDK,
/// network errors, or timeouts.
/// </summary>
public sealed class ExternalServiceError(string message) : Error(message);
