using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReviewAI.Api.Configuration;
using ReviewAI.Core.Services;

namespace ReviewAI.Tests.Infrastructure;

public sealed class ReviewAiWebApplicationFactory(
    IGitHubDiffService gitHubDiffService,
    IClaudeReviewService claudeReviewService) : WebApplicationFactory<Program>
{
    private readonly IGitHubDiffService _gitHubDiffService = gitHubDiffService;
    private readonly IClaudeReviewService _claudeReviewService = claudeReviewService;

    public string ApiKey { get; } = "integration-test-api-key-0000000001";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGitHubDiffService>();
            services.RemoveAll<IClaudeReviewService>();
            services.AddSingleton(_gitHubDiffService);
            services.AddSingleton(_claudeReviewService);
            services.PostConfigure<ApiKeyAuthOptions>(options => options.ApiKeys = [ApiKey]);
        });
    }
}
