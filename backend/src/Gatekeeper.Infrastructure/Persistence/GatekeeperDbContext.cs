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
    }
}
