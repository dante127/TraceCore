using TraceCore.Application.Common.Interfaces;
using TraceCore.Domain.Entities.Cases;
using TraceCore.Domain.Enums;

namespace TraceCore.Application.Common.Models;

internal static class CaseAccessQuery
{
    internal static IQueryable<Case> WhereReadableBy(
        this IQueryable<Case> query,
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        if (currentUser.IsInRole("Administrator"))
            return query;

        var userId = currentUser.UserId;
        if (!userId.HasValue)
            return query.Where(c => false);

        var uid = userId.Value;
        return query.Where(c =>
            c.AssignedInvestigatorId == uid ||
            c.ConfidentialityLevel <= ConfidentialityLevel.Internal ||
            context.CaseAccessGrants.Any(g => g.CaseId == c.Id && g.UserId == uid));
    }
}
