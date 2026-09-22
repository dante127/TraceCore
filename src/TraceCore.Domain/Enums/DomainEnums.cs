namespace TraceCore.Domain.Enums;

public enum CaseStatus
{
    Draft = 1,
    Open = 2,
    UnderInvestigation = 3,
    PendingReview = 4,
    Resolved = 5,
    Closed = 6,
    Reopened = 7
}

public enum CasePriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum CaseType
{
    Fraud = 1,
    CyberCrime = 2,
    FinancialCrime = 3,
    InternalAffairs = 4,
    Regulatory = 5,
    Compliance = 6,
    IntellectualProperty = 7,
    Other = 99
}

public enum ConfidentialityLevel
{
    Public = 1,
    Internal = 2,
    Confidential = 3,
    Restricted = 4,
    TopSecret = 5
}

public enum ParticipantRole
{
    Subject = 1,
    Witness = 2,
    Victim = 3,
    Investigator = 4,
    Reporter = 5,
    Contact = 6,
    Other = 99
}

public enum EvidenceType
{
    Document = 1,
    Image = 2,
    Video = 3,
    Audio = 4,
    Physical = 5,
    Digital = 6,
    Other = 99
}

public enum EvidenceStatus
{
    Collected = 1,
    InCustody = 2,
    InAnalysis = 3,
    Transferred = 4,
    CourtExhibited = 5,
    Archived = 6,
    Disposed = 7
}

public enum CustodyAction
{
    Collected = 1,
    Transferred = 2,
    Received = 3,
    CheckedOutForAnalysis = 4,
    CheckedIn = 5,
    CourtPresented = 6,
    Archived = 7,
    Destroyed = 8
}

public enum InvestigationStatus
{
    Planned = 1,
    Active = 2,
    Suspended = 3,
    Completed = 4,
    Closed = 5
}

public enum ActivityType
{
    Interview = 1,
    EvidenceReview = 2,
    DocumentReview = 3,
    SiteVisit = 4,
    Research = 5,
    Communication = 6,
    Other = 99
}

public enum TaskStatus
{
    Todo = 1,
    InProgress = 2,
    Blocked = 3,
    Completed = 4,
    Cancelled = 5
}

public enum TaskPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Urgent = 4
}

public enum EntityType
{
    Case = 1,
    Person = 2,
    Organization = 3,
    Evidence = 4,
    Document = 5,
    Investigation = 6
}

public enum RelationshipType
{
    EmployeeOf = 1,
    MemberOf = 2,
    RelatedTo = 3,
    ParentCompanyOf = 4,
    MentionedIn = 5,
    OwnsAsset = 6,
    AssociateOf = 7,
    FinancialTransactionWith = 8,
    Witnessed = 9
}

public enum CaseAccessLevel
{
    Read = 1,
    ReadWrite = 2,
    Admin = 3
}

public enum RiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum NotificationType
{
    TaskAssigned = 1,
    TaskDueSoon = 2,
    TaskOverdue = 3,
    SlaBreached = 4,
    CaseAssigned = 5,
    CaseStatusChanged = 6,
    ReviewRequired = 7,
    RiskLevelChanged = 8
}
