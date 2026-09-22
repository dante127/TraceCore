# TraceCore Database Design & Schema

TraceCore utilizes **Microsoft SQL Server** as its primary relational datastore with **Entity Framework Core 10**. This document details the database schema, entity relationships, indexes, constraints, concurrency controls, and design patterns.

---

## 1. Entity Relationship Diagram (ERD)

```mermaid
erDiagram
    USERS ||--o{ CASE_ACCESS_GRANTS : receives
    USERS ||--o{ NOTIFICATIONS : receives
    USERS ||--o{ AUDIT_LOGS : performs
    USERS ||--o{ EVIDENCE_CUSTODY_EVENTS : initiates

    CASES ||--o{ CASE_PERSONS : involves
    PEOPLE ||--o{ CASE_PERSONS : participates_in

    CASES ||--o{ CASE_ORGANIZATIONS : involves
    ORGANIZATIONS ||--o{ CASE_ORGANIZATIONS : participates_in

    CASES ||--o{ INVESTIGATIONS : contains
    INVESTIGATIONS ||--o{ INVESTIGATION_ACTIVITIES : records

    CASES ||--o{ EVIDENCE : gathers
    EVIDENCE ||--o{ EVIDENCE_CUSTODY_EVENTS : tracks_custody

    CASES ||--o{ DOCUMENTS : attaches
    DOCUMENTS ||--o{ DOCUMENT_VERSIONS : tracks_versions

    CASES ||--o{ CASE_TASKS : assigns
    INVESTIGATIONS ||--o{ CASE_TASKS : assigns

    CASES ||--o{ RISK_ASSESSMENT_HISTORY : logs_risk
    CASES ||--o{ CASE_ACCESS_GRANTS : controls_access

    ENTITY_RELATIONSHIPS }o--|| CASES : links
    ENTITY_RELATIONSHIPS }o--|| PEOPLE : links
    ENTITY_RELATIONSHIPS }o--|| ORGANIZATIONS : links

    CASES {
        uniqueidentifier Id PK
        nvarchar(50) CaseNumber UK
        nvarchar(200) Title
        nvarchar(max) Description
        int Type
        int Status
        int Priority
        int ConfidentialityLevel
        uniqueidentifier CreatedByUserId
        uniqueidentifier AssignedTeamId
        uniqueidentifier AssignedInvestigatorId
        datetime2 OpenedAtUtc
        datetime2 DueDateUtc
        datetime2 ClosedAtUtc
        int SlaTargetHours
        datetime2 SlaDeadlineUtc
        bit IsSlaBreached
        datetime2 SlaBreachedAtUtc
        int CurrentRiskScore
        int CurrentRiskLevel
        datetime2 CreatedAtUtc
        datetime2 UpdatedAtUtc
        rowversion RowVersion
    }

    EVIDENCE {
        uniqueidentifier Id PK
        uniqueidentifier CaseId FK
        nvarchar(50) EvidenceNumber
        int Type
        nvarchar(500) Description
        nvarchar(200) Source
        datetime2 CollectedAtUtc
        uniqueidentifier CollectedByUserId
        int Status
        int ConfidentialityLevel
        nvarchar(255) StorageLocation
        nvarchar(64) Hash
        bit IsCritical
        datetime2 CreatedAtUtc
        datetime2 UpdatedAtUtc
        rowversion RowVersion
    }

    EVIDENCE_CUSTODY_EVENTS {
        uniqueidentifier Id PK
        uniqueidentifier EvidenceId FK
        int Action
        uniqueidentifier FromUserId
        uniqueidentifier ToUserId
        datetime2 TimestampUtc
        nvarchar(255) Location
        nvarchar(1000) Notes
        nvarchar(64) PreviousHash
        nvarchar(64) CurrentHash
    }

    INVESTIGATIONS {
        uniqueidentifier Id PK
        uniqueidentifier CaseId FK
        nvarchar(200) Title
        uniqueidentifier LeadInvestigatorId
        nvarchar(max) Objectives
        nvarchar(max) Findings
        int Status
        datetime2 StartDateUtc
        datetime2 EndDateUtc
        datetime2 CreatedAtUtc
        datetime2 UpdatedAtUtc
        rowversion RowVersion
    }

    INVESTIGATION_ACTIVITIES {
        uniqueidentifier Id PK
        uniqueidentifier InvestigationId FK
        int ActivityType
        uniqueidentifier PerformedByUserId
        datetime2 PerformedAtUtc
        nvarchar(500) Description
        nvarchar(max) Outcome
        nvarchar(255) Location
        nvarchar(max) Notes
        datetime2 CreatedAtUtc
    }

    DOCUMENTS {
        uniqueidentifier Id PK
        uniqueidentifier CaseId FK
        nvarchar(200) Name
        nvarchar(50) DocumentType
        nvarchar(500) Description
        int ConfidentialityLevel
        uniqueidentifier CreatedByUserId
        datetime2 CreatedAtUtc
        datetime2 UpdatedAtUtc
    }

    DOCUMENT_VERSIONS {
        uniqueidentifier Id PK
        uniqueidentifier DocumentId FK
        int VersionNumber
        nvarchar(255) FileName
        nvarchar(100) ContentType
        bigint FileSizeBytes
        nvarchar(500) StoragePath
        nvarchar(64) Sha256Hash
        nvarchar(500) ChangeSummary
        uniqueidentifier UploadedByUserId
        datetime2 UploadedAtUtc
    }

    CASE_TASKS {
        uniqueidentifier Id PK
        uniqueidentifier CaseId FK
        uniqueidentifier InvestigationId FK
        nvarchar(200) Title
        nvarchar(max) Description
        uniqueidentifier AssignedToUserId
        int Priority
        int Status
        datetime2 DueDateUtc
        datetime2 CompletedAtUtc
        datetime2 CreatedAtUtc
        datetime2 UpdatedAtUtc
        rowversion RowVersion
    }

    PEOPLE {
        uniqueidentifier Id PK
        nvarchar(100) FirstName
        nvarchar(100) LastName
        nvarchar(200) DisplayName
        nvarchar(255) Email
        nvarchar(50) Phone
        nvarchar(100) ExternalReference
        datetime2 DateOfBirth
        nvarchar(max) Notes
        datetime2 CreatedAtUtc
        datetime2 UpdatedAtUtc
    }

    ORGANIZATIONS {
        uniqueidentifier Id PK
        nvarchar(200) Name
        nvarchar(100) RegistrationNumber
        nvarchar(100) Industry
        nvarchar(255) Email
        nvarchar(50) Phone
        nvarchar(500) Address
        nvarchar(max) Notes
        datetime2 CreatedAtUtc
        datetime2 UpdatedAtUtc
    }

    ENTITY_RELATIONSHIPS {
        uniqueidentifier Id PK
        uniqueidentifier SourceEntityId
        int SourceEntityType
        uniqueidentifier TargetEntityId
        int TargetEntityType
        int RelationshipType
        nvarchar(500) Description
        float ConfidenceScore
        datetime2 StartDateUtc
        datetime2 EndDateUtc
        bit IsActive
        datetime2 CreatedAtUtc
    }

    AUDIT_LOGS {
        uniqueidentifier Id PK
        uniqueidentifier UserId
        nvarchar(100) Action
        nvarchar(100) EntityType
        nvarchar(100) EntityId
        datetime2 TimestampUtc
        nvarchar(50) IpAddress
        nvarchar(100) CorrelationId
        nvarchar(max) BeforeJson
        nvarchar(max) AfterJson
    }

    OUTBOX_MESSAGES {
        uniqueidentifier Id PK
        datetime2 OccurredOnUtc
        nvarchar(255) Type
        nvarchar(max) ContentJson
        datetime2 ProcessedOnUtc
        nvarchar(max) Error
        int RetryCount
    }
```

