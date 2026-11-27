namespace FrioTrack.Api.AlertsReporting.Application.QueryServices;
public interface IOperationalQueryService {
    Task<MonthlySummary> Monthly(string authorization,string? month);
    Task<AuditEntry[]> Audit(string authorization,string? month);
}
