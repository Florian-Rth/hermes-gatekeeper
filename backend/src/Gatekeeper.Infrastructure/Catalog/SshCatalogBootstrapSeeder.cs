using System.Text.Json;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Infrastructure.Persistence;
using Gatekeeper.Infrastructure.Persistence.Entities;
using Gatekeeper.Infrastructure.SessionActions.Ssh;
using Microsoft.EntityFrameworkCore;

namespace Gatekeeper.Infrastructure.Catalog;

public sealed class SshCatalogBootstrapSeeder
{
    private static readonly JsonSerializerOptions SerializerOptions = new();
    private readonly GatekeeperDbContext _dbContext;
    private readonly SshConnectorOptions _options;

    public SshCatalogBootstrapSeeder(GatekeeperDbContext dbContext, SshConnectorOptions options)
    {
        _dbContext = dbContext;
        _options = options;
    }

    public async Task SeedIfEmptyAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.SshTargets.AnyAsync(cancellationToken))
        {
            return;
        }

        if (_options.Targets.Count == 0)
        {
            return;
        }

        foreach ((string alias, SshTargetOptions target) in _options.Targets)
        {
            ValidateTarget(alias, target);
            _dbContext.SshTargets.Add(CreateTargetEntity(alias, target));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static SshTargetEntity CreateTargetEntity(string alias, SshTargetOptions target)
    {
        SshTargetEntity targetEntity = new()
        {
            Id = Guid.NewGuid(),
            Alias = alias,
            Host = target.Host,
            Port = target.Port,
            Username = target.Username,
            PrivateKeyPath = target.PrivateKeyPath,
            KnownHostsPath = target.KnownHostsPath,
            DefaultTimeoutSeconds = target.DefaultTimeoutSeconds,
            DefaultOutputLimitBytes = target.DefaultOutputLimitBytes,
        };

        Dictionary<string, SshActionEntity> actionEntities = new(StringComparer.Ordinal);
        foreach ((string actionName, SshActionOptions action) in target.Actions)
        {
            ValidateAction(alias, actionName, action);
            SshActionEntity actionEntity = new()
            {
                Id = Guid.NewGuid(),
                Target = targetEntity,
                Name = actionName,
                CommandJson = JsonSerializer.Serialize(action.Command, SerializerOptions),
                CommandTemplateJson = JsonSerializer.Serialize(
                    action.CommandTemplate,
                    SerializerOptions
                ),
                IsMutating = action.IsMutating!.Value,
                Risk = action.Risk!.Value,
                TimeoutSeconds = action.TimeoutSeconds,
                OutputLimitBytes = action.OutputLimitBytes,
            };

            foreach ((string parameterName, List<string> allowedValues) in action.AllowedParameters)
            {
                ValidateAllowedParameter(alias, actionName, parameterName, allowedValues);
                SshActionAllowedParameterEntity parameterEntity = new()
                {
                    Id = Guid.NewGuid(),
                    Action = actionEntity,
                    Name = parameterName,
                };

                foreach (string allowedValue in allowedValues)
                {
                    parameterEntity.AllowedValues.Add(
                        new SshActionAllowedParameterValueEntity
                        {
                            Id = Guid.NewGuid(),
                            AllowedParameter = parameterEntity,
                            Value = allowedValue,
                        }
                    );
                }

                actionEntity.AllowedParameters.Add(parameterEntity);
            }

            targetEntity.Actions.Add(actionEntity);
            actionEntities[actionName] = actionEntity;
        }

        foreach ((string profileName, SshProfileOptions profile) in target.Profiles)
        {
            ValidateProfile(alias, profileName, profile, actionEntities);
            SshProfileEntity profileEntity = new()
            {
                Id = Guid.NewGuid(),
                Target = targetEntity,
                Name = profileName,
            };

            foreach (string actionName in profile.Actions)
            {
                profileEntity.ProfileActions.Add(
                    new SshProfileActionEntity
                    {
                        Profile = profileEntity,
                        Action = actionEntities[actionName],
                    }
                );
            }

            targetEntity.Profiles.Add(profileEntity);
        }

        return targetEntity;
    }

    private static void ValidateTarget(string alias, SshTargetOptions target)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new InvalidOperationException("SSH catalog seed target alias must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(target.Host))
        {
            throw new InvalidOperationException(
                $"SSH catalog seed target '{alias}' must configure a host."
            );
        }

        if (string.IsNullOrWhiteSpace(target.Username))
        {
            throw new InvalidOperationException(
                $"SSH catalog seed target '{alias}' must configure a username."
            );
        }

        if (target.Actions.Count == 0)
        {
            throw new InvalidOperationException(
                $"SSH catalog seed target '{alias}' must define at least one action."
            );
        }
    }

    private static void ValidateProfile(
        string targetAlias,
        string profileName,
        SshProfileOptions profile,
        IReadOnlyDictionary<string, SshActionEntity> actionEntities
    )
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new InvalidOperationException(
                $"SSH catalog seed target '{targetAlias}' contains an empty profile name."
            );
        }

        if (profile.Actions.Count == 0)
        {
            throw new InvalidOperationException(
                $"SSH catalog seed profile '{targetAlias}/{profileName}' must define at least one action."
            );
        }

        foreach (string actionName in profile.Actions)
        {
            if (!actionEntities.ContainsKey(actionName))
            {
                throw new InvalidOperationException(
                    $"SSH catalog seed profile '{targetAlias}/{profileName}' references unknown action '{actionName}'."
                );
            }
        }
    }

    private static void ValidateAction(
        string targetAlias,
        string actionName,
        SshActionOptions action
    )
    {
        if (string.IsNullOrWhiteSpace(actionName))
        {
            throw new InvalidOperationException(
                $"SSH catalog seed target '{targetAlias}' contains an empty action name."
            );
        }

        if (action.Command.Count == 0 && action.CommandTemplate.Count == 0)
        {
            throw new InvalidOperationException(
                $"SSH catalog seed action '{targetAlias}/{actionName}' must define a command or command template."
            );
        }

        if (!action.IsMutating.HasValue)
        {
            throw new InvalidOperationException(
                $"SSH catalog seed action '{targetAlias}/{actionName}' must configure mutating metadata explicitly."
            );
        }

        if (!action.Risk.HasValue)
        {
            throw new InvalidOperationException(
                $"SSH catalog seed action '{targetAlias}/{actionName}' must configure risk metadata explicitly."
            );
        }

        if (action.IsMutating.Value && action.Risk.Value == RiskLevel.Low)
        {
            throw new InvalidOperationException(
                $"SSH catalog seed action '{targetAlias}/{actionName}' is invalid: mutating actions must not use low risk metadata."
            );
        }

        if (!action.IsMutating.Value && action.Risk.Value != RiskLevel.Low)
        {
            throw new InvalidOperationException(
                $"SSH catalog seed action '{targetAlias}/{actionName}' is invalid: read-only actions must use low risk metadata."
            );
        }
    }

    private static void ValidateAllowedParameter(
        string targetAlias,
        string actionName,
        string parameterName,
        IReadOnlyCollection<string> allowedValues
    )
    {
        if (string.IsNullOrWhiteSpace(parameterName))
        {
            throw new InvalidOperationException(
                $"SSH catalog seed action '{targetAlias}/{actionName}' contains an empty parameter name."
            );
        }

        if (allowedValues.Count == 0)
        {
            throw new InvalidOperationException(
                $"SSH catalog seed action '{targetAlias}/{actionName}' parameter '{parameterName}' must define at least one allowed value."
            );
        }

        if (allowedValues.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                $"SSH catalog seed action '{targetAlias}/{actionName}' parameter '{parameterName}' contains an empty allowed value."
            );
        }
    }
}
