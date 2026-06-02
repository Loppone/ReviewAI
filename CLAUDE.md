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

## Primary Constructors (C# 12+)
- Always use primary constructors instead of classic constructors when the constructor
  only assigns parameters to fields/properties (e.g. dependency injection)
- If a primary-constructor parameter is used by the class behavior, immediately copy it
  into a `private readonly` field (e.g. `private readonly T _x = x;`) to guarantee
  immutability — do not reference mutable primary-constructor parameters directly inside
  methods
- Place the primary constructor parameter list before the base class / interface list
- Do not mutate primary-constructor parameters and do not duplicate assignments already
  performed via the primary constructor
- Reference: https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/instance-constructors#primary-constructors

## Result Pattern (FluentResults)
- Use FluentResults (`Result` / `Result<T>`) for expected application failures — do NOT
  throw exceptions for validation, not-found, external-dependency, or invalid-AI-response cases
- Handlers and services return `Result` or `Result<T>`; the handler orchestrates and
  short-circuits on the first failure (return it without calling downstream services)
- Use typed `Error` classes (in `ReviewAI.Core/Common/Errors/`), never string parsing to
  distinguish failures:
  - `ValidationError` — invalid user input (malformed PR URL, invalid PR number) → HTTP 400
  - `NotFoundError` — repository or pull request not found → HTTP 404
  - `ExternalServiceError` — GitHub API, Anthropic SDK, network, or timeout failure → HTTP 502
  - `InvalidAiResponseError` — Claude responded but broke the expected JSON contract → HTTP 502
- Translate `Result` into HTTP responses only at the API boundary (e.g. the
  `ToActionResult` extension in `ReviewAI.Api/Http/`); keep HTTP concerns out of Core
- In Vertical Slice features, keep the Result flow inside the slice and the HTTP mapping at
  the API boundary
- Reserve exceptions for truly unexpected, unrecoverable failures

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
- FluentResults for the Result pattern
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
