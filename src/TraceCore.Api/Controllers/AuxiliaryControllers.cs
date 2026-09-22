using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TraceCore.Application.Audit;
using TraceCore.Application.Common.Interfaces;
using TraceCore.Application.Common.Models;
using TraceCore.Application.Notifications;
using TraceCore.Application.Reports;
using TraceCore.Application.Risk;
using TraceCore.Application.Search;

namespace TraceCore.Api.Controllers;

[Authorize]
[Route("api/v1/[controller]")]
public class RiskController : ApiControllerBase
{
    [HttpPost("cases/{caseId:guid}/assess")]
    public async Task<ActionResult<RiskAssessmentResult>> AssessCaseRisk(
        Guid caseId,
        [FromQuery] string triggerReason = "User Request",
        CancellationToken ct = default)
    {
        return Ok(await Sender.Send(new AssessCaseRiskCommand(caseId, triggerReason), ct));
    }

    [HttpGet("cases/{caseId:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<RiskHistoryDto>>> GetCaseRiskHistory(Guid caseId, CancellationToken ct)
    {
        return Ok(await Sender.Send(new GetCaseRiskHistoryQuery(caseId), ct));
    }
}

[Authorize]
[Route("api/v1/[controller]")]
public class SearchController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SearchSummaryDto>> Search([FromQuery] SearchCasesAndEntitiesQuery query, CancellationToken ct)
    {
        return Ok(await Sender.Send(query, ct));
    }
}

[Authorize]
[Route("api/v1/[controller]")]
public class NotificationsController : ApiControllerBase
{
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
    [HttpGet]
    public async Task<ActionResult<PagedList<AuditLogDto>>> GetAuditLogs([FromQuery] GetAuditLogsPagedQuery query, CancellationToken ct)
    {
        return Ok(await Sender.Send(query, ct));
    }
}

[Authorize]
[Route("api/v1/[controller]")]
public class ReportsController : ApiControllerBase
{
    [HttpGet("sla-compliance")]
    public async Task<ActionResult<SlaComplianceReportDto>> GetSlaCompliance(CancellationToken ct)
    {
        return Ok(await Sender.Send(new GetSlaComplianceReportQuery(), ct));
    }

    [HttpGet("risk-distribution")]
    public async Task<ActionResult<RiskDistributionReportDto>> GetRiskDistribution(CancellationToken ct)
    {
        return Ok(await Sender.Send(new GetRiskDistributionReportQuery(), ct));
    }

    [HttpGet("investigator-workload")]
    public async Task<ActionResult<InvestigatorWorkloadReportDto>> GetInvestigatorWorkload(CancellationToken ct)
    {
        return Ok(await Sender.Send(new GetInvestigatorWorkloadReportQuery(), ct));
    }
}
