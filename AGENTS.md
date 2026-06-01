# ReviewAI AI Agent Instructions

## What this repo is
ReviewAI is a stateless .NET 10 Web API project for automated GitHub pull request reviews using Claude AI.

## Key projects
- `src/ReviewAI.Api` — ASP.NET Core API entry point
- `src/ReviewAI.Core` — domain logic, GitHub and AI integrations
- `tests/ReviewAI.Tests` — unit tests

## Important conventions
- Keep controllers thin. Controllers should only dispatch MediatR commands/queries.
- Business logic belongs in handlers or services inside `ReviewAI.Core`.
- No repository pattern, no database persistence, no static classes except extensions.
- Prefer records for DTOs/commands and primary constructors when possible.

## Dependencies and technology
- .NET 10
- C# 13
- MediatR for CQRS/mediator pattern
- Octokit for GitHub API
- Anthropic.SDK for Claude AI
- Scalar for API documentation
- xUnit + FluentAssertions + NSubstitute for testing

## Build and run
- `dotnet restore`
- `dotnet build`
- `dotnet run --project src/ReviewAI.Api`
- API docs available at `/scalar/v1` in Development mode

## Testing
- Use `dotnet test` for the `tests/ReviewAI.Tests` project
- Keep one test class per handler or feature
- Name tests in the format: `MethodName_Scenario_ExpectedResult`

## Rules for changes
- Do not add packages not listed in the existing project files without asking first.
- Do not introduce Swagger; Scalar is the API documentation choice.
- Do not create new projects outside the existing solution structure.

## Environment variables
- `ANTHROPIC_API_KEY` — Claude API key
- `GITHUB_TOKEN` — GitHub personal access token for reading PRs

## Reference docs
- `README.md` for project overview and tech stack
- `CLAUDE.md` for coding standards and architecture rules
