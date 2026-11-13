using FrioTrack.Api.Shared.Domain;
using System.Text.RegularExpressions;
namespace FrioTrack.Api.Fleet.Domain;

public sealed class Vehicle {
    public string Id { get; private set; } = "";
    public string OrganizationId { get; private set; } = "";
    public string Plate { get; private set; } = "";
    public decimal Capacity { get; private set; }
    public bool Enabled { get; private set; }
    private Vehicle() { }
    public Vehicle(string id, string organizationId, string plate, decimal capacity, bool enabled) { Id = id; OrganizationId = organizationId; Update(plate, capacity, enabled); }
    public void Update(string plate, decimal capacity, bool enabled) {
        Rules.Require(Regex.IsMatch(plate.Trim(), "^[A-Za-z0-9]{3}-[A-Za-z0-9]{3}$"), "invalidVehicle");
        Rules.Require(capacity > 0 && capacity <= 100, "invalidWeight"); Plate = plate.Trim().ToUpperInvariant(); Capacity = capacity; Enabled = enabled;
    }
}
public sealed class Driver {
    public string Id { get; private set; } = "";
    public string OrganizationId { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string License { get; private set; } = "";
    public bool Enabled { get; private set; }
    private Driver() { }
    public Driver(string id, string organizationId, string name, string license, bool enabled) { Id = id; OrganizationId = organizationId; Update(name, license, enabled); }
    public void Update(string name, string license, bool enabled) { Name = Rules.Text(name, 100); License = Rules.Text(license, 40).ToUpperInvariant(); Enabled = enabled; }
}
public sealed class Sensor {
    public string Id { get; private set; } = "";
    public string OrganizationId { get; private set; } = "";
    public string SerialCode { get; private set; } = "";
    public string? VehicleId { get; private set; }
    public bool Enabled { get; private set; }
    private Sensor() { }
    public Sensor(string id, string organizationId, string serialCode, string? vehicleId, bool enabled) { Id = id; OrganizationId = organizationId; Update(serialCode, vehicleId, enabled); }
    public void Update(string serialCode, string? vehicleId, bool enabled) { SerialCode = Rules.Text(serialCode, 80).ToUpperInvariant(); VehicleId = string.IsNullOrWhiteSpace(vehicleId) ? null : vehicleId; Enabled = enabled; }
}
public sealed class FleetAllocation {
    public string Id { get; private set; } = "";
    public string ShipmentId { get; private set; } = "";
    public string VehicleId { get; private set; } = "";
    public string DriverId { get; private set; } = "";
    public DateTimeOffset From { get; private set; }
    public DateTimeOffset Until { get; private set; }
    public string Status { get; private set; } = "reserved";
    public DateTimeOffset? ReleasedAt { get; private set; }
    private FleetAllocation() { }
    public FleetAllocation(string shipmentId, string vehicleId, string driverId, DateTimeOffset from, DateTimeOffset until) { Id = Guid.NewGuid().ToString(); ShipmentId = shipmentId; Reschedule(vehicleId, driverId, from, until); }
    public void Reschedule(string vehicleId, string driverId, DateTimeOffset from, DateTimeOffset until) { Rules.Require(until > from, "invalidDates"); VehicleId = vehicleId; DriverId = driverId; From = from.ToUniversalTime(); Until = until.ToUniversalTime(); }
    public void Release(DateTimeOffset at) { Status = "released"; ReleasedAt = at.ToUniversalTime(); }
    public bool Overlaps(DateTimeOffset from, DateTimeOffset until) => Status == "reserved" && from < Until && until > From;
}
