using Gatekeeper.Application.AccessRequests;
using Gatekeeper.Core.AccessRequests;
using Gatekeeper.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gatekeeper.Infrastructure.Persistence.Repositories;

public sealed class EfAccessRequestRepository : IAccessRequestRepository
{
    private readonly GatekeeperDbContext _dbContext;

    public EfAccessRequestRepository(GatekeeperDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(AccessRequest accessRequest, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accessRequest);

        await _dbContext.AccessRequests.AddAsync(ToEntity(accessRequest), cancellationToken);
    }

    public Task UpdateAsync(AccessRequest accessRequest, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accessRequest);

        AccessRequestEntity entity = ToEntity(accessRequest);
        AccessRequestEntity? tracked = _dbContext.AccessRequests.Local.SingleOrDefault(item =>
            item.Id == accessRequest.Id
        );
        if (tracked is null)
        {
            _dbContext.AccessRequests.Update(entity);
            MarkPendingStatusTransition(entity);
        }
        else
        {
            _dbContext.Entry(tracked).CurrentValues.SetValues(entity);
            MarkPendingStatusTransition(tracked);
        }

        return Task.CompletedTask;
    }

    public async Task<AccessRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        AccessRequestEntity? entity = await _dbContext
            .AccessRequests.AsNoTracking()
            .SingleOrDefaultAsync(accessRequest => accessRequest.Id == id, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<AccessRequest>> ListAsync(CancellationToken cancellationToken)
    {
        AccessRequestEntity[] entities = await _dbContext
            .AccessRequests.AsNoTracking()
            .ToArrayAsync(cancellationToken);

        return entities.Select(ToDomain).ToArray();
    }

    private static AccessRequestEntity ToEntity(AccessRequest accessRequest)
    {
        return new AccessRequestEntity
        {
            Id = accessRequest.Id,
            Intent = accessRequest.Intent,
            Requester = accessRequest.Requester,
            TargetsJson = JsonColumnSerializer.SerializeStringList(accessRequest.Targets),
            RequestedCapabilitiesJson = JsonColumnSerializer.SerializeStringList(
                accessRequest.RequestedCapabilities
            ),
            DurationMinutes = accessRequest.DurationMinutes,
            Risk = accessRequest.Risk,
            Justification = accessRequest.Justification,
            ProposedActionsJson = JsonColumnSerializer.SerializeStringList(
                accessRequest.ProposedActions
            ),
            ForbiddenActionsJson = JsonColumnSerializer.SerializeStringList(
                accessRequest.ForbiddenActions
            ),
            MetadataJson = JsonColumnSerializer.SerializeStringDictionary(accessRequest.Metadata),
            Status = accessRequest.Status,
            CreatedAt = accessRequest.CreatedAt,
            UpdatedAt = accessRequest.UpdatedAt,
        };
    }

    private void MarkPendingStatusTransition(AccessRequestEntity entity)
    {
        if (entity.Status == AccessRequestStatus.Pending)
        {
            return;
        }

        _dbContext.Entry(entity).Property(accessRequest => accessRequest.Status).OriginalValue =
            AccessRequestStatus.Pending;
    }

    private static AccessRequest ToDomain(AccessRequestEntity entity)
    {
        return AccessRequest.Load(
            entity.Id,
            entity.Intent,
            entity.Requester,
            JsonColumnSerializer.DeserializeStringList(entity.TargetsJson),
            JsonColumnSerializer.DeserializeStringList(entity.RequestedCapabilitiesJson),
            entity.DurationMinutes,
            entity.Risk,
            entity.Justification,
            JsonColumnSerializer.DeserializeStringList(entity.ProposedActionsJson),
            JsonColumnSerializer.DeserializeStringList(entity.ForbiddenActionsJson),
            JsonColumnSerializer.DeserializeStringDictionary(entity.MetadataJson),
            entity.Status,
            entity.CreatedAt,
            entity.UpdatedAt
        );
    }
}