---

## 2. Table Specifications & Integrity Constraints

### `Cases`
- **Primary Key**: `Id` (`uniqueidentifier`, non-clustered or clustered GUID).
- **Unique Key**: `CaseNumber` (Formatted identifier, e.g., `CAS-2026-0001`).
- **Concurrency Token**: `RowVersion` (`rowversion / timestamp`).
- **Indexes**:
  - `IX_Cases_CaseNumber` (Unique)
  - `IX_Cases_Status_Priority` (Composite: facilitates dashboard and worklist filtering)
  - `IX_Cases_AssignedInvestigatorId` (Facilitates investigator dashboard lookup)
  - `IX_Cases_SlaDeadlineUtc_IsSlaBreached` (Filtered index for SLA background monitoring: `WHERE IsSlaBreached = 0 AND Status NOT IN (4, 5)`)
  - `IX_Cases_CreatedAtUtc` (Sorting and date-range queries)

### `Evidence` & `EvidenceCustodyEvents`
- **Tamper-Evident Chain of Custody**:
  - Each `EvidenceCustodyEvent` contains `PreviousHash` and `CurrentHash`.
  - `PreviousHash` strictly matches the preceding event's `CurrentHash`.
  - Cryptographic verification algorithm recalculates SHA-256 over:
    `SHA256(EvidenceId + Action + FromUserId + ToUserId + TimestampUtc + PreviousHash + Location)`
  - Append-only guarantee: `EvidenceCustodyEvents` records are immutable; modifications or deletions trigger application exceptions.
