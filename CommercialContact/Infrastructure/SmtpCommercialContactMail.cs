using System.Net;
using System.Net.Mail;
using FrioTrack.Api.CommercialContact.Application.Ports;
namespace FrioTrack.Api.CommercialContact.Infrastructure;
public sealed class SmtpCommercialContactMail(IConfiguration configuration):ICommercialContactMail {
    string Host=>configuration["FRIOTRACK_MAIL_HOST"]??"";
    string From=>configuration["FRIOTRACK_MAIL_FROM"]??"";
    string Recipient=>configuration["FRIOTRACK_CONTACT_TO"]??"";
    bool Tls=>configuration["FRIOTRACK_MAIL_TLS"]!="false";
    public bool IsConfigured=>!string.IsNullOrWhiteSpace(Host)&&MailAddress.TryCreate(From,out _)&&MailAddress.TryCreate(Recipient,out _)&&(Tls||Host is "localhost" or "127.0.0.1" or "::1");
    public async Task Send(ContactDetails details,CancellationToken cancellationToken) {
        if(!IsConfigured)throw new InvalidOperationException("Commercial SMTP is not configured.");
        using var client=new SmtpClient(Host,int.TryParse(configuration["FRIOTRACK_MAIL_PORT"],out var port)?port:587){EnableSsl=Tls,Timeout=15000};
        if(!string.IsNullOrEmpty(configuration["FRIOTRACK_MAIL_USERNAME"]))client.Credentials=new NetworkCredential(configuration["FRIOTRACK_MAIL_USERNAME"],configuration["FRIOTRACK_MAIL_PASSWORD"]);
        using var message=new MailMessage(From,Recipient){Subject="FríoTrack — Solicitud de contacto",Body=$"Nombre: {details.Name}\nEmpresa: {details.Company}\nCorreo: {details.Email}\nTeléfono: {details.Phone}\nPerfil: {details.Role}",IsBodyHtml=false,BodyEncoding=System.Text.Encoding.UTF8,SubjectEncoding=System.Text.Encoding.UTF8};
        message.ReplyToList.Add(new MailAddress(details.Email));await client.SendMailAsync(message,cancellationToken);
    }
}
