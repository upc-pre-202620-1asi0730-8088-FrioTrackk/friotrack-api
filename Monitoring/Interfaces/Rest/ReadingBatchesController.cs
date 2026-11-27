using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using FrioTrack.Api.Interfaces;
using FrioTrack.Api.Shared.Application;
using Microsoft.AspNetCore.Mvc;
namespace FrioTrack.Api.Monitoring.Interfaces.Rest;
public sealed record ReadingBatchResource([Required,MinLength(1),MaxLength(100)]ReadingRequest[] Readings);
[ApiController,Route("api/shipments/{id}/readings/batch")]
public sealed class ReadingBatchesController(RelationalWorkspaceService commands):ControllerBase {
    [HttpPost,ProducesResponseType(typeof(MutationResponse),200),ProducesResponseType(400),ProducesResponseType(401),ProducesResponseType(403)]
    public Task<JsonObject> Record(string id,ReadingBatchResource resource)=>commands.Command(Request.Headers.Authorization.ToString(),"addReadings",new JsonObject{["id"]=id,["readings"]=JsonSerializer.SerializeToNode(resource.Readings,RelationalWorkspaceService.Json)});
}
