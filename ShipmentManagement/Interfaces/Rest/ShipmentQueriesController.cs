using System.Text.Json.Nodes;
using FrioTrack.Api.ShipmentManagement.Application.QueryServices;
using Microsoft.AspNetCore.Mvc;
namespace FrioTrack.Api.ShipmentManagement.Interfaces.Rest;
[ApiController,Route("api/shipments")]
public sealed class ShipmentQueriesController(IShipmentQueryService queries):ControllerBase {
    [HttpGet,ProducesResponseType(typeof(JsonArray),200),ProducesResponseType(400),ProducesResponseType(401)]
    public Task<JsonArray> List([FromQuery]string? q,[FromQuery]string? status,[FromQuery]string? vehicleId,[FromQuery]bool? active,[FromQuery]DateTimeOffset? from,[FromQuery]DateTimeOffset? to)=>queries.List(Request.Headers.Authorization.ToString(),q,status,vehicleId,active,from,to);
}
