using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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

namespace TraceCore.Infrastructure.Persistence.Configurations;

public class CaseConfiguration : IEntityTypeConfiguration<Case>
{
    public void Configure(EntityTypeBuilder<Case> builder)
    {
        builder.ToTable("Cases");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CaseNumber).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Title).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).IsRequired();
        builder.Property(c => c.RowVersion).IsRowVersion();

        builder.HasIndex(c => c.CaseNumber).IsUnique();
        builder.HasIndex(c => new { c.Status, c.Priority });
        builder.HasIndex(c => c.AssignedInvestigatorId);
        builder.HasIndex(c => c.DueDateUtc);
        builder.HasIndex(c => c.IsSlaBreached);
        builder.HasIndex(c => new { c.CreatedAtUtc, c.CurrentRiskScore });

        builder.HasMany(c => c.Persons)
            .WithOne()
            .HasForeignKey(p => p.CaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Organizations)
            .WithOne()
            .HasForeignKey(o => o.CaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.AccessGrants)
            .WithOne()
            .HasForeignKey(g => g.CaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CasePersonConfiguration : IEntityTypeConfiguration<CasePerson>
{
    public void Configure(EntityTypeBuilder<CasePerson> builder)
    {
        builder.ToTable("CasePersons");
        builder.HasKey(cp => cp.Id);

        builder.Property(cp => cp.Notes).HasMaxLength(1000);
        builder.HasIndex(cp => new { cp.CaseId, cp.PersonId, cp.Role }).IsUnique();
    }
}

public class CaseOrganizationConfiguration : IEntityTypeConfiguration<CaseOrganization>
{
    public void Configure(EntityTypeBuilder<CaseOrganization> builder)
    {
        builder.ToTable("CaseOrganizations");
        builder.HasKey(co => co.Id);

        builder.Property(co => co.Role).HasMaxLength(100).IsRequired();
        builder.Property(co => co.Notes).HasMaxLength(1000);
        builder.HasIndex(co => new { co.CaseId, co.OrganizationId, co.Role }).IsUnique();
    }
}

public class CaseAccessGrantConfiguration : IEntityTypeConfiguration<CaseAccessGrant>
{
    public void Configure(EntityTypeBuilder<CaseAccessGrant> builder)
    {
        builder.ToTable("CaseAccessGrants");
        builder.HasKey(g => g.Id);

        builder.HasIndex(g => new { g.CaseId, g.UserId });
    }
}

public class EvidenceConfiguration : IEntityTypeConfiguration<Evidence>
{
    public void Configure(EntityTypeBuilder<Evidence> builder)
    {
        builder.ToTable("Evidence");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EvidenceNumber).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(200).IsRequired();
        builder.Property(e => e.StorageLocation).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Hash).HasMaxLength(64).IsRequired();
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasIndex(e => new { e.CaseId, e.EvidenceNumber }).IsUnique();
        builder.HasIndex(e => e.Hash);
        builder.HasIndex(e => e.Status);

        builder.HasMany(e => e.CustodyEvents)
            .WithOne()
            .HasForeignKey(c => c.EvidenceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class EvidenceCustodyEventConfiguration : IEntityTypeConfiguration<EvidenceCustodyEvent>
{
    public void Configure(EntityTypeBuilder<EvidenceCustodyEvent> builder)
    {
        builder.ToTable("EvidenceCustodyEvents");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Location).HasMaxLength(255).IsRequired();
        builder.Property(c => c.Notes).HasMaxLength(1000);
        builder.Property(c => c.PreviousHash).HasMaxLength(64).IsRequired();
        builder.Property(c => c.CurrentHash).HasMaxLength(64).IsRequired();

        builder.HasIndex(c => new { c.EvidenceId, c.TimestampUtc });
    }
}

public class InvestigationConfiguration : IEntityTypeConfiguration<Investigation>
{
    public void Configure(EntityTypeBuilder<Investigation> builder)
    {
        builder.ToTable("Investigations");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Title).HasMaxLength(200).IsRequired();
        builder.Property(i => i.RowVersion).IsRowVersion();

        builder.HasIndex(i => new { i.CaseId, i.Status });
        builder.HasIndex(i => i.LeadInvestigatorId);

        builder.HasMany(i => i.Activities)
            .WithOne()
            .HasForeignKey(a => a.InvestigationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class InvestigationActivityConfiguration : IEntityTypeConfiguration<InvestigationActivity>
{
    public void Configure(EntityTypeBuilder<InvestigationActivity> builder)
    {
        builder.ToTable("InvestigationActivities");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Description).HasMaxLength(500).IsRequired();
        builder.Property(a => a.Location).HasMaxLength(255);

        builder.HasIndex(a => new { a.InvestigationId, a.PerformedAtUtc });
    }
}

public class CaseTaskConfiguration : IEntityTypeConfiguration<CaseTask>
{
    public void Configure(EntityTypeBuilder<CaseTask> builder)
    {
        builder.ToTable("CaseTasks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title).HasMaxLength(200).IsRequired();
        builder.Property(t => t.RowVersion).IsRowVersion();

        builder.HasIndex(t => new { t.CaseId, t.Status });
        builder.HasIndex(t => new { t.AssignedToUserId, t.DueDateUtc });
    }
}

public class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("People");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.LastName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Email).HasMaxLength(255);
        builder.Property(p => p.Phone).HasMaxLength(50);
        builder.Property(p => p.ExternalReference).HasMaxLength(100);

        builder.HasIndex(p => p.DisplayName);
        builder.HasIndex(p => p.Email);
    }
}

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();
        builder.Property(o => o.RegistrationNumber).HasMaxLength(100);
        builder.Property(o => o.Industry).HasMaxLength(100);
        builder.Property(o => o.Email).HasMaxLength(255);
        builder.Property(o => o.Phone).HasMaxLength(50);

        builder.HasIndex(o => o.Name);
    }
}

