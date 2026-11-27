using FrioTrack.Api.AlertsReporting.Application;
using FrioTrack.Api.AlertsReporting.Application.QueryServices;
using Microsoft.AspNetCore.Mvc;
namespace FrioTrack.Api.AlertsReporting.Interfaces.Rest;
[ApiController,Route("api")]
public sealed class OperationalAnalyticsController(IOperationalQueryService queries):ControllerBase {
    [HttpGet("analytics/monthly"),ProducesResponseType(typeof(MonthlySummary),200),ProducesResponseType(400),ProducesResponseType(401)]
    public Task<MonthlySummary> Monthly([FromQuery]string? month)=>queries.Monthly(Request.Headers.Authorization.ToString(),month);
    [HttpGet("analytics/quarterly"),ProducesResponseType(typeof(QuarterlySummary),200),ProducesResponseType(400),ProducesResponseType(401)]
    public Task<QuarterlySummary> Quarterly([FromQuery]int year,[FromQuery]int quarter)=>queries.Quarterly(Request.Headers.Authorization.ToString(),year,quarter);
    [HttpGet("audit"),ProducesResponseType(typeof(AuditEntry[]),200),ProducesResponseType(400),ProducesResponseType(401)]
    public Task<AuditEntry[]> Audit([FromQuery]string? month,[FromQuery]DateTimeOffset? from,[FromQuery]DateTimeOffset? to,[FromQuery]string? actor)=>queries.Audit(Request.Headers.Authorization.ToString(),month,from,to,actor);
}
