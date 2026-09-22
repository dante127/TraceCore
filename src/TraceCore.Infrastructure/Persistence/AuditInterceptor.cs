using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Domain.Common;
using TraceCore.Domain.Entities.Audit;
using TraceCore.Domain.Entities.Outbox;

namespace TraceCore.Infrastructure.Persistence;

public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;

    public AuditInterceptor(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        var auditEntries = new List<AuditLog>();
        var userId = _currentUser.UserId;
        var correlationId = Guid.NewGuid().ToString(); // correlation tracing
        var timestamp = DateTime.UtcNow;

        foreach (var entry in eventData.Context.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog or OutboxMessage || entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            string entityType = entry.Entity.GetType().Name;
            var primaryKey = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString() ?? "Unknown";

            string action = entry.State switch
            {
                EntityState.Added => "Insert",
                EntityState.Modified => "Update",
                EntityState.Deleted => "Delete",
                _ => entry.State.ToString()
            };

            var before = new Dictionary<string, object?>();
            var after = new Dictionary<string, object?>();

            foreach (var property in entry.Properties)
            {
                if (property.Metadata.IsPrimaryKey() || property.Metadata.Name == "RowVersion")
                    continue;

                string propName = property.Metadata.Name;

                switch (entry.State)
                {
                    case EntityState.Added:
                        after[propName] = property.CurrentValue;
                        break;
                    case EntityState.Deleted:
                        before[propName] = property.OriginalValue;
                        break;
                    case EntityState.Modified:
                        if (property.IsModified)
                        {
                            before[propName] = property.OriginalValue;
                            after[propName] = property.CurrentValue;
                        }
                        break;
                }
            }

            string? beforeJson = before.Count > 0 ? JsonSerializer.Serialize(before) : null;
            string? afterJson = after.Count > 0 ? JsonSerializer.Serialize(after) : null;

            auditEntries.Add(new AuditLog(
                userId,
                action,
                entityType,
                primaryKey,
                null,
                correlationId,
                beforeJson,
                afterJson));
        }

        if (auditEntries.Count > 0)
        {
            eventData.Context.Set<AuditLog>().AddRange(auditEntries);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
