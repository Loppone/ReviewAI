# ReviewAI

A .NET 10 application that leverages AI to perform automated code reviews on GitHub pull requests.

## Description

ReviewAI integrates with the GitHub API (via Octokit) and Anthropic's Claude API to provide intelligent, automated code review feedback directly on pull requests.

## Projects

| Project | Type | Description |
|---|---|---|
| `ReviewAI.Api` | ASP.NET Core Web API | HTTP entry point, exposes review endpoints |
| `ReviewAI.Core` | Class Library | Domain logic, GitHub and AI integrations |
| `ReviewAI.Tests` | xUnit Test Project | Unit and integration tests |

## Getting Started

```bash
dotnet restore
dotnet build
dotnet run --project src/ReviewAI.Api
```

API documentation available at `/scalar/v1` when running in Development mode.

## Tech Stack

- .NET 10
- MediatR (CQRS / mediator pattern)
- Octokit (GitHub API client)
- Anthropic.SDK (Claude AI client)
- Scalar (API documentation UI)
- xUnit + FluentAssertions + NSubstitute (testing)
