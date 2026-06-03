using System;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Core.Sessions;
using Gatekeeper.Infrastructure.Persistence;
using Gatekeeper.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Gatekeeper.Infrastructure.Migrations;

[DbContext(typeof(GatekeeperDbContext))]
public sealed partial class GatekeeperDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");

        modelBuilder.Entity(
            "Gatekeeper.Infrastructure.Persistence.Entities.AccessRequestEntity",
            entity =>
            {
                entity.Property<Guid>("Id").HasColumnType("TEXT");

                entity.Property<DateTimeOffset>("CreatedAt").HasColumnType("TEXT");

                entity.Property<int>("DurationMinutes").HasColumnType("INTEGER");

                entity.Property<string>("ForbiddenActionsJson").IsRequired().HasColumnType("TEXT");

                entity
                    .Property<string>("Intent")
                    .IsRequired()
                    .HasMaxLength(500)
                    .HasColumnType("TEXT");

                entity.Property<string>("Justification").HasColumnType("TEXT");

                entity.Property<string>("MetadataJson").IsRequired().HasColumnType("TEXT");

                entity.Property<string>("ProposedActionsJson").IsRequired().HasColumnType("TEXT");

                entity
                    .Property<string>("RequestedCapabilitiesJson")
                    .IsRequired()
                    .HasColumnType("TEXT");

                entity
                    .Property<string>("Requester")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("TEXT");

                entity.Property<RiskLevel>("Risk").HasColumnType("INTEGER");

                entity
                    .Property<AccessRequestStatus>("Status")
                    .IsConcurrencyToken()
                    .HasColumnType("INTEGER");

                entity.Property<string>("TargetsJson").IsRequired().HasColumnType("TEXT");

                entity.Property<DateTimeOffset>("UpdatedAt").HasColumnType("TEXT");

                entity.HasKey("Id");

                entity.HasIndex("CreatedAt");

                entity.HasIndex("Status");

                entity.ToTable("AccessRequests", (string)null);
            }
        );

        modelBuilder.Entity(
            "Gatekeeper.Infrastructure.Persistence.Entities.AuditEventEntity",
            entity =>
            {
                entity.Property<Guid>("Id").HasColumnType("TEXT");

                entity.Property<Guid?>("AggregateId").HasColumnType("TEXT");

                entity
                    .Property<string>("EventType")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("TEXT");

                entity.Property<DateTimeOffset>("OccurredAt").HasColumnType("TEXT");

                entity.Property<string>("PayloadJson").IsRequired().HasColumnType("TEXT");

                entity.HasKey("Id");

                entity.HasIndex("AggregateId");

                entity.HasIndex("OccurredAt");

                entity.ToTable("AuditEvents", (string)null);
            }
        );

        modelBuilder.Entity(
            "Gatekeeper.Infrastructure.Persistence.Entities.SessionEntity",
            entity =>
            {
                entity.Property<Guid>("Id").HasColumnType("TEXT");

                entity.Property<Guid>("AccessRequestId").HasColumnType("TEXT");

                entity
                    .Property<string>("AllowedCapabilitiesJson")
                    .IsRequired()
                    .HasColumnType("TEXT");

                entity.Property<string>("AllowedTargetsJson").IsRequired().HasColumnType("TEXT");

                entity.Property<string>("SshProfileGrantsJson").IsRequired().HasColumnType("TEXT");

                entity.Property<int>("ActionCount").HasColumnType("INTEGER");

                entity.Property<DateTimeOffset?>("CompletedAt").HasColumnType("TEXT");

                entity.Property<DateTimeOffset>("CreatedAt").HasColumnType("TEXT");

                entity.Property<DateTimeOffset?>("ExpiredAt").HasColumnType("TEXT");

                entity.Property<DateTimeOffset>("ExpiresAt").HasColumnType("TEXT");

                entity.Property<int>("MaxActionCount").HasColumnType("INTEGER");

                entity.Property<DateTimeOffset?>("RevokedAt").HasColumnType("TEXT");

                entity
                    .Property<SessionStatus>("Status")
                    .IsConcurrencyToken()
                    .HasColumnType("INTEGER");

                entity.HasKey("Id");

                entity.HasIndex("AccessRequestId").IsUnique();

                entity.HasIndex("ExpiresAt");

                entity.HasIndex("Status");

                entity.ToTable("Sessions", (string)null);
            }
        );

        modelBuilder.Entity(
            "Gatekeeper.Infrastructure.Persistence.Entities.SshActionAllowedParameterEntity",
            entity =>
            {
                entity.Property<Guid>("Id").HasColumnType("TEXT");

                entity.Property<Guid>("ActionId").HasColumnType("TEXT");

                entity
                    .Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("TEXT");

                entity.HasKey("Id");

                entity.HasIndex("ActionId", "Name").IsUnique();

                entity.ToTable("SshActionAllowedParameters", (string)null);
            }
        );

        modelBuilder.Entity(
            "Gatekeeper.Infrastructure.Persistence.Entities.SshActionAllowedParameterValueEntity",
            entity =>
            {
                entity.Property<Guid>("Id").HasColumnType("TEXT");

                entity.Property<Guid>("AllowedParameterId").HasColumnType("TEXT");

                entity
                    .Property<string>("Value")
                    .IsRequired()
                    .HasMaxLength(500)
                    .HasColumnType("TEXT");

                entity.HasKey("Id");

                entity.HasIndex("AllowedParameterId", "Value").IsUnique();

                entity.ToTable("SshActionAllowedParameterValues", (string)null);
            }
        );

        modelBuilder.Entity(
            "Gatekeeper.Infrastructure.Persistence.Entities.SshActionEntity",
            entity =>
            {
                entity.Property<Guid>("Id").HasColumnType("TEXT");

                entity.Property<string>("CommandJson").IsRequired().HasColumnType("TEXT");

                entity.Property<string>("CommandTemplateJson").IsRequired().HasColumnType("TEXT");

                entity.Property<bool>("IsMutating").HasColumnType("INTEGER");

                entity
                    .Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("TEXT");

                entity.Property<int?>("OutputLimitBytes").HasColumnType("INTEGER");

                entity.Property<RiskLevel>("Risk").HasColumnType("INTEGER");

                entity.Property<Guid>("TargetId").HasColumnType("TEXT");

                entity.Property<int?>("TimeoutSeconds").HasColumnType("INTEGER");

                entity.HasKey("Id");

                entity.HasIndex("TargetId", "Name").IsUnique();

                entity.ToTable("SshActions", (string)null);
            }
        );

        modelBuilder.Entity(
            "Gatekeeper.Infrastructure.Persistence.Entities.SshProfileActionEntity",
            entity =>
            {
                entity.Property<Guid>("ProfileId").HasColumnType("TEXT");

                entity.Property<Guid>("ActionId").HasColumnType("TEXT");

                entity.HasKey("ProfileId", "ActionId");

                entity.HasIndex("ActionId");

                entity.ToTable("SshProfileActions", (string)null);
            }
        );

        modelBuilder.Entity(
            "Gatekeeper.Infrastructure.Persistence.Entities.SshProfileEntity",
            entity =>
            {
                entity.Property<Guid>("Id").HasColumnType("TEXT");

                entity
                    .Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("TEXT");

                entity.Property<Guid>("TargetId").HasColumnType("TEXT");

                entity.HasKey("Id");

                entity.HasIndex("TargetId", "Name").IsUnique();

                entity.ToTable("SshProfiles", (string)null);
            }
        );

        modelBuilder.Entity(
            "Gatekeeper.Infrastructure.Persistence.Entities.SshTargetEntity",
            entity =>
            {
                entity.Property<Guid>("Id").HasColumnType("TEXT");

                entity
                    .Property<string>("Alias")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("TEXT");

                entity.Property<int>("DefaultOutputLimitBytes").HasColumnType("INTEGER");

                entity.Property<int>("DefaultTimeoutSeconds").HasColumnType("INTEGER");

                entity
                    .Property<string>("Host")
                    .IsRequired()
                    .HasMaxLength(500)
                    .HasColumnType("TEXT");

                entity
                    .Property<string>("KnownHostsPath")
                    .IsRequired()
                    .HasMaxLength(1000)
                    .HasColumnType("TEXT");

                entity.Property<int>("Port").HasColumnType("INTEGER");

                entity
                    .Property<string>("PrivateKeyPath")
                    .IsRequired()
                    .HasMaxLength(1000)
                    .HasColumnType("TEXT");

                entity
                    .Property<string>("Username")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("TEXT");

                entity.HasKey("Id");

                entity.HasIndex("Alias").IsUnique();

                entity.ToTable("SshTargets", (string)null);
            }
        );

        modelBuilder.Entity(
            "Gatekeeper.Infrastructure.Persistence.Entities.SshActionAllowedParameterEntity",
            entity =>
            {
                entity
                    .HasOne(
                        "Gatekeeper.Infrastructure.Persistence.Entities.SshActionEntity",
                        "Action"
                    )
                    .WithMany("AllowedParameters")
                    .HasForeignKey("ActionId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                entity.Navigation("Action");
            }
        );

        modelBuilder.Entity(
            "Gatekeeper.Infrastructure.Persistence.Entities.SshActionAllowedParameterValueEntity",
            entity =>
            {
                entity
                    .HasOne(
                        "Gatekeeper.Infrastructure.Persistence.Entities.SshActionAllowedParameterEntity",
                        "AllowedParameter"
                    )
                    .WithMany("AllowedValues")
                    .HasForeignKey("AllowedParameterId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                entity.Navigation("AllowedParameter");
            }
        );

        modelBuilder.Entity(
            "Gatekeeper.Infrastructure.Persistence.Entities.SshActionEntity",
            entity =>
            {
                entity
                    .HasOne(
                        "Gatekeeper.Infrastructure.Persistence.Entities.SshTargetEntity",
                        "Target"
                    )
                    .WithMany("Actions")
                    .HasForeignKey("TargetId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                entity.Navigation("Target");
            }
        );

        modelBuilder.Entity(
            "Gatekeeper.Infrastructure.Persistence.Entities.SshProfileActionEntity",
            entity =>
            {
                entity
                    .HasOne(
                        "Gatekeeper.Infrastructure.Persistence.Entities.SshActionEntity",
                        "Action"
                    )
                    .WithMany("ProfileActions")
                    .HasForeignKey("ActionId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                entity
                    .HasOne(
                        "Gatekeeper.Infrastructure.Persistence.Entities.SshProfileEntity",
                        "Profile"
                    )
                    .WithMany("ProfileActions")
                    .HasForeignKey("ProfileId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                entity.Navigation("Action");

                entity.Navigation("Profile");
            }
        );

        modelBuilder.Entity(
            "Gatekeeper.Infrastructure.Persistence.Entities.SshProfileEntity",
            entity =>
            {
                entity
                    .HasOne(
                        "Gatekeeper.Infrastructure.Persistence.Entities.SshTargetEntity",
                        "Target"
                    )
                    .WithMany("Profiles")
                    .HasForeignKey("TargetId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                entity.Navigation("Target");
            }
        );
    }
}
