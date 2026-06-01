# ReviewAI — Claude Code Configuration

## Project Overview
ReviewAI is a stateless .NET 10 Web API that analyzes GitHub Pull Requests 
using Claude AI and returns a structured code review with scores and comments.

## Architecture
- Vertical Slice Architecture
- CQRS with MediatR
- No database — stateless by design

## Solution Structure
- ReviewAI.Api — Web API, endpoints, middleware, Program.cs
- ReviewAI.Core — Features, domain logic, AI and GitHub integration
- ReviewAI.Tests — Unit tests only

## Coding Standards
- Target framework: net10.0
- Language: C# 13
- Naming: PascalCase for classes, camelCase for variables
- Each feature in its own folder under Features/
- One class per file
- No static classes except extensions
- Prefer records for DTOs and commands
- Always use primary constructors where possible

## Architecture Rules
- Follow SOLID principles strictly
- Use design patterns where appropriate (Strategy, Factory, Decorator, etc.)
- No business logic in controllers
- Controllers only dispatch MediatR commands/queries
- All business logic lives in handlers inside ReviewAI.Core
- Keep handlers small and focused
- No cross-feature dependencies

## Testing Rules
- TDD approach — write tests before implementation
- xUnit for test framework
- FluentAssertions for assertions
- NSubstitute for mocking
- One test class per handler
- Test method naming: MethodName_Scenario_ExpectedResult

## Dependencies
- MediatR for CQRS
- Octokit.NET for GitHub API
- Anthropic.SDK for Claude AI
- Scalar for API documentation
- FluentAssertions + NSubstitute for testing

## What NOT to do
- No Swagger — use Scalar only
- No repository pattern — stateless, no DB
- No AutoMapper
- No static classes
- Do not add packages not listed above without asking first
- Do not create projects outside the defined solution structure

## Environment Variables
- ANTHROPIC_API_KEY — Claude API key
- GITHUB_TOKEN — GitHub personal access token (for reading PRs)
