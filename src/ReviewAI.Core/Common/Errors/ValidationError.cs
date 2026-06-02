using FluentResults;

namespace ReviewAI.Core.Common.Errors;

/// <summary>
/// Represents invalid user input, such as a malformed pull request URL or an invalid PR number.
/// </summary>
public sealed class ValidationError(string message) : Error(message);
