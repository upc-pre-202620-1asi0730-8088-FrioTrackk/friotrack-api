using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace FrioTrack.Api.Shared.Application;

internal static class WorkspaceCommands {
    static string S(JsonNode? n, string key) => n?[key]?.ToString() ?? "";
    static double N(JsonNode? n, string key) => double.TryParse(S(n, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : throw new ApiError("requiredFields");
    static IEnumerable<JsonObject> Items(JsonObject state, string key) => state[key]!.AsArray().OfType<JsonObject>();
    static bool Active(JsonObject s) => S(s, "status") is "scheduled" or "in-transit";
    static string Now() => DateTimeOffset.UtcNow.ToString("O");
    static DateTimeOffset Date(JsonObject n, string key) => DateTimeOffset.TryParse(S(n, key), out var value) ? value : throw new ApiError("invalidDates");
    static void Require(bool condition, string error) { if (!condition) throw new ApiError(error); }
    static void Notify(JsonObject state, JsonObject shipment, string type, JsonArray? recipients = null) {
        var notice = new JsonObject { ["id"] = "notice-" + Guid.NewGuid(), ["shipmentId"] = S(shipment, "id"), ["type"] = type, ["readBy"] = new JsonArray(), ["at"] = Now() };
        if (recipients is not null) notice["recipientProfileIds"] = recipients;
        state["notifications"]!.AsArray().Insert(0, notice);
    }
    static void Audit(JsonObject shipment, JsonObject actor, string action, string note = "") {
        shipment["history"]!.AsArray().Add(new JsonObject { ["at"] = Now(), ["actor"] = S(actor, "name"), ["action"] = action, ["note"] = note });
        shipment["version"] = N(shipment, "version") + 1;
    }
    public static void ValidateProfile(JsonObject state, JsonObject p, string exclude) {
        Require(S(p, "name").Trim().Length is > 0 and <= 90 && S(p, "organization").Trim().Length is > 0 and <= 100 && S(p, "email").Length <= 140 && Regex.IsMatch(S(p, "email"), @"^[^\s@]+@[^\s@]+\.[^\s@]+$"), "invalidProfile");
        Require(!Items(state, "profiles").Any(i => S(i, "id") != exclude && S(i, "email").Equals(S(p, "email").Trim(), StringComparison.OrdinalIgnoreCase)), "duplicateProfile");
    }
    static JsonObject ShipmentDraft(JsonObject state, JsonObject p, string exclude = "") {
        foreach (var key in new[] { "cargo", "origin", "destination" }) Require(S(p, key).Trim().Length is > 0 and <= 150, "requiredFields");
        Require(!S(p, "origin").Trim().Equals(S(p, "destination").Trim(), StringComparison.OrdinalIgnoreCase), "sameRoute");
        Require(N(p, "weight") > 0, "invalidWeight");
        Require(N(p, "minTemp") >= -50 && N(p, "maxTemp") <= 50 && N(p, "minTemp") < N(p, "maxTemp"), "invalidRange");
        Require(N(p, "minHumidity") >= 0 && N(p, "maxHumidity") <= 100 && N(p, "minHumidity") < N(p, "maxHumidity"), "invalidHumidity");
        var start = Date(p, "departure"); var end = Date(p, "arrival"); Require(end > start, "invalidDates");
        var vehicle = Items(state, "vehicles").FirstOrDefault(i => S(i, "id") == S(p, "vehicleId") && S(i, "enabled") == "true");
        var driver = Items(state, "drivers").FirstOrDefault(i => S(i, "id") == S(p, "driverId") && S(i, "enabled") == "true");
        Require(vehicle is not null && driver is not null, "missingAssignment");
        Require(Items(state, "profiles").Any(i => S(i, "id") == S(p, "customerId") && S(i, "role") == "cargo-client"), "missingCustomer");
        Require(N(p, "weight") <= N(vehicle, "capacity"), "overCapacity");
        var overlapping = Items(state, "shipments").Where(i => S(i, "id") != exclude && Active(i) && start < Date(i, "arrival") && end > Date(i, "departure")).ToArray();
        Require(!overlapping.Any(i => S(i, "vehicleId") == S(p, "vehicleId")), "vehicleBusy");
        Require(!overlapping.Any(i => S(i, "driverId") == S(p, "driverId")), "driverBusy");
        var draft = new JsonObject();
        foreach (var key in new[] { "cargo", "origin", "destination", "customerId", "vehicleId", "driverId" }) draft[key] = S(p, key).Trim();
        foreach (var key in new[] { "weight", "minTemp", "maxTemp", "minHumidity", "maxHumidity" }) draft[key] = N(p, key);
        draft["departure"] = start.ToString("O"); draft["arrival"] = end.ToString("O");
        return draft;
    }
    static void Assign(JsonObject destination, JsonObject source) { foreach (var entry in source) destination[entry.Key] = entry.Value?.DeepClone(); }
    public static JsonNode? Apply(JsonObject state, JsonObject actor, string type, JsonObject p) {
        var manager = S(actor, "role") == "coordinator";
        if (!manager && type is not ("updateProfile" or "readNotifications")) throw new ApiError("forbidden", 403);
        JsonObject GetShipment() => Items(state, "shipments").FirstOrDefault(s => S(s, "id") == S(p, "id") && (manager || S(s, "customerId") == S(actor, "id"))) ?? throw new ApiError("notFound", 404);
        void Version(JsonObject shipment) => Require(N(p, "version") == N(shipment, "version"), "staleShipment");
        JsonNode? result = null;
        switch (type) {
            case "createShipment": {
                var draft = ShipmentDraft(state, p);
                Require(Date(draft, "departure") > DateTimeOffset.UtcNow, "futureDeparture");
                var sequence = Items(state, "shipments").Select(s => int.TryParse(S(s, "id").Split('-').Last(), out var id) ? id : 0).DefaultIfEmpty().Max() + 1;
                draft["id"] = $"FT-{sequence:0000}"; draft["version"] = 0; draft["status"] = "scheduled"; draft["readings"] = new JsonArray(); draft["offline"] = false; draft["history"] = new JsonArray();
                Audit(draft, actor, "created", "Shipment scheduled");
                state["shipments"]!.AsArray().Insert(0, draft); Notify(state, draft, "created"); result = draft; break;
            }
            case "updateShipment": {
                var shipment = GetShipment(); Version(shipment); Require(S(shipment, "status") == "scheduled", "editScheduledOnly");
                Assign(shipment, ShipmentDraft(state, p, S(shipment, "id"))); Audit(shipment, actor, "updated"); result = shipment; break;
            }
            case "transition": {
                var shipment = GetShipment(); Version(shipment); var status = S(p, "status");
                Require(S(shipment, "status") == "scheduled" && status is "in-transit" or "cancelled" || S(shipment, "status") == "in-transit" && status == "delivered", "invalidTransition");
                Require(status != "cancelled" || !string.IsNullOrWhiteSpace(S(p, "note")), "requiredAction");
                shipment["status"] = status; Audit(shipment, actor, status, S(p, "note")); Notify(state, shipment, status); result = shipment; break;
            }
            case "deleteShipment": {
                var shipment = GetShipment(); Version(shipment); Require(S(shipment, "status") == "scheduled" && shipment["readings"]!.AsArray().Count == 0, "deleteScheduledOnly");
                var id = S(shipment, "id");
                foreach (var key in new[] { "shipments", "notifications", "alerts" }) {
                    var array = state[key]!.AsArray(); for (var i = array.Count - 1; i >= 0; i--) if (S(array[i], key == "shipments" ? "id" : "shipmentId") == id) array.RemoveAt(i);
                }
                result = JsonValue.Create(id); break;
            }
            case "addReading": {
                var shipment = GetShipment(); Require(S(shipment, "status") == "in-transit", "readingsTransitOnly");
                Require(N(p, "temperature") is >= -50 and <= 50 && N(p, "humidity") is >= 0 and <= 100 && Math.Abs(N(p, "lat")) <= 90 && Math.Abs(N(p, "lng")) <= 180, "invalidReading");
                var reading = new JsonObject { ["at"] = Now() }; foreach (var key in new[] { "temperature", "humidity", "lat", "lng" }) reading[key] = N(p, key);
                shipment["readings"]!.AsArray().Add(reading); shipment["offline"] = false;
                var abnormal = false;
                foreach (var (key, min, max) in new[] { ("temperature", "minTemp", "maxTemp"), ("humidity", "minHumidity", "maxHumidity") }) {
                    if (N(reading, key) >= N(shipment, min) && N(reading, key) <= N(shipment, max)) continue;
                    abnormal = true;
                    state["alerts"]!.AsArray().Insert(0, new JsonObject { ["id"] = "alert-" + Guid.NewGuid(), ["shipmentId"] = S(shipment, "id"), ["type"] = key, ["reading"] = N(reading, key), ["at"] = S(reading, "at"), ["resolved"] = false, ["actions"] = new JsonArray() }); Notify(state, shipment, "alert");
                }
                if (!abnormal) foreach (var alert in Items(state, "alerts").Where(a => S(a, "shipmentId") == S(shipment, "id") && S(a, "resolved") != "true")) { alert["resolved"] = true; alert["resolvedAt"] = S(reading, "at"); }
                Audit(shipment, actor, "sampleReading"); result = reading; break;
            }
            case "recordIncident": {
                var shipment = GetShipment(); Require(S(p, "note").Trim().Length is >= 8 and <= 1000, "requiredAction"); Audit(shipment, actor, "incident", S(p, "note").Trim()); result = shipment; break;
            }
            case "recordCorrectiveAction": case "resolveAlert": {
                var alert = Items(state, "alerts").FirstOrDefault(a => S(a, "id") == S(p, "id")) ?? throw new ApiError("notFound", 404);
                Require(S(alert, "resolved") != "true", "alreadyResolved"); Require(S(p, "note").Trim().Length is >= 8 and <= 1000, "requiredAction");
                var shipment = Items(state, "shipments").First(s => S(s, "id") == S(alert, "shipmentId"));
                alert["actions"]!.AsArray().Add(new JsonObject { ["note"] = S(p, "note").Trim(), ["at"] = Now(), ["actor"] = S(actor, "name") }); alert["acknowledged"] = true;
                if (S(p, "notifyClient") == "true") Notify(state, shipment, "correctiveAction", new JsonArray(S(shipment, "customerId")));
                Audit(shipment, actor, "correctiveAction", S(p, "note").Trim()); result = alert; break;
            }
            case "saveVehicle": case "saveDriver": {
                var vehicle = type == "saveVehicle"; var key = vehicle ? "vehicles" : "drivers"; var id = S(p, "id");
                var existing = Items(state, key).FirstOrDefault(i => S(i, "id") == id);
                if (id.Length > 0 && existing is null) throw new ApiError("notFound", 404);
                Require(existing is null || !Items(state, "shipments").Any(s => Active(s) && S(s, vehicle ? "vehicleId" : "driverId") == id), "assignedResource");
                var resource = new JsonObject { ["id"] = existing is null ? (vehicle ? "vehicle-" : "driver-") + Guid.NewGuid() : id, ["enabled"] = S(p, "enabled") != "false" };
                if (vehicle) {
                    Require(Regex.IsMatch(S(p, "plate").Trim(), "^[A-Za-z0-9]{3}-[A-Za-z0-9]{3}$") && !string.IsNullOrWhiteSpace(S(p, "sensor")), "invalidVehicle");
                    Require(N(p, "capacity") is > 0 and <= 100, "invalidWeight");
                    Require(!Items(state, key).Any(i => S(i, "id") != id && (S(i, "plate").Equals(S(p, "plate").Trim(), StringComparison.OrdinalIgnoreCase) || S(i, "sensor").Equals(S(p, "sensor").Trim(), StringComparison.OrdinalIgnoreCase))), "duplicateVehicle");
                    resource["plate"] = S(p, "plate").Trim().ToUpperInvariant(); resource["sensor"] = S(p, "sensor").Trim().ToUpperInvariant(); resource["capacity"] = N(p, "capacity");
                } else {
                    Require(!string.IsNullOrWhiteSpace(S(p, "name")) && !string.IsNullOrWhiteSpace(S(p, "license")), "requiredFields");
                    Require(!Items(state, key).Any(i => S(i, "id") != id && S(i, "license").Equals(S(p, "license").Trim(), StringComparison.OrdinalIgnoreCase)), "duplicateDriver");
                    resource["name"] = S(p, "name").Trim(); resource["license"] = S(p, "license").Trim();
                }
                if (existing is null) state[key]!.AsArray().Add(resource); else Assign(existing, resource); result = resource; break;
            }
            case "updateProfile": {
                var id = S(actor, "id"); ValidateProfile(state, p, id);
                var account = Items(state, "profiles").First(i => S(i, "id") == id);
                account["name"] = S(p, "name").Trim(); account["organization"] = S(p, "organization").Trim(); account["email"] = S(p, "email").Trim().ToLowerInvariant(); result = account; break;
            }
            case "readNotifications": {
                foreach (var notice in Items(state, "notifications")) {
                    var shipment = Items(state, "shipments").FirstOrDefault(s => S(s, "id") == S(notice, "shipmentId"));
                    var readers = notice["readBy"]!.AsArray();
                    if (shipment is not null && (manager || S(shipment, "customerId") == S(actor, "id")) && (notice["recipientProfileIds"] is not JsonArray recipients || recipients.Any(r => r?.ToString() == S(actor, "id"))) && !readers.Any(r => r?.ToString() == S(actor, "id"))) readers.Add(S(actor, "id"));
                }
                break;
            }
            default: throw new ApiError("unknownCommand");
        }
        state["revision"] = N(state, "revision") + 1;
        return result;
    }
}
