using Gatekeeper.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatekeeper.Infrastructure.Persistence;

public sealed class GatekeeperDbContext : DbContext
{
    public GatekeeperDbContext(DbContextOptions<GatekeeperDbContext> options)
        : base(options) { }

    public DbSet<AccessRequestEntity> AccessRequests => Set<AccessRequestEntity>();

    public DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();

    public DbSet<SessionEntity> Sessions => Set<SessionEntity>();

    public DbSet<SshTargetEntity> SshTargets => Set<SshTargetEntity>();

    public DbSet<SshProfileEntity> SshProfiles => Set<SshProfileEntity>();

    public DbSet<SshActionEntity> SshActions => Set<SshActionEntity>();

    public DbSet<SshProfileActionEntity> SshProfileActions => Set<SshProfileActionEntity>();

    public DbSet<SshActionAllowedParameterEntity> SshActionAllowedParameters =>
        Set<SshActionAllowedParameterEntity>();

    public DbSet<SshActionAllowedParameterValueEntity> SshActionAllowedParameterValues =>
        Set<SshActionAllowedParameterValueEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<AccessRequestEntity>(entity =>
        {
            entity.ToTable("AccessRequests");
            entity.HasKey(accessRequest => accessRequest.Id);
            entity.Property(accessRequest => accessRequest.Intent).HasMaxLength(500).IsRequired();
            entity
                .Property(accessRequest => accessRequest.Requester)
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(accessRequest => accessRequest.TargetsJson).IsRequired();
            entity.Property(accessRequest => accessRequest.RequestedCapabilitiesJson).IsRequired();
            entity.Property(accessRequest => accessRequest.Risk).IsRequired();
            entity
                .Property(accessRequest => accessRequest.Status)
                .IsRequired()
                .IsConcurrencyToken();
            entity.Property(accessRequest => accessRequest.ProposedActionsJson).IsRequired();
            entity.Property(accessRequest => accessRequest.ForbiddenActionsJson).IsRequired();
            entity.Property(accessRequest => accessRequest.MetadataJson).IsRequired();
            entity.Property(accessRequest => accessRequest.CreatedAt).IsRequired();
            entity.Property(accessRequest => accessRequest.UpdatedAt).IsRequired();
            entity.HasIndex(accessRequest => accessRequest.Status);
            entity.HasIndex(accessRequest => accessRequest.CreatedAt);
        });

        modelBuilder.Entity<AuditEventEntity>(entity =>
        {
            entity.ToTable("AuditEvents");
            entity.HasKey(auditEvent => auditEvent.Id);
            entity.Property(auditEvent => auditEvent.EventType).HasMaxLength(200).IsRequired();
            entity.Property(auditEvent => auditEvent.OccurredAt).IsRequired();
            entity.Property(auditEvent => auditEvent.PayloadJson).IsRequired();
            entity.Property(auditEvent => auditEvent.DetailsJson);
            entity.HasIndex(auditEvent => auditEvent.AggregateId);
            entity.HasIndex(auditEvent => auditEvent.OccurredAt);
        });

        modelBuilder.Entity<SessionEntity>(entity =>
        {
            entity.ToTable("Sessions");
            entity.HasKey(session => session.Id);
            entity.Property(session => session.AccessRequestId).IsRequired();
            entity.Property(session => session.Status).IsRequired().IsConcurrencyToken();
            entity.Property(session => session.AllowedTargetsJson).IsRequired();
            entity.Property(session => session.AllowedCapabilitiesJson).IsRequired();
            entity.Property(session => session.SshProfileGrantsJson).IsRequired();
            entity.Property(session => session.CreatedAt).IsRequired();
            entity.Property(session => session.ExpiresAt).IsRequired();
            entity.Property(session => session.ActionCount).IsRequired();
            entity.Property(session => session.MaxActionCount).IsRequired();
            entity.Property(session => session.CompletedAt);
            entity.Property(session => session.RevokedAt);
            entity.Property(session => session.ExpiredAt);
            entity.HasIndex(session => session.AccessRequestId).IsUnique();
            entity.HasIndex(session => session.Status);
            entity.HasIndex(session => session.ExpiresAt);
        });

        modelBuilder.Entity<SshTargetEntity>(entity =>
        {
            entity.ToTable("SshTargets");
            entity.HasKey(target => target.Id);
            entity.Property(target => target.Alias).HasMaxLength(200).IsRequired();
            entity.Property(target => target.Host).HasMaxLength(500).IsRequired();
            entity.Property(target => target.Port).IsRequired();
            entity.Property(target => target.Username).HasMaxLength(200).IsRequired();
            entity.Property(target => target.PrivateKeyPath).HasMaxLength(1000).IsRequired();
            entity.Property(target => target.KnownHostsPath).HasMaxLength(1000).IsRequired();
            entity.Property(target => target.DefaultTimeoutSeconds).IsRequired();
            entity.Property(target => target.DefaultOutputLimitBytes).IsRequired();
            entity.HasIndex(target => target.Alias).IsUnique();
        });

        modelBuilder.Entity<SshProfileEntity>(entity =>
        {
            entity.ToTable("SshProfiles");
            entity.HasKey(profile => profile.Id);
            entity.Property(profile => profile.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(profile => new { profile.TargetId, profile.Name }).IsUnique();
            entity
                .HasOne(profile => profile.Target)
                .WithMany(target => target.Profiles)
                .HasForeignKey(profile => profile.TargetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SshActionEntity>(entity =>
        {
            entity.ToTable("SshActions");
            entity.HasKey(action => action.Id);
            entity.Property(action => action.Name).HasMaxLength(200).IsRequired();
            entity.Property(action => action.CommandJson).IsRequired();
            entity.Property(action => action.CommandTemplateJson).IsRequired();
            entity.Property(action => action.IsMutating).IsRequired();
            entity.Property(action => action.Risk).IsRequired();
            entity.Property(action => action.TimeoutSeconds);
            entity.Property(action => action.OutputLimitBytes);
            entity.HasIndex(action => new { action.TargetId, action.Name }).IsUnique();
            entity
                .HasOne(action => action.Target)
                .WithMany(target => target.Actions)
                .HasForeignKey(action => action.TargetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SshProfileActionEntity>(entity =>
        {
            entity.ToTable("SshProfileActions");
            entity.HasKey(profileAction => new { profileAction.ProfileId, profileAction.ActionId });
            entity
                .HasOne(profileAction => profileAction.Profile)
                .WithMany(profile => profile.ProfileActions)
                .HasForeignKey(profileAction => profileAction.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .HasOne(profileAction => profileAction.Action)
                .WithMany(action => action.ProfileActions)
                .HasForeignKey(profileAction => profileAction.ActionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SshActionAllowedParameterEntity>(entity =>
        {
            entity.ToTable("SshActionAllowedParameters");
            entity.HasKey(parameter => parameter.Id);
            entity.Property(parameter => parameter.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(parameter => new { parameter.ActionId, parameter.Name }).IsUnique();
            entity
                .HasOne(parameter => parameter.Action)
                .WithMany(action => action.AllowedParameters)
                .HasForeignKey(parameter => parameter.ActionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SshActionAllowedParameterValueEntity>(entity =>
        {
            entity.ToTable("SshActionAllowedParameterValues");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Value).HasMaxLength(500).IsRequired();
            entity.HasIndex(value => new { value.AllowedParameterId, value.Value }).IsUnique();
            entity
                .HasOne(value => value.AllowedParameter)
                .WithMany(parameter => parameter.AllowedValues)
                .HasForeignKey(value => value.AllowedParameterId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
