using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Domain.Common;
using TraceCore.Domain.Entities.Audit;
using TraceCore.Domain.Entities.Cases;
using TraceCore.Domain.Entities.Documents;
using TraceCore.Domain.Entities.Evidence;
using TraceCore.Domain.Entities.Investigations;
using TraceCore.Domain.Entities.Notifications;
using TraceCore.Domain.Entities.Organizations;
using TraceCore.Domain.Entities.Outbox;
using TraceCore.Domain.Entities.People;
using TraceCore.Domain.Entities.Relationships;
using TraceCore.Domain.Entities.Risk;
using TraceCore.Domain.Entities.Tasks;
using EvidenceEntity = TraceCore.Domain.Entities.Evidence.Evidence;

using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace TraceCore.Infrastructure.Persistence;

public class TraceCoreDbContext : DbContext, IApplicationDbContext
{
    public TraceCoreDbContext(DbContextOptions<TraceCoreDbContext> options)
        : base(options)
    {
        ChangeTracker.Tracked += OnEntityTracked;
    }

    private void OnEntityTracked(object? sender, EntityTrackedEventArgs e)
    {
        if (!e.FromQuery && e.Entry.State == EntityState.Modified)
        {
            e.Entry.State = EntityState.Added;
        }
    }

    public DbSet<Case> Cases => Set<Case>();
    public DbSet<CasePerson> CasePersons => Set<CasePerson>();
    public DbSet<CaseOrganization> CaseOrganizations => Set<CaseOrganization>();
    public DbSet<CaseAccessGrant> CaseAccessGrants => Set<CaseAccessGrant>();
    public DbSet<EvidenceEntity> Evidence => Set<EvidenceEntity>();
    public DbSet<EvidenceCustodyEvent> EvidenceCustodyEvents => Set<EvidenceCustodyEvent>();
    public DbSet<Investigation> Investigations => Set<Investigation>();
    public DbSet<InvestigationActivity> InvestigationActivities => Set<InvestigationActivity>();
    public DbSet<CaseTask> Tasks => Set<CaseTask>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<EntityRelationship> EntityRelationships => Set<EntityRelationship>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<RiskAssessmentHistory> RiskAssessmentHistories => Set<RiskAssessmentHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    IQueryable<Case> IApplicationDbContext.Cases => Cases;
    IQueryable<CasePerson> IApplicationDbContext.CasePersons => CasePersons;
    IQueryable<CaseOrganization> IApplicationDbContext.CaseOrganizations => CaseOrganizations;
    IQueryable<CaseAccessGrant> IApplicationDbContext.CaseAccessGrants => CaseAccessGrants;
    IQueryable<EvidenceEntity> IApplicationDbContext.Evidence => Evidence;
    IQueryable<EvidenceCustodyEvent> IApplicationDbContext.EvidenceCustodyEvents => EvidenceCustodyEvents;
    IQueryable<Investigation> IApplicationDbContext.Investigations => Investigations;
    IQueryable<InvestigationActivity> IApplicationDbContext.InvestigationActivities => InvestigationActivities;
    IQueryable<CaseTask> IApplicationDbContext.Tasks => Tasks;
    IQueryable<Person> IApplicationDbContext.People => People;
    IQueryable<Organization> IApplicationDbContext.Organizations => Organizations;
    IQueryable<EntityRelationship> IApplicationDbContext.EntityRelationships => EntityRelationships;
    IQueryable<Document> IApplicationDbContext.Documents => Documents;
    IQueryable<DocumentVersion> IApplicationDbContext.DocumentVersions => DocumentVersions;
    IQueryable<RiskAssessmentHistory> IApplicationDbContext.RiskAssessmentHistories => RiskAssessmentHistories;
    IQueryable<AuditLog> IApplicationDbContext.AuditLogs => AuditLogs;
    IQueryable<Notification> IApplicationDbContext.Notifications => Notifications;
    IQueryable<OutboxMessage> IApplicationDbContext.OutboxMessages => OutboxMessages;

    void IApplicationDbContext.Add<TEntity>(TEntity entity) => Set<TEntity>().Add(entity);
    void IApplicationDbContext.Update<TEntity>(TEntity entity) => Set<TEntity>().Update(entity);
    void IApplicationDbContext.Remove<TEntity>(TEntity entity) => Set<TEntity>().Remove(entity);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            modelBuilder.Entity<Case>().Property(c => c.RowVersion).ValueGeneratedNever().IsConcurrencyToken(false);
            modelBuilder.Entity<EvidenceEntity>().Property(e => e.RowVersion).ValueGeneratedNever().IsConcurrencyToken(false);
            modelBuilder.Entity<Investigation>().Property(i => i.RowVersion).ValueGeneratedNever().IsConcurrencyToken(false);
            modelBuilder.Entity<CaseTask>().Property(t => t.RowVersion).ValueGeneratedNever().IsConcurrencyToken(false);
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 1. Extract domain events from all modified entities
        var domainEntities = ChangeTracker
            .Entries<BaseEntity>()
            .Where(x => x.Entity.DomainEvents.Count != 0)
            .Select(x => x.Entity)
            .ToList();

        var domainEvents = domainEntities
            .SelectMany(x => x.DomainEvents)
            .ToList();

        // 2. Clear domain events so they are not re-published
        domainEntities.ForEach(entity => entity.ClearDomainEvents());

        // 3. Serialize into OutboxMessages in the exact same transaction
        foreach (var domainEvent in domainEvents)
        {
            string typeName = domainEvent.GetType().AssemblyQualifiedName ?? domainEvent.GetType().FullName!;
            string json = JsonSerializer.Serialize(domainEvent, domainEvent.GetType());

            var outboxMessage = new OutboxMessage(domainEvent.OccurredOnUtc, typeName, json);
            OutboxMessages.Add(outboxMessage);
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
