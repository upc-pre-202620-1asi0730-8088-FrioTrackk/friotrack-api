using FrioTrack.Api.Shared.Domain;

namespace FrioTrack.Api.ShipmentManagement.Domain;

public sealed record ThermalRange {
    public decimal MinTemp { get; private set; }
    public decimal MaxTemp { get; private set; }
    public decimal MinHumidity { get; private set; }
    public decimal MaxHumidity { get; private set; }
    public int ToleranceMinutes { get; private set; }
    private ThermalRange() { }
    public ThermalRange(decimal minTemp, decimal maxTemp, decimal minHumidity, decimal maxHumidity, int toleranceMinutes = 0) {
        Rules.Require(minTemp >= -50 && maxTemp <= 50 && minTemp < maxTemp, "invalidRange");
        Rules.Require(minHumidity >= 0 && maxHumidity <= 100 && minHumidity < maxHumidity, "invalidHumidity");
        Rules.Require(toleranceMinutes >= 0 && toleranceMinutes <= 1440, "invalidTolerance");
        MinTemp = minTemp; MaxTemp = maxTemp; MinHumidity = minHumidity; MaxHumidity = maxHumidity; ToleranceMinutes = toleranceMinutes;
    }
    public bool Contains(decimal temperature, decimal humidity) => temperature >= MinTemp && temperature <= MaxTemp && humidity >= MinHumidity && humidity <= MaxHumidity;
}

public sealed class Shipment {
    public string Id { get; private set; } = "";
    public string OrganizationId { get; private set; } = "";
    public string CoordinatorId { get; private set; } = "";
    public string CustomerId { get; private set; } = "";
    public string VehicleId { get; private set; } = "";
    public string DriverId { get; private set; } = "";
    public string Cargo { get; private set; } = "";
    public decimal Weight { get; private set; }
    public string Origin { get; private set; } = "";
    public string Destination { get; private set; } = "";
    public DateTimeOffset Departure { get; private set; }
    public DateTimeOffset Arrival { get; private set; }
    public string Status { get; private set; } = "scheduled";
    public long Version { get; private set; } = 1;
    public ThermalRange Range { get; private set; } = null!;
    public List<ShipmentHistoryEvent> History { get; private set; } = [];
    private Shipment() { }
    public static Shipment Schedule(string id, string organizationId, string coordinatorId, string customerId, string vehicleId, string driverId, string cargo, decimal weight, string origin, string destination, DateTimeOffset departure, DateTimeOffset arrival, ThermalRange range, bool reservationConfirmed, DateTimeOffset now) {
        Rules.Require(reservationConfirmed, "missingAssignment");
        Rules.Require(departure > now, "futureDeparture");
        var shipment = new Shipment { Id = id, OrganizationId = organizationId, CoordinatorId = coordinatorId };
        shipment.SetPlan(customerId, vehicleId, driverId, cargo, weight, origin, destination, departure, arrival, range);
        shipment.Audit(coordinatorId, "created", "Shipment scheduled", now, false); return shipment;
    }
    void SetPlan(string customerId, string vehicleId, string driverId, string cargo, decimal weight, string origin, string destination, DateTimeOffset departure, DateTimeOffset arrival, ThermalRange range) {
        Rules.Require(weight > 0, "invalidWeight"); Rules.Require(arrival > departure, "invalidDates");
        Cargo = Rules.Text(cargo, 150); Origin = Rules.Text(origin, 150); Destination = Rules.Text(destination, 150);
        Rules.Require(!Origin.Equals(Destination, StringComparison.OrdinalIgnoreCase), "sameRoute");
        CustomerId = customerId; VehicleId = vehicleId; DriverId = driverId; Weight = weight; Departure = departure.ToUniversalTime(); Arrival = arrival.ToUniversalTime(); Range = range;
    }
    public void Update(long version, string customerId, string vehicleId, string driverId, string cargo, decimal weight, string origin, string destination, DateTimeOffset departure, DateTimeOffset arrival, ThermalRange range, string actorId, DateTimeOffset now) {
        Rules.Version(Version, version); Rules.Require(Status == "scheduled", "editScheduledOnly");
        SetPlan(customerId, vehicleId, driverId, cargo, weight, origin, destination, departure, arrival, range); Audit(actorId, "updated", "", now);
    }
    public void Transition(long version, string next, string note, string actorId, DateTimeOffset now) {
        Rules.Version(Version, version);
        Rules.Require(Status == "scheduled" && next is "in-transit" or "cancelled" || Status == "in-transit" && next == "delivered", "invalidTransition");
        if (next == "cancelled") note = Rules.Text(note, 1000, "requiredAction");
        Status = next; Audit(actorId, next, note, now);
    }
    public void CheckDeletion(long version, bool hasReadings) { Rules.Version(Version, version); Rules.Require(Status == "scheduled" && !hasReadings, "deleteScheduledOnly"); }
    public void Audit(string actorId, string action, string note, DateTimeOffset at, bool increment = true) {
        History.Add(new ShipmentHistoryEvent { Id = Guid.NewGuid().ToString(), ShipmentId = Id, ActorId = actorId, Action = action, Note = note, At = at.ToUniversalTime() });
        if (increment) Version++;
    }
    internal void ImportHistory(string status, long version, IEnumerable<ShipmentHistoryEvent> history) { Status = status; Version = Math.Max(1, version); History = history.ToList(); }
}
public sealed class ShipmentHistoryEvent {
    public string Id { get; set; } = "";
    public string ShipmentId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string Action { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTimeOffset At { get; set; }
}
