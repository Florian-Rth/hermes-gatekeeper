using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Gatekeeper.Tests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Should_ReturnOk_When_HealthEndpointIsCalled()
    {
        string? previousDataPath = Environment.GetEnvironmentVariable(
            "GATEKEEPER_SQLITE_DATA_PATH"
        );
        string databasePath = Path.Combine(
            Path.GetTempPath(),
            $"gatekeeper-health-{Guid.NewGuid():N}.db"
        );
        Environment.SetEnvironmentVariable("GATEKEEPER_SQLITE_DATA_PATH", databasePath);

        try
        {
            await using WebApplicationFactory<Program> factory =
                new WebApplicationFactory<Program>();
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
        finally
        {
            Environment.SetEnvironmentVariable("GATEKEEPER_SQLITE_DATA_PATH", previousDataPath);
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }
}
