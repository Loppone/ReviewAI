using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ReviewAI.Api.Authentication;
using ReviewAI.Api.Configuration;

namespace ReviewAI.Tests;

public class ApiKeyAuthenticationHandlerTests
{
    private const string HeaderName = "X-API-Key";
    private const string ValidKey = "0123456789abcdef0123456789abcdef"; // 32 chars
    private const string RotationKey = "fedcba9876543210fedcba9876543210"; // 32 chars

    private static async Task<(ApiKeyAuthenticationHandler Handler, HttpContext Context)> CreateHandlerAsync(
        ApiKeyAuthOptions apiKeyOptions,
        Action<HttpContext> configureRequest)
    {
        var optionsMonitor = Substitute.For<IOptionsMonitor<AuthenticationSchemeOptions>>();
        optionsMonitor.Get(Arg.Any<string>()).Returns(new AuthenticationSchemeOptions());

        var handler = new ApiKeyAuthenticationHandler(
            optionsMonitor,
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            Options.Create(apiKeyOptions));

        var context = new DefaultHttpContext();
        configureRequest(context);

        var scheme = new AuthenticationScheme(
            ApiKeyAuthenticationDefaults.AuthenticationScheme,
            displayName: null,
            handlerType: typeof(ApiKeyAuthenticationHandler));

        await handler.InitializeAsync(scheme, context);
        return (handler, context);
    }

    private static ApiKeyAuthOptions OptionsWith(params string[] keys) =>
        new() { HeaderName = HeaderName, ApiKeys = keys };

    [Fact]
    public async Task Authenticate_WithValidKey_Succeeds()
    {
        var (handler, _) = await CreateHandlerAsync(
            OptionsWith(ValidKey),
            ctx => ctx.Request.Headers[HeaderName] = ValidKey);

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        result.Principal!.Identity!.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task Authenticate_WithSecondKeyDuringRotation_Succeeds()
    {
        var (handler, _) = await CreateHandlerAsync(
            OptionsWith(RotationKey, ValidKey),
            ctx => ctx.Request.Headers[HeaderName] = ValidKey);

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Authenticate_WithMissingHeader_ReturnsNoResult()
    {
        var (handler, _) = await CreateHandlerAsync(
            OptionsWith(ValidKey),
            _ => { });

        var result = await handler.AuthenticateAsync();

        result.None.Should().BeTrue();
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Authenticate_WithEmptyHeaderValue_Fails()
    {
        var (handler, _) = await CreateHandlerAsync(
            OptionsWith(ValidKey),
            ctx => ctx.Request.Headers[HeaderName] = "   ");

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task Authenticate_WithUnknownKey_Fails()
    {
        var (handler, _) = await CreateHandlerAsync(
            OptionsWith(ValidKey),
            ctx => ctx.Request.Headers[HeaderName] = "wrong-key-value-not-matching-anything");

        var result = await handler.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
    }
}
