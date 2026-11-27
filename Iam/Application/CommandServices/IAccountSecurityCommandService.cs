namespace FrioTrack.Api.Iam.Application.CommandServices;
public interface IAccountSecurityCommandService {
    bool RecoveryAvailable { get; }
    Task RequestReset(string email,CancellationToken cancellationToken);
    Task Reset(string token,string password,CancellationToken cancellationToken);
}
public interface IAccountAccessCommandService { Task Update(string authorization,string id,string role,bool enabled); }
