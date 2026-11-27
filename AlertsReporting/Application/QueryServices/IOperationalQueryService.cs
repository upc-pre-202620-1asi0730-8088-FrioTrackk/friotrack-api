namespace FrioTrack.Api.AlertsReporting.Application.QueryServices;
public interface IOperationalQueryService {
    Task<MonthlySummary> Monthly(string authorization,string? month);
    Task<QuarterlySummary> Quarterly(string authorization,int year,int quarter);
    Task<AuditEntry[]> Audit(string authorization,string? month,DateTimeOffset? from=null,DateTimeOffset? to=null,string? actor=null);
}
