using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Gatekeeper.Tests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Should_ReturnOk_When_HealthEndpointIsCalled()
    {
        await using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>();
        using HttpClient client = factory.CreateClient();
        using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(
            TimeSpan.FromSeconds(10)
        );

        using HttpResponseMessage response = await client.GetAsync(
            "/health",
            cancellationTokenSource.Token
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
