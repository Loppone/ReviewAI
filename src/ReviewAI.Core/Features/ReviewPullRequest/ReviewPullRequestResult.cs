using System.Collections.Generic;

namespace ReviewAI.Core.Features.ReviewPullRequest;

public sealed record ReviewPullRequestResult(
    string Summary,
    decimal SeverityScore,
    IReadOnlyList<string> SecurityIssues,
    IReadOnlyList<string> PerformanceIssues,
    IReadOnlyList<string> NamingIssues,
    IReadOnlyList<string> PatternIssues
);
