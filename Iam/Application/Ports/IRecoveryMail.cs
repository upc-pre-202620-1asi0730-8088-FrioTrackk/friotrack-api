namespace FrioTrack.Api.Iam.Application.Ports;

public interface IRecoveryMail {
    bool IsConfigured { get; }
    Task Send(string recipient, string resetUrl, CancellationToken cancellationToken);
}
