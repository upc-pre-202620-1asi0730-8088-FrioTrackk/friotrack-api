using System.Text.Json.Nodes;
namespace FrioTrack.Api.ShipmentManagement.Application.QueryServices;
public interface IShipmentQueryService {
    Task<JsonArray> List(string authorization,string? q,string? status,string? vehicleId,bool? active,DateTimeOffset? from,DateTimeOffset? to);
}
