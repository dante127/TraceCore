using TraceCore.Domain.Common;
using TraceCore.Domain.Enums;

namespace TraceCore.Domain.Entities.Cases;

public class CasePerson : BaseEntity
{
    public Guid CaseId { get; private set; }
    public Guid PersonId { get; private set; }
    public ParticipantRole Role { get; private set; }
    public string Notes { get; private set; } = string.Empty;
    public DateTime AssignedAtUtc { get; private set; } = DateTime.UtcNow;

    protected CasePerson() : base() { }

    public CasePerson(Guid caseId, Guid personId, ParticipantRole role, string notes) : base()
    {
        CaseId = caseId;
        PersonId = personId;
        Role = role;
        Notes = notes?.Trim() ?? string.Empty;
        AssignedAtUtc = DateTime.UtcNow;
    }

    public void UpdateRole(ParticipantRole newRole, string notes)
    {
        Role = newRole;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            Notes = notes.Trim();
        }
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

public class CaseOrganization : BaseEntity
{
    public Guid CaseId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Role { get; private set; } = default!;
    public string Notes { get; private set; } = string.Empty;
    public DateTime AssignedAtUtc { get; private set; } = DateTime.UtcNow;

    protected CaseOrganization() : base() { }

    public CaseOrganization(Guid caseId, Guid organizationId, string role, string notes) : base()
    {
        CaseId = caseId;
        OrganizationId = organizationId;
        Role = string.IsNullOrWhiteSpace(role) ? "InvolvedEntity" : role.Trim();
        Notes = notes?.Trim() ?? string.Empty;
        AssignedAtUtc = DateTime.UtcNow;
    }

    public void UpdateRole(string newRole, string notes)
    {
        Role = newRole?.Trim() ?? Role;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            Notes = notes.Trim();
        }
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

public class CaseAccessGrant : BaseEntity
{
    public Guid CaseId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? TeamId { get; private set; }
    public CaseAccessLevel AccessLevel { get; private set; }
    public Guid GrantedByUserId { get; private set; }
    public DateTime GrantedAtUtc { get; private set; } = DateTime.UtcNow;

    protected CaseAccessGrant() : base() { }

    public CaseAccessGrant(Guid caseId, Guid userId, CaseAccessLevel accessLevel, Guid grantedByUserId, Guid? teamId = null) : base()
    {
        CaseId = caseId;
        UserId = userId;
        AccessLevel = accessLevel;
        GrantedByUserId = grantedByUserId;
        TeamId = teamId;
        GrantedAtUtc = DateTime.UtcNow;
    }

    public void UpdateAccessLevel(CaseAccessLevel newLevel)
    {
        AccessLevel = newLevel;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
