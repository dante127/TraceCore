# TraceCore API Specification

All TraceCore API endpoints are versioned under `/api/v1/` and return standard JSON payloads.

---

## 1. Global Conventions

### Response Format & Pagination
List responses are wrapped in a standard paginated response envelope:
```json
{
  "items": [ ... ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 142,
  "totalPages": 8,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

### Error Handling (RFC 7807 Problem Details)
Errors return standardized ProblemDetails with appropriate HTTP status codes:
```json
{
  "type": "https://tracecore.dev/errors/concurrency-conflict",
  "title": "A concurrency conflict occurred",
  "status": 409,
  "detail": "The record was updated or deleted by another transaction.",
  "instance": "/api/v1/cases/550e8400-e29b-41d4-a716-446655440000",
  "traceId": "00-983b6329..."
}
```

---

## 2. API Endpoints Overview

### Authentication (`/api/v1/auth`)
- `POST /api/v1/auth/login`: Authenticates user with email & password, returns JWT token with claims, permissions, and roles.
- `GET /api/v1/auth/me`: Returns the currently authenticated user profile and permissions.

### Cases (`/api/v1/cases`)
- `GET /api/v1/cases`: Returns paginated list of cases with filtering (status, priority, investigator, date ranges).
- `GET /api/v1/cases/{id}`: Returns complete details of a single case.
- `POST /api/v1/cases`: Creates a new case in `Draft` or `Open` status.
- `PUT /api/v1/cases/{id}`: Updates case details (requires `RowVersion` for optimistic concurrency).
- `POST /api/v1/cases/{id}/status`: Transitions case status (`Open`, `UnderInvestigation`, `PendingReview`, `Resolved`, `Closed`, `Reopened`).
- `POST /api/v1/cases/{id}/assign`: Assigns investigator or investigative team.
- `POST /api/v1/cases/{id}/priority`: Adjusts case priority with audit justification.
- `GET /api/v1/cases/{id}/timeline`: Chronological timeline of all case events, investigation milestones, evidence custody, and status changes.
- `GET /api/v1/cases/{id}/dashboard`: High-level metrics for case oversight.

### Evidence & Custody (`/api/v1/evidence`)
- `GET /api/v1/evidence?caseId={id}`: Returns evidence items gathered in a case.
- `GET /api/v1/evidence/{id}`: Returns evidence details with current custodian and hash.
- `POST /api/v1/evidence`: Registers a new evidence item with cryptographic SHA-256 hash.
- `POST /api/v1/evidence/{id}/transfer`: Executes a chain of custody transfer (From, To, Action, Location, Notes) with hash chaining.
- `GET /api/v1/evidence/{id}/custody`: Returns immutable chronological chain of custody events.
- `GET /api/v1/evidence/{id}/verify`: Performs cryptographic verification of the complete custody hash chain.

### Documents (`/api/v1/documents`)
- `GET /api/v1/documents?caseId={id}`: Returns documents attached to a case.
- `POST /api/v1/documents`: Creates document entry and uploads initial version (multipart/form-data).
- `POST /api/v1/documents/{id}/versions`: Uploads a new version of an existing document.
- `GET /api/v1/documents/{id}/versions/{versionNumber}/download`: Streams the specific document version file.

### Investigations (`/api/v1/investigations`)
- `GET /api/v1/investigations?caseId={id}`: Returns investigations belonging to a case.
- `GET /api/v1/investigations/{id}`: Returns investigation objectives, findings, status, and activities.
- `POST /api/v1/investigations`: Initiates an investigation under a case.
- `POST /api/v1/investigations/{id}/activities`: Records an investigation activity (Interview, SiteVisit, Review, etc.).
- `PUT /api/v1/investigations/{id}/findings`: Updates findings and conclusions.
- `POST /api/v1/investigations/{id}/status`: Updates status (`Active`, `Suspended`, `Completed`, `Closed`).

### Tasks (`/api/v1/tasks`)
- `GET /api/v1/tasks`: Returns tasks with filters (caseId, assignedTo, status, isOverdue).
- `POST /api/v1/tasks`: Creates a task for a case or investigation.
- `PUT /api/v1/tasks/{id}/status`: Transitions task status (`Todo`, `InProgress`, `Blocked`, `Completed`, `Cancelled`).
- `PUT /api/v1/tasks/{id}/assign`: Reassigns task to another investigator.

### Entity Relationship Graph (`/api/v1/relationships` & `/api/v1/entities`)
- `POST /api/v1/relationships`: Creates a directed relationship between two entities (`Person`, `Organization`, `Case`, `Evidence`, `Document`).
- `GET /api/v1/entities/{entityType}/{id}/relationships`: Returns direct relationships connected to the entity.
- `GET /api/v1/entities/{entityType}/{id}/graph?depth=2`: Returns ego graph (nodes and edges up to depth N).
- `GET /api/v1/entities/paths?sourceType=Person&sourceId={id}&targetType=Organization&targetId={id}`: Finds shortest connection paths between entities.

### Risk Management (`/api/v1/risk`)
- `POST /api/v1/risk/cases/{caseId}/assess`: Runs deterministic rule-based evaluation for a case.
- `GET /api/v1/risk/cases/{caseId}/history`: Retrieves historical risk scores, breakdowns, and trigger reasons.

### Search (`/api/v1/search`)
- `GET /api/v1/search`: Advanced multi-entity search with full-text keyword matching, status, priority, date filters, and facet counters.

### Audit & Reports (`/api/v1/audit` & `/api/v1/reports`)
- `GET /api/v1/audit`: Searches immutable audit log by entity, user, date range, or correlation ID.
- `GET /api/v1/reports/sla-compliance`: Returns SLA breach metrics, resolution velocity, and breach causes.
- `GET /api/v1/reports/risk-distribution`: Aggregates active cases by risk tiers.
