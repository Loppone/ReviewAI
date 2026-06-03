using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using ReviewAI.Api.Configuration;

namespace ReviewAI.Tests;

public class ResolvePartitionKeyTests
{
    private static HttpContext ContextWith(ClaimsPrincipal user) =>
        new DefaultHttpContext { User = user };

    [Fact]
    public void ResolvePartitionKey_WithNameIdentifierClaim_ReturnsClaimValue()
    {
        const string fingerprint = "0123456789ABCDEF";
        var identity = new ClaimsIdentity("ApiKey");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, fingerprint));
        var context = ContextWith(new ClaimsPrincipal(identity));

        RateLimitingServiceCollectionExtensions.ResolvePartitionKey(context).Should().Be(fingerprint);
    }

    [Fact]
    public void ResolvePartitionKey_WithoutClaim_ReturnsAnonymousBucket()
    {
        var context = ContextWith(new ClaimsPrincipal(new ClaimsIdentity()));

        RateLimitingServiceCollectionExtensions.ResolvePartitionKey(context)
            .Should().Be(RateLimitingServiceCollectionExtensions.AnonymousPartitionKey);
    }
}
