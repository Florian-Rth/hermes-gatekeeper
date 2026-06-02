using System.Text.Json;
using Gatekeeper.Application.Sessions;
using Gatekeeper.Core.AccessRequests;

namespace Gatekeeper.Infrastructure.SessionActions.Ssh;

public sealed class ConfiguredSshActionPolicy : ISshActionPolicy
{
    private readonly SshConnectorOptions _options;

    public ConfiguredSshActionPolicy(SshConnectorOptions options)
    {
        _options = options;
    }

    public SshActionPolicyResult Resolve(
        string targetAlias,
        string actionName,
        IReadOnlyCollection<SshApprovedProfileGrant> approvedProfileGrants,
        JsonElement? parameters
    )
    {
        ArgumentNullException.ThrowIfNull(approvedProfileGrants);

        if (!_options.Targets.TryGetValue(targetAlias, out SshTargetOptions? target))
        {
            return SshActionPolicyResult.Failed(
                SshActionPolicyFailureReason.UnknownTarget,
                "Unknown SSH target."
            );
        }

        if (!target.Actions.TryGetValue(actionName, out SshActionOptions? action))
        {
            return SshActionPolicyResult.Failed(
                SshActionPolicyFailureReason.UnknownAction,
                "Unknown SSH action."
            );
        }

        if (!HasApprovedProfileForAction(targetAlias, target, actionName, approvedProfileGrants))
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
            metadataValidation.Risk
        );

        return SshActionPolicyResult.Success(resolvedAction);
    }

    private static bool HasApprovedProfileForAction(
        string targetAlias,
        SshTargetOptions target,
        string actionName,
        IReadOnlyCollection<SshApprovedProfileGrant> approvedProfileGrants
    )
    {
        foreach (SshApprovedProfileGrant approvedProfileGrant in approvedProfileGrants)
        {
            if (
                !string.Equals(
                    approvedProfileGrant.TargetAlias,
                    targetAlias,
                    StringComparison.Ordinal
                )
            )
            {
                continue;
            }

            if (
                !target.Profiles.TryGetValue(
                    approvedProfileGrant.ProfileName,
                    out SshProfileOptions? profile
                )
            )
            {
                continue;
            }

            if (profile.Actions.Contains(actionName, StringComparer.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static SshActionMetadataValidation ValidateActionMetadata(SshActionOptions action)
    {
        if (!action.IsMutating.HasValue)
        {
            return SshActionMetadataValidation.Failed(
                "SSH action mutating metadata must be configured explicitly."
            );
        }

        if (!action.Risk.HasValue)
        {
            return SshActionMetadataValidation.Failed(
                "SSH action risk metadata must be configured explicitly."
            );
        }

        if (action.IsMutating.Value)
        {
            return action.Risk.Value == RiskLevel.Low
                ? SshActionMetadataValidation.Failed(
                    "Mutating SSH actions must not use low risk metadata."
                )
                : SshActionMetadataValidation.Success(true, action.Risk.Value);
        }

        return action.Risk.Value == RiskLevel.Low
            ? SshActionMetadataValidation.Success(false, RiskLevel.Low)
            : SshActionMetadataValidation.Failed(
                "Read-only SSH actions must use low risk metadata."
            );
    }

    private static SshParameterResolution ResolveParameters(
        SshActionOptions action,
        JsonElement? parameters
    )
    {
        if (parameters is null)
        {
            return action.AllowedParameters.Count == 0
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
                !action.AllowedParameters.TryGetValue(
                    parameter.Name,
                    out List<string>? allowedValues
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
            if (value is null || !allowedValues.Contains(value, StringComparer.Ordinal))
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

        foreach (string requiredParameter in action.AllowedParameters.Keys)
        {
            if (!safeParameters.ContainsKey(requiredParameter))
            {
                return SshParameterResolution.Failed("SSH action parameter is required.");
            }
        }

        return SshParameterResolution.Success(safeParameters);
    }

    private static SshCommandResolution ResolveCommand(
        SshActionOptions action,
        IReadOnlyDictionary<string, string> safeParameters
    )
    {
        bool usesTemplate = action.CommandTemplate.Count > 0;
        List<string> configuredCommand = usesTemplate ? action.CommandTemplate : action.Command;
        if (configuredCommand.Count == 0)
        {
            return SshCommandResolution.Failed("SSH action command is not configured.");
        }

        var resolvedCommand = new List<string>(configuredCommand.Count);

        foreach (string argument in configuredCommand)
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
        int openBraceIndex = value.IndexOf('{', StringComparison.Ordinal);
        if (openBraceIndex < 0)
        {
            return false;
        }

        int closeBraceIndex = value.IndexOf('}', openBraceIndex + 1);
        return closeBraceIndex > openBraceIndex + 1;
    }

    private static int ResolveTimeoutSeconds(SshActionOptions action, SshTargetOptions target)
    {
        return action.TimeoutSeconds is > 0
            ? action.TimeoutSeconds.Value
            : target.DefaultTimeoutSeconds;
    }

    private static int ResolveOutputLimitBytes(SshActionOptions action, SshTargetOptions target)
    {
        return action.OutputLimitBytes is > 0
            ? action.OutputLimitBytes.Value
            : target.DefaultOutputLimitBytes;
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
