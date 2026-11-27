using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using FrioTrack.Api.CommercialContact.Application.Ports;
using FrioTrack.Api.Shared.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace FrioTrack.Api.CommercialContact.Interfaces.Rest;
public sealed record CommercialContactResource(
    [Required,MaxLength(140)]string Name,[Required,MaxLength(180)]string Company,
    [Required,EmailAddress,MaxLength(140)]string Email,[Required,MaxLength(32)]string Phone,
    [Required,RegularExpression("^(carrier|producer|buyer|other)$")]string Role);
[ApiController,Route("api/commercial-contacts"),AllowAnonymous,EnableRateLimiting("contact")]
public sealed class CommercialContactController(ICommercialContactMail mail,ILogger<CommercialContactController> logger):ControllerBase {
    [HttpPost,ProducesResponseType(202),ProducesResponseType(400),ProducesResponseType(429),ProducesResponseType(503)]
    public async Task<IActionResult> Submit(CommercialContactResource resource,CancellationToken cancellationToken) {
        if(!mail.IsConfigured)throw new ApiError("contactUnavailable",503);
        if(new[]{resource.Name,resource.Company,resource.Email,resource.Phone}.Any(x=>string.IsNullOrWhiteSpace(x)||x.Contains('\r')||x.Contains('\n')))throw new ApiError("requiredFields");
        try{await mail.Send(new ContactDetails(resource.Name.Trim(),resource.Company.Trim(),resource.Email.Trim(),resource.Phone.Trim(),resource.Role),cancellationToken);}
        catch(SmtpException error){logger.LogWarning("Commercial SMTP delivery failed: {Type}",error.GetType().Name);throw new ApiError("contactUnavailable",503);}
        return Accepted(new{message="The contact request was delivered to the configured team mailbox."});
    }
}
