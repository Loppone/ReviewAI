namespace ReviewAI.Core.Configuration;

/// <summary>
/// The set of Anthropic model identifiers officially supported by ReviewAI.
/// This is the project's own contract — intentionally decoupled from the Anthropic.SDK
/// model constants so that an SDK upgrade can neither silently widen nor invalidate the
/// approved set. Configured <c>Anthropic:AllowedModels</c> values must be a subset of this set.
/// </summary>
/// <remarks>
/// Not a static class (project convention forbids them outside extensions); instantiation
/// is prevented via a private constructor. Add a model here only after it has been approved
/// for use by ReviewAI.
/// </remarks>
public sealed class KnownAnthropicModels
{
    public const string ClaudeSonnet45 = "claude-sonnet-4-5";
    public const string ClaudeSonnet46 = "claude-sonnet-4-6";
    public const string ClaudeOpus46 = "claude-opus-4-6";
    public const string ClaudeHaiku45 = "claude-haiku-4-5-20251001";

    /// <summary>
    /// All model identifiers supported by ReviewAI. Ordinal comparison: model IDs are
    /// case-sensitive.
    /// </summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        ClaudeSonnet45,
        ClaudeSonnet46,
        ClaudeOpus46,
        ClaudeHaiku45
    };

    private KnownAnthropicModels()
    {
    }
}