- **Indexes**:
  - `IX_Evidence_CaseId_EvidenceNumber` (Unique per case)
  - `IX_Evidence_Hash` (Quick deduplication and integrity audits)
  - `IX_EvidenceCustodyEvents_EvidenceId_TimestampUtc` (Clustered/ordered timeline retrieval)

### `EntityRelationships` (Universal Graph Schema)
- Connects any system entity (`Person`, `Organization`, `Case`, `Evidence`, `Document`) to any other entity.
- **Indexes**:
  - `IX_EntityRelationships_Source` (`SourceEntityType`, `SourceEntityId`)
  - `IX_EntityRelationships_Target` (`TargetEntityType`, `TargetEntityId`)
  - `IX_EntityRelationships_Type` (`RelationshipType`)
- Graph queries utilize recursive Common Table Expressions (CTEs) in SQL Server to find paths, cycles, and multi-hop ego graphs efficiently up to configurable depth without requiring an external graph database.

### `AuditLogs`
- Implements immutable system change tracking.
- Captured automatically via EF Core `SaveChangesInterceptor`.
- Serializes full entity state `BeforeJson` and `AfterJson` on state modifications.
- **Indexes**:
  - `IX_AuditLogs_EntityType_EntityId` (Entity history drill-down)
  - `IX_AuditLogs_TimestampUtc` (Time-series audit search)
  - `IX_AuditLogs_UserId` (User activity tracking)
  - `IX_AuditLogs_CorrelationId` (End-to-end request tracing)

### `OutboxMessages`
- Implements the Transactional Outbox pattern.
- Domain events are written to `OutboxMessages` within the same database transaction as business entities.
- **Indexes**:
  - `IX_OutboxMessages_ProcessedOnUtc_OccurredOnUtc` (Filtered index: `WHERE ProcessedOnUtc IS NULL`) for ultra-fast polling by background workers.

---

## 3. Concurrency Strategy
TraceCore uses optimistic concurrency control for all mutating aggregates:
- `Case`, `Evidence`, `Investigation`, and `CaseTask` declare a `byte[] RowVersion` column.
- In EF Core, this is configured via:
  ```csharp
  builder.Property(e => e.RowVersion).IsRowVersion();
  ```
- When a client issues an update, the request includes the `RowVersion`. If another transaction updated the row concurrently, EF Core raises `DbUpdateConcurrencyException`.
- The API's global exception handling middleware maps this to an RFC 7807 `ProblemDetails` response with HTTP Status `409 Conflict`.
