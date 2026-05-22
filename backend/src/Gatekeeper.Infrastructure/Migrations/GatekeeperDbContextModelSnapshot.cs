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

                entity.Property<DateTimeOffset>("CreatedAt").HasColumnType("TEXT");

                entity.Property<DateTimeOffset>("ExpiresAt").HasColumnType("TEXT");

                entity.Property<SessionStatus>("Status").HasColumnType("INTEGER");

                entity.HasKey("Id");

                entity.HasIndex("AccessRequestId").IsUnique();

                entity.HasIndex("ExpiresAt");

                entity.HasIndex("Status");

                entity.ToTable("Sessions", (string)null);
            }
        );
    }
}
