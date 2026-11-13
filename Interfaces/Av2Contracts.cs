using System.ComponentModel.DataAnnotations;

namespace FrioTrack.Api.Interfaces;

public sealed record LoginRequest(string Email, string Password);
public sealed record RegistrationRequest(string Name, string Email, string Organization, string Password, bool Consent, string Role = "cargo-client");
public sealed record ShipmentRequest(string Cargo, decimal Weight, string Origin, string Destination, string CustomerId, string VehicleId, string DriverId, decimal MinTemp, decimal MaxTemp, decimal MinHumidity, decimal MaxHumidity, DateTimeOffset Departure, DateTimeOffset Arrival, long Version = 0, int ToleranceMinutes = 0);
public sealed record TransitionRequest(long Version, string Status, string Note = "");
public sealed record ReadingRequest(decimal Temperature, decimal Humidity, decimal Lat, decimal Lng, DateTimeOffset? At = null);
public sealed record IncidentRequest(string Note);
public sealed record CorrectiveActionRequest(string Note, bool NotifyClient = false);
public sealed record VehicleRequest(string Plate, decimal Capacity, bool Enabled = true, string Sensor = "");
public sealed record DriverRequest(string Name, string License, bool Enabled = true);
public sealed record SensorRequest(string SerialCode, string? VehicleId = null, bool Enabled = true);
public sealed record ProfileRequest(string Name, string Email, string Organization, string? Language = null);
public sealed record ErrorResponse(string Code, string Message);
public sealed record ProfileResponse(string Id, string Name, string Email, string Organization, string Role, string Language);
public sealed record WorkspaceResponse(ProfileResponse Profile, object State);
public sealed record MutationResponse(ProfileResponse Profile, object State, object? Result);
