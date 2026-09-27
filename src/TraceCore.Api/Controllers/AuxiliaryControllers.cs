using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TraceCore.Api.Common;
using TraceCore.Api.Services;
using TraceCore.Application.Audit;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Application.Common.Models;
using TraceCore.Application.Notifications;
using TraceCore.Application.Reports;
using TraceCore.Application.Risk;
using TraceCore.Application.Search;
using TraceCore.Domain.Enums;
using TraceCore.Infrastructure.Security;

namespace TraceCore.Api.Controllers;

[Authorize]
[Route("api/v1/[controller]")]
public class RiskController : ApiControllerBase
{
    private readonly ICaseAuthorizationService _caseAuth;

    public RiskController(ISender sender, ICaseAuthorizationService caseAuth)
        : base(sender)
    {
        _caseAuth = caseAuth;
    }

    [HttpPost("cases/{caseId:guid}/assess")]
    [HasPermission(Permissions.CaseUpdate)]
    public async Task<ActionResult<RiskAssessmentResult>> AssessCaseRisk(
        Guid caseId,
        [FromQuery] string triggerReason = "User Request",
        CancellationToken ct = default)
    {
        if (!await _caseAuth.HasCaseAccessAsync(caseId, CaseAccessLevel.ReadWrite, ct))
        {
            return Forbid();
        }

        return Ok(await Sender.Send(new AssessCaseRiskCommand(caseId, triggerReason), ct));
    }

    [HttpGet("cases/{caseId:guid}/history")]
    [HasPermission(Permissions.CaseRead)]
    public async Task<ActionResult<IReadOnlyList<RiskHistoryDto>>> GetCaseRiskHistory(Guid caseId, CancellationToken ct)
    {
        if (!await _caseAuth.HasCaseAccessAsync(caseId, CaseAccessLevel.Read, ct))
        {
            return Forbid();
        }

        return Ok(await Sender.Send(new GetCaseRiskHistoryQuery(caseId), ct));
    }
}

[Authorize]
[Route("api/v1/[controller]")]
public class SearchController : ApiControllerBase
{
    public SearchController(ISender sender)
        : base(sender)
    {
    }

    [HttpGet]
    [HasPermission(Permissions.CaseRead)]
    public async Task<ActionResult<SearchSummaryDto>> Search([FromQuery] SearchCasesAndEntitiesQuery query, CancellationToken ct)
    {
        return Ok(await Sender.Send(query, ct));
    }
}

[Authorize]
[Route("api/v1/[controller]")]
public class NotificationsController : ApiControllerBase
{
    public NotificationsController(ISender sender)
        : base(sender)
    {
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetNotifications([FromQuery] bool? unreadOnly, CancellationToken ct)
    {
        return Ok(await Sender.Send(new GetUserNotificationsQuery(unreadOnly), ct));
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        await Sender.Send(new MarkNotificationAsReadCommand(id), ct);
        return NoContent();
    }
}

[Authorize]
[Route("api/v1/[controller]")]
public class AuditController : ApiControllerBase
{
    public AuditController(ISender sender)
        : base(sender)
    {
    }

    [HttpGet]
    [HasPermission(Permissions.AuditRead)]
    public async Task<ActionResult<PagedList<AuditLogDto>>> GetAuditLogs([FromQuery] GetAuditLogsPagedQuery query, CancellationToken ct)
    {
        return Ok(await Sender.Send(query, ct));
    }
}

[Authorize]
[Route("api/v1/[controller]")]
public class ReportsController : ApiControllerBase
{
    public ReportsController(ISender sender)
        : base(sender)
    {
    }

    [HttpGet("sla-compliance")]
    [HasPermission(Permissions.ReportsRead)]
    public async Task<ActionResult<SlaComplianceReportDto>> GetSlaCompliance(CancellationToken ct)
    {
        return Ok(await Sender.Send(new GetSlaComplianceReportQuery(), ct));
    }

    [HttpGet("risk-distribution")]
    [HasPermission(Permissions.ReportsRead)]
    public async Task<ActionResult<RiskDistributionReportDto>> GetRiskDistribution(CancellationToken ct)
    {
        return Ok(await Sender.Send(new GetRiskDistributionReportQuery(), ct));
    }

    [HttpGet("investigator-workload")]
    [HasPermission(Permissions.ReportsRead)]
    public async Task<ActionResult<InvestigatorWorkloadReportDto>> GetInvestigatorWorkload(CancellationToken ct)
    {
        return Ok(await Sender.Send(new GetInvestigatorWorkloadReportQuery(), ct));
    }
}
