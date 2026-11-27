using FrioTrack.Api.Iam.Application.CommandServices;
using FrioTrack.Api.Iam.Application.QueryServices;
using Microsoft.AspNetCore.Mvc;
namespace FrioTrack.Api.Iam.Interfaces.Rest;
public sealed record AccountAccessResource(string Role,bool Enabled);
[ApiController,Route("api/accounts")]
public sealed class OrganizationAccountsController(IAccountAccessCommandService commands,IAccountAccessQueryService queries):ControllerBase {
    [HttpGet,ProducesResponseType(200),ProducesResponseType(401),ProducesResponseType(403)]
    public async Task<IActionResult> List()=>Ok(await queries.List(Request.Headers.Authorization.ToString()));
    [HttpPut("{id}/access"),ProducesResponseType(204),ProducesResponseType(400),ProducesResponseType(401),ProducesResponseType(403),ProducesResponseType(404),ProducesResponseType(409)]
    public async Task<IActionResult> Update(string id,AccountAccessResource resource){await commands.Update(Request.Headers.Authorization.ToString(),id,resource.Role,resource.Enabled);return NoContent();}
}
