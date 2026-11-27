using System.Globalization;
using System.Text.Json.Nodes;
using FrioTrack.Api.Shared.Application;

namespace FrioTrack.Api.AlertsReporting.Application;

public sealed record RouteSummary(string Origin,string Destination,int Shipments,int Readings,decimal? MinimumTemperature,decimal? MaximumTemperature,decimal? AverageTemperature,int Alerts,int OpenAlerts);
public sealed record MonthlySummary(string Month,int Shipments,int InTransit,int Delivered,int Readings,int ThermalAlerts,int SignalAlerts,int OpenAlerts,RouteSummary[] Routes,DateTimeOffset GeneratedAt,string DataSource);
public sealed record AuditEntry(string ShipmentId,DateTimeOffset At,string Action,string Actor,string Note);

public sealed class OperationalReporting(RelationalWorkspaceService workspace) : QueryServices.IOperationalQueryService {
    public static readonly string Notice="Lecturas de demostración. La ausencia de lecturas o alertas no acredita cumplimiento de la cadena de frío.";
    static string S(JsonNode? value,string key)=>value?[key]?.ToString()??"";
    public static string ValidateMonth(string? month) {
        month??=DateTimeOffset.UtcNow.ToString("yyyy-MM",CultureInfo.InvariantCulture);
        if(!DateTime.TryParseExact(month+"-01","yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))throw new ApiError("invalidMonth");
        return month;
    }
    public static MonthlySummary Summarize(JsonObject state,string month) {
        month=ValidateMonth(month);
        var shipments=state["shipments"]!.AsArray().Where(x=>DateTimeOffset.Parse(S(x,"departure"),CultureInfo.InvariantCulture).UtcDateTime.ToString("yyyy-MM")==month).ToArray();
        var ids=shipments.Select(x=>S(x,"id")).ToHashSet();
        var alerts=state["alerts"]!.AsArray().Where(x=>ids.Contains(S(x,"shipmentId"))).ToArray();
        var routes=shipments.GroupBy(x=>(S(x,"origin"),S(x,"destination"))).Select(group=>{
            var groupIds=group.Select(x=>S(x,"id")).ToHashSet();var routeAlerts=alerts.Where(x=>groupIds.Contains(S(x,"shipmentId"))).ToArray();
            var readings=group.SelectMany(x=>x!["readings"]!.AsArray()).Select(x=>x!["temperature"]!.GetValue<decimal>()).ToArray();
            return new RouteSummary(group.Key.Item1,group.Key.Item2,group.Count(),readings.Length,readings.Length==0?null:readings.Min(),readings.Length==0?null:readings.Max(),readings.Length==0?null:decimal.Round(readings.Average(),2),routeAlerts.Length,routeAlerts.Count(x=>x!["resolved"]!.GetValue<bool>()==false));
        }).OrderByDescending(x=>x.Alerts).ThenBy(x=>x.Origin,StringComparer.Ordinal).ThenBy(x=>x.Destination,StringComparer.Ordinal).ToArray();
        return new MonthlySummary(month,shipments.Length,shipments.Count(x=>S(x,"status")=="in-transit"),shipments.Count(x=>S(x,"status")=="delivered"),routes.Sum(x=>x.Readings),alerts.Count(x=>S(x,"type")!="signal"),alerts.Count(x=>S(x,"type")=="signal"),alerts.Count(x=>x!["resolved"]!.GetValue<bool>()==false),routes,DateTimeOffset.UtcNow,"demonstration");
    }
    public async Task<MonthlySummary> Monthly(string authorization,string? month)=>Summarize((await workspace.Workspace(authorization))["state"]!.AsObject(),ValidateMonth(month));
    public async Task<AuditEntry[]> Audit(string authorization,string? month) {
        var state=(await workspace.Workspace(authorization))["state"]!.AsObject();month=ValidateMonth(month);
        var entries=state["shipments"]!.AsArray().SelectMany(s=>s!["history"]!.AsArray().Select(h=>new AuditEntry(S(s,"id"),DateTimeOffset.Parse(S(h,"at")),S(h,"action"),S(h,"actor"),S(h,"note"))))
            .Concat(state["alerts"]!.AsArray().SelectMany(a=>a!["actions"]!.AsArray().Select(h=>new AuditEntry(S(a,"shipmentId"),DateTimeOffset.Parse(S(h,"at")),"correctiveAction",S(h,"actor"),S(h,"note")))));
        return entries.Where(x=>x.At.UtcDateTime.ToString("yyyy-MM")==month).OrderByDescending(x=>x.At).ThenBy(x=>x.ShipmentId,StringComparer.Ordinal).ToArray();
    }
}
