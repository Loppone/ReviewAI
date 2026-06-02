using Anthropic.SDK;
using Octokit;
using ReviewAI.Api.Configuration;
using ReviewAI.Api.Middleware;
using ReviewAI.Core.Configuration;
using ReviewAI.Core.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
    typeof(Program).Assembly,
    typeof(ReviewAI.Core.Features.ReviewPullRequest.ReviewPullRequestCommand).Assembly));

builder.Services.AddOptions<AnthropicOptions>()
    .Bind(builder.Configuration.GetSection(AnthropicOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ResilienceOptions>()
    .Bind(builder.Configuration.GetSection(ResilienceOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var resilienceOptions = builder.Configuration
    .GetSection(ResilienceOptions.SectionName)
    .Get<ResilienceOptions>() ?? new ResilienceOptions();
builder.Services.AddAnthropicResilientHttpClient(resilienceOptions);

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

builder.Services.AddSingleton(sp =>
{
    var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? string.Empty;
    var authentication = new APIAuthentication(apiKey);
    var httpClient = sp.GetRequiredService<IHttpClientFactory>()
        .CreateClient(AnthropicHttpClientExtensions.AnthropicClientName);
    return new AnthropicClient(authentication, httpClient, null);
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
