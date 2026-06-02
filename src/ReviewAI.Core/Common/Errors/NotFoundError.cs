using FluentResults;

namespace ReviewAI.Core.Common.Errors;

/// <summary>
/// Represents a missing resource, such as a repository or pull request that could not be found.
/// </summary>
public sealed class NotFoundError(string message) : Error(message);
