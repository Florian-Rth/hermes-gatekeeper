using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gatekeeper.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gatekeeper.Tests;

public sealed class AdminAuthenticationEndpointTests
{
    [Fact]
    public async Task LoginWithCorrectCredentialsSetsHttpOnlyCookieAndMeReturnsAdmin()
    {
        await using AdminAuthApiFactory factory = new AdminAuthApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );

        using HttpResponseMessage loginResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/login",
            new { username = "admin", password = "correct-password" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Contains(
            loginResponse.Headers.GetValues("Set-Cookie"),
            value =>
                value.Contains("gatekeeper_admin", StringComparison.Ordinal)
                && value.Contains("httponly", StringComparison.OrdinalIgnoreCase)
                && value.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase)
        );
        string loginBody = await loginResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken
        );
        Assert.DoesNotContain("correct-password", loginBody, StringComparison.Ordinal);

        using HttpResponseMessage meResponse = await client.GetAsync(
            "/api/v1/admin/me",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        using JsonDocument meDocument = JsonDocument.Parse(
            await meResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
        Assert.True(meDocument.RootElement.GetProperty("authenticated").GetBoolean());
        Assert.Equal("admin", meDocument.RootElement.GetProperty("username").GetString());
    }

    [Fact]
    public async Task LoginWithWrongPasswordReturnsUnauthorizedAndDoesNotAuthenticate()
    {
        await using AdminAuthApiFactory factory = new AdminAuthApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/admin/login",
            new { username = "admin", password = "wrong" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LogoutInvalidatesCookieAndWritesAuditEvents()
    {
        await using AdminAuthApiFactory factory = new AdminAuthApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true }
        );
        using HttpResponseMessage loginResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/login",
            new { username = "admin", password = "correct-password" },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        client.DefaultRequestHeaders.Add("Origin", "http://localhost");

        using HttpResponseMessage logoutResponse = await client.PostAsync(
            "/api/v1/admin/logout",
            null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);
        using HttpResponseMessage meResponse = await client.GetAsync(
            "/api/v1/admin/me",
            TestContext.Current.CancellationToken
        );
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        GatekeeperDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
        string[] eventTypes = await dbContext
            .AuditEvents.Select(auditEvent => auditEvent.EventType)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Contains("AdminLoginSucceeded", eventTypes);
        Assert.Contains("AdminLogout", eventTypes);
    }

    [Fact]
    public async Task RepeatedFailedLoginsAreRateLimitedAndAuditedWithoutPassword()
    {
        await using AdminAuthApiFactory factory = new AdminAuthApiFactory();
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage? lastResponse = null;
        for (int attempt = 0; attempt < 6; attempt++)
        {
            lastResponse?.Dispose();
            lastResponse = await client.PostAsJsonAsync(
                "/api/v1/admin/login",
                new { username = "admin", password = "wrong-password" },
                TestContext.Current.CancellationToken
            );
        }

        using (lastResponse)
        {
            Assert.NotNull(lastResponse);
            Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse.StatusCode);
        }

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        GatekeeperDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
        string payload = await dbContext
            .AuditEvents.Where(auditEvent => auditEvent.EventType == "AdminLoginFailed")
            .Select(auditEvent => auditEvent.PayloadJson)
            .FirstAsync(TestContext.Current.CancellationToken);
        Assert.Contains("admin", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("wrong-password", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("cookie", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingAdminCredentialsFailClosed()
    {
        await using AdminAuthApiFactory factory = new AdminAuthApiFactory(
            username: null,
            password: null
        );
        await factory.MigrateAsync(TestContext.Current.CancellationToken);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/admin/login",
            new { username = "admin", password = "correct-password" },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private sealed class AdminAuthApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databasePath;

        public AdminAuthApiFactory(
            string? username = "admin",
            string? password = "correct-password"
        )
        {
            _databasePath = Path.Combine(Path.GetTempPath(), $"gatekeeper-{Guid.NewGuid():N}.db");
            Environment.SetEnvironmentVariable("GATEKEEPER_SQLITE_DATA_PATH", _databasePath);
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_USERNAME", username);
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_PASSWORD", password);
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_COOKIE_SECURE", "false");
        }

        public async Task MigrateAsync(CancellationToken cancellationToken)
        {
            await using AsyncServiceScope scope = Services.CreateAsyncScope();
            GatekeeperDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Environment.SetEnvironmentVariable("GATEKEEPER_SQLITE_DATA_PATH", null);
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_USERNAME", null);
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_PASSWORD", null);
            Environment.SetEnvironmentVariable("GATEKEEPER_ADMIN_COOKIE_SECURE", null);
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
    }
}
