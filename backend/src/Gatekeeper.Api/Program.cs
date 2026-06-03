using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints;
using FastEndpoints.Swagger;
using Gatekeeper.Api.AdminAuthentication;
using Gatekeeper.Api.AgentAuthentication;
using Gatekeeper.Application;
using Gatekeeper.Infrastructure;
using Gatekeeper.Infrastructure.Catalog;
using Gatekeeper.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

AdminAuthOptions adminAuthOptions = new AdminAuthOptions(builder.Configuration);
AgentAuthOptions agentAuthOptions = new AgentAuthOptions(builder.Configuration);
builder.Services.AddSingleton(adminAuthOptions);
builder.Services.AddSingleton(agentAuthOptions);
builder.Services.AddSingleton<AgentApiKeyVerifier>();
builder.Services.AddScoped<AgentApiKeyGuard>();
builder.Services.AddScoped<AgentAuthAuditWriter>();
builder.Services.AddSingleton<AdminCredentialVerifier>();
builder.Services.AddSingleton<AdminLoginRateLimiter>();
builder.Services.AddScoped<AdminAuthAuditWriter>();
builder.Services.AddScoped<AdminSessionGuard>();
builder
    .Services.AddAuthentication(AdminAuthConstants.Scheme)
    .AddCookie(
        AdminAuthConstants.Scheme,
        options =>
        {
            options.Cookie.Name = adminAuthOptions.CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = adminAuthOptions.CookieSecure
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.None;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(adminAuthOptions.SessionIdleMinutes);
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        }
    );
builder.Services.AddAuthorization();

builder
    .Services.AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddFastEndpoints()
    .SwaggerDocument();

WebApplication app = builder.Build();

await ApplyMigrationsAsync(app, app.Lifetime.ApplicationStopping);

app.UseAuthentication();
app.UseAuthorization();
app.UseFastEndpoints(config =>
{
    config.Serializer.Options.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
    );
});
app.UseSwaggerGen();

await app.RunAsync();

static async Task ApplyMigrationsAsync(WebApplication app, CancellationToken cancellationToken)
{
    await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
    GatekeeperDbContext dbContext = scope.ServiceProvider.GetRequiredService<GatekeeperDbContext>();
    await dbContext.Database.MigrateAsync(cancellationToken);

    SshCatalogBootstrapSeeder seeder =
        scope.ServiceProvider.GetRequiredService<SshCatalogBootstrapSeeder>();
    await seeder.SeedIfEmptyAsync(cancellationToken);
}

public sealed partial class Program { }
