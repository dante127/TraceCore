using TraceCore.Domain.Common;
using TraceCore.Domain.Common.Exceptions;
using TraceCore.Domain.Enums;

namespace TraceCore.Domain.Entities.Relationships;

public class EntityRelationship : BaseEntity, IAggregateRoot
{
    public Guid SourceEntityId { get; private set; }
    public EntityType SourceEntityType { get; private set; }
    public Guid TargetEntityId { get; private set; }
    public EntityType TargetEntityType { get; private set; }
    public RelationshipType RelationshipType { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public float ConfidenceScore { get; private set; }
    public DateTime? StartDateUtc { get; private set; }
    public DateTime? EndDateUtc { get; private set; }
    public bool IsActive { get; private set; }

    protected EntityRelationship() : base() { }

    public EntityRelationship(
        Guid sourceEntityId,
        EntityType sourceEntityType,
        Guid targetEntityId,
        EntityType targetEntityType,
        RelationshipType relationshipType,
        string description = "",
        float confidenceScore = 1.0f,
        DateTime? startDateUtc = null,
        DateTime? endDateUtc = null) : base()
    {
        if (sourceEntityId == Guid.Empty || targetEntityId == Guid.Empty)
            throw new DomainException("Source and Target entity IDs must be valid non-empty GUIDs.");
        if (sourceEntityId == targetEntityId && sourceEntityType == targetEntityType)
            throw new DomainException("An entity cannot have a relationship with itself.");

        SourceEntityId = sourceEntityId;
        SourceEntityType = sourceEntityType;
        TargetEntityId = targetEntityId;
        TargetEntityType = targetEntityType;
        RelationshipType = relationshipType;
        Description = description?.Trim() ?? string.Empty;
        ConfidenceScore = Math.Clamp(confidenceScore, 0.0f, 1.0f);
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        EndDateUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateConfidence(float confidenceScore, string description)
    {
        ConfidenceScore = Math.Clamp(confidenceScore, 0.0f, 1.0f);
        if (!string.IsNullOrWhiteSpace(description))
        {
            Description = description.Trim();
        }
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