public class EntityRelationshipConfiguration : IEntityTypeConfiguration<EntityRelationship>
{
    public void Configure(EntityTypeBuilder<EntityRelationship> builder)
    {
        builder.ToTable("EntityRelationships");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Description).HasMaxLength(500);

        builder.HasIndex(r => new { r.SourceEntityType, r.SourceEntityId });
        builder.HasIndex(r => new { r.TargetEntityType, r.TargetEntityId });
        builder.HasIndex(r => r.RelationshipType);
        builder.HasIndex(r => r.IsActive);
    }
}

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name).HasMaxLength(200).IsRequired();
        builder.Property(d => d.DocumentType).HasMaxLength(50).IsRequired();
        builder.Property(d => d.Description).HasMaxLength(500);

        builder.HasIndex(d => d.CaseId);

        builder.HasMany(d => d.Versions)
            .WithOne()
            .HasForeignKey(v => v.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.ToTable("DocumentVersions");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.FileName).HasMaxLength(255).IsRequired();
        builder.Property(v => v.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(v => v.StoragePath).HasMaxLength(500).IsRequired();
        builder.Property(v => v.Sha256Hash).HasMaxLength(64).IsRequired();
        builder.Property(v => v.ChangeSummary).HasMaxLength(500);

        builder.HasIndex(v => new { v.DocumentId, v.VersionNumber }).IsUnique();
    }
}

public class RiskAssessmentHistoryConfiguration : IEntityTypeConfiguration<RiskAssessmentHistory>
{
    public void Configure(EntityTypeBuilder<RiskAssessmentHistory> builder)
    {
        builder.ToTable("RiskAssessmentHistories");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.TriggerReason).HasMaxLength(255);
        builder.HasIndex(r => new { r.CaseId, r.EvaluatedAtUtc });
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(100).IsRequired();
        builder.Property(a => a.IpAddress).HasMaxLength(50);
        builder.Property(a => a.CorrelationId).HasMaxLength(100);

        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => a.TimestampUtc);
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.CorrelationId);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(1000).IsRequired();
        builder.Property(n => n.ReferenceType).HasMaxLength(50);

        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAtUtc });
    }
}

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Type).HasMaxLength(255).IsRequired();
        builder.Property(o => o.ContentJson).IsRequired();

        builder.HasIndex(o => new { o.ProcessedOnUtc, o.OccurredOnUtc });
    }
}
