using System.ComponentModel.DataAnnotations;
using FrioTrack.Api.Iam.Application.CommandServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace FrioTrack.Api.Iam.Interfaces.Rest;
public sealed record ForgotPasswordResource([Required,EmailAddress,MaxLength(140)]string Email);
public sealed record ResetPasswordResource([Required,MinLength(64),MaxLength(64)]string Token,[Required,MinLength(12),MaxLength(128)]string Password);
[ApiController,Route("api/auth"),AllowAnonymous,EnableRateLimiting("auth")]
public sealed class AccountRecoveryController(IAccountSecurityCommandService commands):ControllerBase {
    [HttpPost("forgot-password"),ProducesResponseType(202),ProducesResponseType(400),ProducesResponseType(503),ProducesResponseType(429)]
    public async Task<IActionResult> RequestRecovery(ForgotPasswordResource resource,CancellationToken cancellationToken){await commands.RequestReset(resource.Email,cancellationToken);return Accepted(new{message="If an enabled account exists, a recovery message will be sent."});}
    [HttpPost("reset-password"),ProducesResponseType(204),ProducesResponseType(400),ProducesResponseType(429)]
    public async Task<IActionResult> Reset(ResetPasswordResource resource,CancellationToken cancellationToken){await commands.Reset(resource.Token,resource.Password,cancellationToken);return NoContent();}
}
