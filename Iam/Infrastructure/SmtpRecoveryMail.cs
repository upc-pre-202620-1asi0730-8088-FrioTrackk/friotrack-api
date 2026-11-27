using System.Net;
using System.Net.Mail;
using FrioTrack.Api.Iam.Application.Ports;

namespace FrioTrack.Api.Iam.Infrastructure;

public sealed class SmtpRecoveryMail(IConfiguration configuration) : IRecoveryMail {
    string Host => configuration["FRIOTRACK_MAIL_HOST"] ?? "";
    string From => configuration["FRIOTRACK_MAIL_FROM"] ?? "";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && MailAddress.TryCreate(From,out _);
    public async Task Send(string recipient,string resetUrl,CancellationToken cancellationToken) {
        var tls=configuration["FRIOTRACK_MAIL_TLS"]!="false";
        if(!tls && Host is not ("localhost" or "127.0.0.1" or "::1"))throw new InvalidOperationException("SMTP TLS is required outside the loopback test server.");
        using var client=new SmtpClient(Host,int.TryParse(configuration["FRIOTRACK_MAIL_PORT"],out var port)?port:587){EnableSsl=tls,Timeout=15000};
        if(!string.IsNullOrEmpty(configuration["FRIOTRACK_MAIL_USERNAME"]))client.Credentials=new NetworkCredential(configuration["FRIOTRACK_MAIL_USERNAME"],configuration["FRIOTRACK_MAIL_PASSWORD"]);
        using var message=new MailMessage(From,recipient){Subject="FríoTrack — Recuperación de acceso",Body="Se solicitó restablecer tu contraseña de FríoTrack. El enlace es de un solo uso y vence en 15 minutos:\n\n"+resetUrl+"\n\nSi no lo solicitaste, ignora este mensaje.",IsBodyHtml=false,BodyEncoding=System.Text.Encoding.UTF8,SubjectEncoding=System.Text.Encoding.UTF8};
        await client.SendMailAsync(message,cancellationToken);
    }
}
