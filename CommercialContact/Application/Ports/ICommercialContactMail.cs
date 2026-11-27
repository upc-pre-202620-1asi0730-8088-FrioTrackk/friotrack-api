namespace FrioTrack.Api.CommercialContact.Application.Ports;
public sealed record ContactDetails(string Name,string Company,string Email,string Phone,string Role);
public interface ICommercialContactMail {
    bool IsConfigured {get;}
    Task Send(ContactDetails details,CancellationToken cancellationToken);
}
