using FrioTrack.Api.AlertsReporting.Application.QueryServices;
using FrioTrack.Api.AlertsReporting.Application.Ports;
using FrioTrack.Api.Shared.Application;
using Microsoft.AspNetCore.Mvc;
namespace FrioTrack.Api.AlertsReporting.Interfaces.Rest;
[ApiController,Route("api/reports")]
public sealed class ReportsController(IOperationalQueryService queries,RelationalWorkspaceService workspace,IReportDocumentRenderer renderer):ControllerBase {
    [HttpGet("monthly/pdf"),Produces("application/pdf"),ProducesResponseType(200),ProducesResponseType(400),ProducesResponseType(401)]
    public async Task<IActionResult> Monthly([FromQuery]string? month){var summary=await queries.Monthly(Request.Headers.Authorization.ToString(),month);return File(renderer.Monthly(summary),"application/pdf","friotrack-monthly-"+summary.Month+".pdf");}
    [HttpGet("{id}/pdf"),Produces("application/pdf"),ProducesResponseType(200),ProducesResponseType(401),ProducesResponseType(404)]
    public async Task<IActionResult> Thermal(string id)=>File(renderer.Shipment(await workspace.ReadReport(Request.Headers.Authorization.ToString(),id)),"application/pdf","friotrack-thermal-report.pdf");
}
