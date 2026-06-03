using System.Text.Json;
using Gatekeeper.Application.Sessions;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Infrastructure.Persistence;
using Gatekeeper.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatekeeper.Infrastructure.SessionActions.Ssh;

public sealed class DbSshActionPolicy : ISshActionPolicy
{
    private readonly GatekeeperDbContext _dbContext;

    public DbSshActionPolicy(GatekeeperDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public SshActionPolicyResult Resolve(
        string targetAlias,
        string actionName,
        IReadOnlyCollection<SshApprovedProfileGrant> approvedProfileGrants,
        JsonElement? parameters
    )
    {
        ArgumentNullException.ThrowIfNull(approvedProfileGrants);

        SshTargetEntity? target = _dbContext
            .SshTargets.AsNoTracking()
            .SingleOrDefault(candidate => candidate.Alias == targetAlias);
        if (target is null)
        {
            return SshActionPolicyResult.Failed(
                SshActionPolicyFailureReason.UnknownTarget,
                "Unknown SSH target."
            );
        }

        SshActionEntity? action = _dbContext
            .SshActions.AsNoTracking()
            .Include(candidate => candidate.Target)
            .Include(candidate => candidate.AllowedParameters)
                .ThenInclude(parameter => parameter.AllowedValues)
            .Include(candidate => candidate.ProfileActions)
                .ThenInclude(profileAction => profileAction.Profile)
            .SingleOrDefault(candidate =>
                candidate.TargetId == target.Id && candidate.Name == actionName
            );
        if (action is null)
        {
            return SshActionPolicyResult.Failed(
                SshActionPolicyFailureReason.UnknownAction,
                "Unknown SSH action."
            );
        }

        if (!HasApprovedProfileForAction(targetAlias, action, approvedProfileGrants))
        {
            return SshActionPolicyResult.Failed(
                SshActionPolicyFailureReason.MissingProfileMembership,
                "No approved SSH profile permits the requested action."
            );
        }

        SshParameterResolution parameterResolution = ResolveParameters(action, parameters);
        if (!parameterResolution.Succeeded)
        {
            return SshActionPolicyResult.Failed(
                SshActionPolicyFailureReason.InvalidParameter,
                parameterResolution.Error!
            );
        }

        SshActionMetadataValidation metadataValidation = ValidateActionMetadata(action);
        if (!metadataValidation.Succeeded)
        {
            return SshActionPolicyResult.Failed(
                SshActionPolicyFailureReason.InvalidConfiguration,
                metadataValidation.Error!
            );
        }

        SshCommandResolution commandResolution = ResolveCommand(
            action,
            parameterResolution.SafeParameters
        );
        if (!commandResolution.Succeeded)
        {
            return SshActionPolicyResult.Failed(
                SshActionPolicyFailureReason.InvalidConfiguration,
                commandResolution.Error!
            );
        }

        var resolvedAction = new SshResolvedAction(
            targetAlias,
            actionName,
            commandResolution.Command,
            parameterResolution.SafeParameters,
            TimeSpan.FromSeconds(ResolveTimeoutSeconds(action, target)),
            ResolveOutputLimitBytes(action, target),
            metadataValidation.IsMutating,
            metadataValidation.Risk,
            target.Host,
            target.Port,
            target.Username,
            target.PrivateKeyPath,
            target.KnownHostsPath
        );

        return SshActionPolicyResult.Success(resolvedAction);
    }

    private static bool HasApprovedProfileForAction(
        string targetAlias,
        SshActionEntity action,
        IReadOnlyCollection<SshApprovedProfileGrant> approvedProfileGrants
    )
    {
        HashSet<string> grantedProfiles = approvedProfileGrants
            .Where(grant => string.Equals(grant.TargetAlias, targetAlias, StringComparison.Ordinal))
            .Select(grant => grant.ProfileName)
            .ToHashSet(StringComparer.Ordinal);

        return action.ProfileActions.Any(profileAction =>
            grantedProfiles.Contains(profileAction.Profile.Name)
        );
    }

    private static SshActionMetadataValidation ValidateActionMetadata(SshActionEntity action)
    {
        if (action.IsMutating)
        {
            return action.Risk == RiskLevel.Low
                ? SshActionMetadataValidation.Failed(
                    "Mutating SSH actions must not use low risk metadata."
                )
                : SshActionMetadataValidation.Success(true, action.Risk);
        }

        return action.Risk == RiskLevel.Low
            ? SshActionMetadataValidation.Success(false, RiskLevel.Low)
            : SshActionMetadataValidation.Failed(
                "Read-only SSH actions must use low risk metadata."
            );
    }

    private static SshParameterResolution ResolveParameters(
        SshActionEntity action,
        JsonElement? parameters
    )
    {
        Dictionary<string, IReadOnlySet<string>> allowedParameters =
            action.AllowedParameters.ToDictionary(
                parameter => parameter.Name,
                parameter =>
                    (IReadOnlySet<string>)
                        parameter
                            .AllowedValues.Select(value => value.Value)
                            .ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal
            );

        if (parameters is null)
        {
            return allowedParameters.Count == 0
                ? SshParameterResolution.Success(
                    new Dictionary<string, string>(StringComparer.Ordinal)
                )
                : SshParameterResolution.Failed("SSH action parameters are required.");
        }

        if (parameters.Value.ValueKind != JsonValueKind.Object)
        {
            return SshParameterResolution.Failed("SSH action parameters must be an object.");
        }

        var safeParameters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonProperty parameter in parameters.Value.EnumerateObject())
        {
            if (
                !allowedParameters.TryGetValue(
                    parameter.Name,
                    out IReadOnlySet<string>? allowedValues
                )
            )
            {
                return SshParameterResolution.Failed("SSH action parameter is not supported.");
            }

            if (parameter.Value.ValueKind != JsonValueKind.String)
            {
                return SshParameterResolution.Failed("SSH action parameter must be a string.");
            }

            string? value = parameter.Value.GetString();
            if (value is null || !allowedValues.Contains(value))
            {
                return SshParameterResolution.Failed("SSH action parameter value is not allowed.");
            }

            if (safeParameters.ContainsKey(parameter.Name))
            {
                return SshParameterResolution.Failed(
                    "SSH action parameter must not be specified more than once."
                );
            }

            safeParameters.Add(parameter.Name, value);
        }

        foreach (string requiredParameter in allowedParameters.Keys)
        {
            if (!safeParameters.ContainsKey(requiredParameter))
            {
                return SshParameterResolution.Failed("SSH action parameter is required.");
            }
        }

        return SshParameterResolution.Success(safeParameters);
    }

