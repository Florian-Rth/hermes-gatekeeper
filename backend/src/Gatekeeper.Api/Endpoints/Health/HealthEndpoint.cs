using FastEndpoints;

namespace Gatekeeper.Api.Endpoints.Health;

public sealed class HealthEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/health");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HealthResponse response = new HealthResponse { Status = "ok" };

        await Send.OkAsync(response, ct);
    }
}
