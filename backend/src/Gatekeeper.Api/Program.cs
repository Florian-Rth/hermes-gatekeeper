using FastEndpoints;
using FastEndpoints.Swagger;
using Gatekeeper.Application;
using Gatekeeper.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder
    .Services.AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddFastEndpoints()
    .SwaggerDocument();

WebApplication app = builder.Build();

app.UseFastEndpoints();
app.UseSwaggerGen();

app.Run();

public sealed partial class Program { }
