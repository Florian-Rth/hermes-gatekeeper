using System.Text.Json;
using Gatekeeper.Application.AuditEvents;

namespace Gatekeeper.Tests;

public sealed class AuditDetailProjectorTests
{
    [Fact]
    public void Should_ProjectSameBoundedDetails_When_FallingBackToSerializedPayload()
    {
        Dictionary<string, object?> source = new(StringComparer.Ordinal)
        {
            ["targetAlias"] = "prod-api",
            ["action"] = "logs.tail",
            ["safeParameters"] = new Dictionary<string, string> { ["lines"] = "100" },
            ["exitStatus"] = 0,
            ["timedOut"] = false,
            ["output"] = new AuditOutputMetadata(13, 7),
            ["reasonCode"] = "none",
            ["agentId"] = "agent-blue",
            ["authMethod"] = "apiKey",
            ["rawOutput"] = "super-secret",
        };

        IReadOnlyDictionary<string, string> projected = AuditDetailProjector.Project(source);
        string payloadJson = JsonSerializer.Serialize(
            new
            {
                Details = new
                {
                    TargetAlias = "prod-api",
                    Action = "logs.tail",
                    SafeParameters = new { lines = "100" },
                    ExitStatus = 0,
                    TimedOut = false,
                    Output = new
                    {
                        StdoutBytes = 13,
                        StderrBytes = 7,
                        Stdout = "secret stdout",
                    },
                    ReasonCode = "none",
                    AgentId = "agent-blue",
                    AuthMethod = "apiKey",
                    RawOutput = "super-secret",
                },
            }
        );

        IReadOnlyDictionary<string, string> fallbackProjected =
            AuditDetailProjector.ProjectFromSerializedPayload(payloadJson);

        Assert.Equal(projected, fallbackProjected);
        Assert.False(fallbackProjected.ContainsKey("rawOutput"));
    }

    [Fact]
    public void Should_IgnoreMalformedSafeDetails_When_FallingBackToSerializedPayload()
    {
        string payloadJson = JsonSerializer.Serialize(
            new
            {
                Details = new
                {
                    TargetAlias = "prod-api",
                    Action = "logs.tail",
                    SafeParameters = "privateKey=super-secret",
                    Output = "raw stdout",
                    ExitStatus = 0,
                },
            }
        );

        IReadOnlyDictionary<string, string> projected =
            AuditDetailProjector.ProjectFromSerializedPayload(payloadJson);

        Assert.Equal("prod-api", projected["targetAlias"]);
        Assert.Equal("logs.tail", projected["action"]);
        Assert.Equal("0", projected["exitStatus"]);
        Assert.False(projected.ContainsKey("safeParameters"));
        Assert.False(projected.ContainsKey("output"));
    }
}
