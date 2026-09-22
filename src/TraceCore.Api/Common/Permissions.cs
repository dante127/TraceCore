namespace TraceCore.Api.Common;

public static class Permissions
{
    public const string CaseRead = "Case.Read";
    public const string CaseCreate = "Case.Create";
    public const string CaseUpdate = "Case.Update";
    public const string CaseClose = "Case.Close";
    public const string CaseReopen = "Case.Reopen";

    public const string EvidenceRead = "Evidence.Read";
    public const string EvidenceCreate = "Evidence.Create";
    public const string EvidenceTransfer = "Evidence.Transfer";
    public const string EvidenceArchive = "Evidence.Archive";

    public const string InvestigationRead = "Investigation.Read";
    public const string InvestigationCreate = "Investigation.Create";
    public const string InvestigationAssign = "Investigation.Assign";
    public const string InvestigationReview = "Investigation.Review";

    public const string AuditRead = "Audit.Read";
    public const string ReportsRead = "Reports.Read";
    public const string AdministrationManage = "Administration.Manage";

    public static readonly IReadOnlyList<string> All =
    [
        CaseRead, CaseCreate, CaseUpdate, CaseClose, CaseReopen,
        EvidenceRead, EvidenceCreate, EvidenceTransfer, EvidenceArchive,
        InvestigationRead, InvestigationCreate, InvestigationAssign, InvestigationReview,
        AuditRead, ReportsRead, AdministrationManage
    ];

    public static readonly IReadOnlyList<string> InvestigatorPermissions =
    [
        CaseRead, CaseUpdate,
        EvidenceRead, EvidenceCreate, EvidenceTransfer,
        InvestigationRead, InvestigationCreate, InvestigationReview,
        ReportsRead
    ];

    public static readonly IReadOnlyList<string> CaseManagerPermissions =
    [
        CaseRead, CaseCreate, CaseUpdate, CaseClose, CaseReopen,
        EvidenceRead, EvidenceCreate, EvidenceTransfer, EvidenceArchive,
        InvestigationRead, InvestigationCreate, InvestigationAssign, InvestigationReview,
        ReportsRead
    ];

    public static readonly IReadOnlyList<string> AuditorPermissions =
    [
        CaseRead, EvidenceRead, InvestigationRead, AuditRead, ReportsRead
    ];
}
