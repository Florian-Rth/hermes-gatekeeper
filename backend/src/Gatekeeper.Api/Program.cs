using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints;
using FastEndpoints.Swagger;
using Gatekeeper.Api.AdminTokens;
using Gatekeeper.Application;
using Gatekeeper.Infrastructure;
using Gatekeeper.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IAdminTokenValidator, AdminTokenValidator>();

builder
    .Services.AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddFastEndpoints()
    .SwaggerDocument();

WebApplication app = builder.Build();

await ApplyMigrationsAsync(app, app.Lifetime.ApplicationStopping);

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
}

public sealed partial class Program { }
