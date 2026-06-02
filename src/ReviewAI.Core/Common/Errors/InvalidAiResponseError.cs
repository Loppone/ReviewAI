using FluentResults;

namespace ReviewAI.Core.Common.Errors;

/// <summary>
/// Represents a response from Claude that does not satisfy the expected JSON contract
/// (malformed, incomplete, or unparseable).
/// </summary>
public sealed class InvalidAiResponseError(string message) : Error(message);
