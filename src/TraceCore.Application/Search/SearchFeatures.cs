using MediatR;
using Microsoft.EntityFrameworkCore;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Application.Common.Models;
using TraceCore.Domain.Enums;

namespace TraceCore.Application.Search;

public sealed record SearchResultItemDto(
    Guid Id,
    EntityType EntityType,
    string Title,
    string Subtitle,
    string Description,
    string Status,
    string? Tag,
    DateTime CreatedAtUtc);

public sealed record SearchSummaryDto(
    IReadOnlyList<SearchResultItemDto> Items,
    int TotalMatches,
    Dictionary<string, int> MatchCountsByEntityType);

public sealed class SearchCasesAndEntitiesQuery : PagedRequest, IRequest<SearchSummaryDto>
{
    public string? Query { get; set; }
    public EntityType? EntityType { get; set; }
    public CaseStatus? Status { get; set; }
    public CasePriority? Priority { get; set; }
    public RiskLevel? RiskLevel { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public class SearchCasesAndEntitiesQueryHandler : IRequestHandler<SearchCasesAndEntitiesQuery, SearchSummaryDto>
{
    private readonly IApplicationDbContext _context;

    public SearchCasesAndEntitiesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SearchSummaryDto> Handle(SearchCasesAndEntitiesQuery request, CancellationToken cancellationToken)
    {
        var results = new List<SearchResultItemDto>();
        var term = request.Query?.Trim().ToLower() ?? string.Empty;

        // 1. Cases Search
        if (!request.EntityType.HasValue || request.EntityType == Domain.Enums.EntityType.Case)
        {
            var caseQuery = _context.Cases.AsNoTracking();

            if (!string.IsNullOrEmpty(term))
            {
                caseQuery = caseQuery.Where(c => c.CaseNumber.ToLower().Contains(term) ||
                                                 c.Title.ToLower().Contains(term) ||
                                                 c.Description.ToLower().Contains(term));
            }

            if (request.Status.HasValue)
                caseQuery = caseQuery.Where(c => c.Status == request.Status.Value);
            if (request.Priority.HasValue)
                caseQuery = caseQuery.Where(c => c.Priority == request.Priority.Value);
            if (request.RiskLevel.HasValue)
                caseQuery = caseQuery.Where(c => c.CurrentRiskLevel == request.RiskLevel.Value);
            if (request.FromUtc.HasValue)
                caseQuery = caseQuery.Where(c => c.CreatedAtUtc >= request.FromUtc.Value);
            if (request.ToUtc.HasValue)
                caseQuery = caseQuery.Where(c => c.CreatedAtUtc <= request.ToUtc.Value);

            var cases = await caseQuery
                .OrderByDescending(c => c.CreatedAtUtc)
                .Take(50)
                .Select(c => new SearchResultItemDto(
                    c.Id,
                    Domain.Enums.EntityType.Case,
                    c.CaseNumber + " - " + c.Title,
                    c.Type.ToString(),
                    c.Description,
                    c.Status.ToString(),
                    c.Priority.ToString(),
                    c.CreatedAtUtc))
                .ToListAsync(cancellationToken);

            results.AddRange(cases);
        }

        // 2. People Search
        if (!request.EntityType.HasValue || request.EntityType == Domain.Enums.EntityType.Person)
        {
            var peopleQuery = _context.People.AsNoTracking();
            if (!string.IsNullOrEmpty(term))
            {
                peopleQuery = peopleQuery.Where(p => p.DisplayName.ToLower().Contains(term) ||
                                                     (p.Email != null && p.Email.ToLower().Contains(term)) ||
                                                     p.Notes.ToLower().Contains(term));
            }

            var people = await peopleQuery
                .OrderBy(p => p.LastName)
                .Take(25)
                .Select(p => new SearchResultItemDto(
                    p.Id,
                    Domain.Enums.EntityType.Person,
                    p.DisplayName,
                    p.Email ?? "No Email",
                    p.Notes,
                    "Active",
                    p.ExternalReference,
                    p.CreatedAtUtc))
                .ToListAsync(cancellationToken);

            results.AddRange(people);
        }

        // 3. Organizations Search
        if (!request.EntityType.HasValue || request.EntityType == Domain.Enums.EntityType.Organization)
        {
            var orgQuery = _context.Organizations.AsNoTracking();
            if (!string.IsNullOrEmpty(term))
            {
                orgQuery = orgQuery.Where(o => o.Name.ToLower().Contains(term) ||
                                               (o.Industry != null && o.Industry.ToLower().Contains(term)));
            }

            var orgs = await orgQuery
                .OrderBy(o => o.Name)
                .Take(25)
                .Select(o => new SearchResultItemDto(
                    o.Id,
                    Domain.Enums.EntityType.Organization,
                    o.Name,
                    o.Industry ?? "Organization",
                    o.Notes,
                    "Active",
                    o.RegistrationNumber,
                    o.CreatedAtUtc))
                .ToListAsync(cancellationToken);

            results.AddRange(orgs);
        }

        // 4. Evidence Search
        if (!request.EntityType.HasValue || request.EntityType == Domain.Enums.EntityType.Evidence)
        {
            var evQuery = _context.Evidence.AsNoTracking();
            if (!string.IsNullOrEmpty(term))
            {
                evQuery = evQuery.Where(e => e.EvidenceNumber.ToLower().Contains(term) ||
                                             e.Description.ToLower().Contains(term) ||
                                             e.Source.ToLower().Contains(term) ||
                                             e.Hash.ToLower().Contains(term));
            }

            var evidence = await evQuery
                .OrderByDescending(e => e.CreatedAtUtc)
                .Take(25)
                .Select(e => new SearchResultItemDto(
                    e.Id,
                    Domain.Enums.EntityType.Evidence,
                    e.EvidenceNumber + " - " + e.Type.ToString(),
                    e.Source,
                    e.Description,
                    e.Status.ToString(),
                    e.IsCritical ? "Critical" : null,
                    e.CreatedAtUtc))
                .ToListAsync(cancellationToken);

            results.AddRange(evidence);
        }

        var counts = results
            .GroupBy(r => r.EntityType.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var paginated = results
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new SearchSummaryDto(paginated, results.Count, counts);
    }
}
