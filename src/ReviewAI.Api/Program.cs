using Anthropic.SDK;
using Octokit;
using ReviewAI.Core.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
    typeof(Program).Assembly,
    typeof(ReviewAI.Core.Features.ReviewPullRequest.ReviewPullRequestCommand).Assembly));

builder.Services.AddSingleton<IGitHubClient>(_ =>
{
    var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? string.Empty;
    var client = new GitHubClient(new ProductHeaderValue("ReviewAI"));
    if (!string.IsNullOrWhiteSpace(token))
    {
        client.Credentials = new Credentials(token);
    }

    return client;
});

builder.Services.AddSingleton<IGitHubDiffService, GitHubDiffService>();
builder.Services.AddSingleton<IClaudeReviewService, ClaudeReviewService>();

builder.Services.AddSingleton(_ =>
{
    var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? string.Empty;
    var authentication = new APIAuthentication(apiKey);
    return new AnthropicClient(authentication, new HttpClient(), null);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
