using System.Globalization;
using System.Text.Json.Nodes;
using FrioTrack.Api.Shared.Application;
using FrioTrack.Api.ShipmentManagement.Application.QueryServices;
namespace FrioTrack.Api.ShipmentManagement.Application;
public sealed class ShipmentQueries(RelationalWorkspaceService workspace):IShipmentQueryService {
    static string S(JsonNode? node,string key)=>node?[key]?.ToString()??"";
    public async Task<JsonArray> List(string authorization,string? q,string? status,string? vehicleId,bool? active,DateTimeOffset? from,DateTimeOffset? to) {
        var state=(await workspace.Workspace(authorization))["state"]!.AsObject();
        if(q?.Length>200||status is not (null or "" or "scheduled" or "in-transit" or "delivered" or "cancelled"))throw new ApiError("requiredFields");
        if(from is not null&&to is not null&&from>=to)throw new ApiError("invalidDates");
        var vehicles=state["vehicles"]!.AsArray().ToDictionary(x=>S(x,"id"),x=>S(x,"plate"));
        var items=state["shipments"]!.AsArray().Where(x=>{
            var departure=DateTimeOffset.Parse(S(x,"departure"),CultureInfo.InvariantCulture);
            var isActive=S(x,"status") is "scheduled" or "in-transit";
            var text=string.Join(" ",S(x,"id"),S(x,"cargo"),S(x,"origin"),S(x,"destination"),vehicles.GetValueOrDefault(S(x,"vehicleId"),""));
            return (string.IsNullOrWhiteSpace(q)||text.Contains(q.Trim(),StringComparison.OrdinalIgnoreCase))
                &&(string.IsNullOrEmpty(status)||S(x,"status")==status)
                &&(string.IsNullOrEmpty(vehicleId)||S(x,"vehicleId")==vehicleId)
                &&(active is null||isActive==active)
                &&(from is null||departure>=from)&&(to is null||departure<to);
        });
        return new JsonArray(items.Select(x=>x!.DeepClone()).ToArray());
    }
}
