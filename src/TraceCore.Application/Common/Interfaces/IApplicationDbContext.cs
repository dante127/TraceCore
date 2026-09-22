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

namespace TraceCore.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    IQueryable<Case> Cases { get; }
    IQueryable<CasePerson> CasePersons { get; }
    IQueryable<CaseOrganization> CaseOrganizations { get; }
    IQueryable<CaseAccessGrant> CaseAccessGrants { get; }
    IQueryable<EvidenceEntity> Evidence { get; }
    IQueryable<EvidenceCustodyEvent> EvidenceCustodyEvents { get; }
    IQueryable<Investigation> Investigations { get; }
    IQueryable<InvestigationActivity> InvestigationActivities { get; }
    IQueryable<CaseTask> Tasks { get; }
    IQueryable<Person> People { get; }
    IQueryable<Organization> Organizations { get; }
    IQueryable<EntityRelationship> EntityRelationships { get; }
    IQueryable<Document> Documents { get; }
    IQueryable<DocumentVersion> DocumentVersions { get; }
    IQueryable<RiskAssessmentHistory> RiskAssessmentHistories { get; }
    IQueryable<AuditLog> AuditLogs { get; }
    IQueryable<Notification> Notifications { get; }
    IQueryable<OutboxMessage> OutboxMessages { get; }

    void Add<TEntity>(TEntity entity) where TEntity : class;
    void Update<TEntity>(TEntity entity) where TEntity : class;
    void Remove<TEntity>(TEntity entity) where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