    private static SshCommandResolution ResolveCommand(
        SshActionEntity action,
        IReadOnlyDictionary<string, string> safeParameters
    )
    {
        IReadOnlyList<string> configuredTemplate =
            JsonSerializer.Deserialize<string[]>(action.CommandTemplateJson) ?? [];
        IReadOnlyList<string> configuredCommand =
            JsonSerializer.Deserialize<string[]>(action.CommandJson) ?? [];

        bool usesTemplate = configuredTemplate.Count > 0;
        IReadOnlyList<string> sourceCommand = usesTemplate ? configuredTemplate : configuredCommand;
        if (sourceCommand.Count == 0)
        {
            return SshCommandResolution.Failed("SSH action command is not configured.");
        }

        var resolvedCommand = new List<string>(sourceCommand.Count);
        foreach (string argument in sourceCommand)
        {
            string resolvedArgument = argument;
            foreach (KeyValuePair<string, string> parameter in safeParameters)
            {
                resolvedArgument = resolvedArgument.Replace(
                    "{" + parameter.Key + "}",
                    parameter.Value,
                    StringComparison.Ordinal
                );
            }

            if (usesTemplate && ContainsTemplatePlaceholder(resolvedArgument))
            {
                return SshCommandResolution.Failed(
                    "SSH action command template contains an unresolved parameter."
                );
            }

            resolvedCommand.Add(resolvedArgument);
        }

        return SshCommandResolution.Success(resolvedCommand);
    }

    private static bool ContainsTemplatePlaceholder(string value)
    {
        int start = value.IndexOf('{', StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        int end = value.IndexOf('}', start + 1);
        return end > start + 1;
    }

    private static int ResolveTimeoutSeconds(SshActionEntity action, SshTargetEntity target)
    {
        if (action.TimeoutSeconds.HasValue && action.TimeoutSeconds.Value > 0)
        {
            return action.TimeoutSeconds.Value;
        }

        return target.DefaultTimeoutSeconds > 0 ? target.DefaultTimeoutSeconds : 10;
    }

    private static int ResolveOutputLimitBytes(SshActionEntity action, SshTargetEntity target)
    {
        if (action.OutputLimitBytes.HasValue && action.OutputLimitBytes.Value > 0)
        {
            return action.OutputLimitBytes.Value;
        }

        return target.DefaultOutputLimitBytes > 0 ? target.DefaultOutputLimitBytes : 8192;
    }

    private sealed class SshCommandResolution
    {
        private SshCommandResolution(bool succeeded, IReadOnlyList<string> command, string? error)
        {
            Succeeded = succeeded;
            Command = command;
            Error = error;
        }

        public bool Succeeded { get; }

        public IReadOnlyList<string> Command { get; }

        public string? Error { get; }

        public static SshCommandResolution Success(IReadOnlyList<string> command)
        {
            return new SshCommandResolution(true, command, null);
        }

        public static SshCommandResolution Failed(string error)
        {
            return new SshCommandResolution(false, Array.Empty<string>(), error);
        }
    }

    private sealed class SshActionMetadataValidation
    {
        private SshActionMetadataValidation(
            bool succeeded,
            bool isMutating,
            RiskLevel risk,
            string? error
        )
        {
            Succeeded = succeeded;
            IsMutating = isMutating;
            Risk = risk;
            Error = error;
        }

        public bool Succeeded { get; }

        public bool IsMutating { get; }

        public RiskLevel Risk { get; }

        public string? Error { get; }

        public static SshActionMetadataValidation Success(bool isMutating, RiskLevel risk)
        {
            return new SshActionMetadataValidation(true, isMutating, risk, null);
        }

        public static SshActionMetadataValidation Failed(string error)
        {
            return new SshActionMetadataValidation(false, false, RiskLevel.Low, error);
        }
    }

    private sealed class SshParameterResolution
    {
        private SshParameterResolution(
            bool succeeded,
            IReadOnlyDictionary<string, string> safeParameters,
            string? error
        )
        {
            Succeeded = succeeded;
            SafeParameters = safeParameters;
            Error = error;
        }

        public bool Succeeded { get; }

        public IReadOnlyDictionary<string, string> SafeParameters { get; }

        public string? Error { get; }

        public static SshParameterResolution Success(
            IReadOnlyDictionary<string, string> safeParameters
        )
        {
            return new SshParameterResolution(true, safeParameters, null);
        }

        public static SshParameterResolution Failed(string error)
        {
            return new SshParameterResolution(
                false,
                new Dictionary<string, string>(StringComparer.Ordinal),
                error
            );
        }
    }
}
