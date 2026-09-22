using MediatR;
using Microsoft.EntityFrameworkCore;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Application.Common.Models;

namespace TraceCore.Application.Audit;

public sealed record AuditLogDto(
    Guid Id,
    Guid? UserId,
    string Action,
    string EntityType,
    string EntityId,
    DateTime TimestampUtc,
    string? IpAddress,
    string? CorrelationId,
    string? BeforeJson,
    string? AfterJson);

public sealed class GetAuditLogsPagedQuery : PagedRequest, IRequest<PagedList<AuditLogDto>>
{
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public Guid? UserId { get; set; }
    public string? Action { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public class GetAuditLogsPagedQueryHandler : IRequestHandler<GetAuditLogsPagedQuery, PagedList<AuditLogDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAuditLogsPagedQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedList<AuditLogDto>> Handle(GetAuditLogsPagedQuery request, CancellationToken cancellationToken)
    {
        var query = _context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.EntityType))
            query = query.Where(a => a.EntityType == request.EntityType);
        if (!string.IsNullOrWhiteSpace(request.EntityId))
            query = query.Where(a => a.EntityId == request.EntityId);
        if (request.UserId.HasValue)
            query = query.Where(a => a.UserId == request.UserId.Value);
        if (!string.IsNullOrWhiteSpace(request.Action))
            query = query.Where(a => a.Action == request.Action);
        if (request.FromUtc.HasValue)
            query = query.Where(a => a.TimestampUtc >= request.FromUtc.Value);
        if (request.ToUtc.HasValue)
            query = query.Where(a => a.TimestampUtc <= request.ToUtc.Value);

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.TimestampUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new AuditLogDto(
                a.Id,
                a.UserId,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.TimestampUtc,
                a.IpAddress,
                a.CorrelationId,
                a.BeforeJson,
                a.AfterJson))
            .ToListAsync(cancellationToken);

        return new PagedList<AuditLogDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
