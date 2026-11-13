using FrioTrack.Api.Shared.Domain;
namespace FrioTrack.Api.Monitoring.Domain;

public sealed record MonitoredRange(decimal MinTemp, decimal MaxTemp, decimal MinHumidity, decimal MaxHumidity, int ToleranceMinutes) {
    public bool Contains(decimal temperature, decimal humidity) => temperature >= MinTemp && temperature <= MaxTemp && humidity >= MinHumidity && humidity <= MaxHumidity;
}
public sealed class ShipmentMonitoring {
    public string Id { get; private set; } = "";
    public string ShipmentId { get; private set; } = "";
    public decimal MinTemp { get; private set; }
    public decimal MaxTemp { get; private set; }
    public decimal MinHumidity { get; private set; }
    public decimal MaxHumidity { get; private set; }
    public int ToleranceMinutes { get; private set; }
    public int MaxSilenceMinutes { get; private set; } = 15;
    public string SignalStatus { get; private set; } = "unknown";
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? StoppedAt { get; private set; }
    public DateTimeOffset? LastReadingAt { get; private set; }
    public List<SensorReading> Readings { get; private set; } = [];
    public List<PositionReport> Positions { get; private set; } = [];
    private ShipmentMonitoring() { }
    public ShipmentMonitoring(string shipmentId, MonitoredRange range, DateTimeOffset at) {
        Id = Guid.NewGuid().ToString(); ShipmentId = shipmentId; MinTemp = range.MinTemp; MaxTemp = range.MaxTemp; MinHumidity = range.MinHumidity; MaxHumidity = range.MaxHumidity; ToleranceMinutes = range.ToleranceMinutes; StartedAt = at.ToUniversalTime();
    }
    public SensorReading Record(decimal temperature, decimal humidity, decimal latitude, decimal longitude, DateTimeOffset at, DateTimeOffset receivedAt) {
        Rules.Require(StoppedAt is null, "readingsTransitOnly");
        Rules.Require(temperature >= -50 && temperature <= 50 && humidity >= 0 && humidity <= 100 && latitude >= -90 && latitude <= 90 && longitude >= -180 && longitude <= 180, "invalidReading");
        Rules.Require(at <= receivedAt.AddMinutes(1), "invalidReadingDate");
        Rules.Require(at >= StartedAt && (LastReadingAt is null || at > LastReadingAt), "invalidReadingDate");
        var reading = new SensorReading { Id = Guid.NewGuid().ToString(), MonitoringId = Id, Temperature = temperature, Humidity = humidity, At = at.ToUniversalTime(), WithinRange = new MonitoredRange(MinTemp, MaxTemp, MinHumidity, MaxHumidity, ToleranceMinutes).Contains(temperature, humidity), IsDemonstration = true };
        Readings.Add(reading); Positions.Add(new PositionReport { Id = Guid.NewGuid().ToString(), MonitoringId = Id, ReadingId = reading.Id, Latitude = latitude, Longitude = longitude, At = reading.At });
        LastReadingAt = reading.At;
        if (receivedAt - at <= TimeSpan.FromMinutes(MaxSilenceMinutes)) SignalStatus = "connected";
        return reading;
    }
    public bool EvaluateSignal(DateTimeOffset now) {
        Rules.Require(StoppedAt is null, "monitoringStopped");
        var previous = SignalStatus;
        SignalStatus = now - (LastReadingAt ?? StartedAt) > TimeSpan.FromMinutes(MaxSilenceMinutes) ? "lost" : LastReadingAt is null ? "unknown" : "connected";
        return previous != SignalStatus;
    }
    public void Stop(DateTimeOffset at) => StoppedAt = at.ToUniversalTime();
}
public sealed class SensorReading {
    public string Id { get; set; } = "";
    public string MonitoringId { get; set; } = "";
    public decimal Temperature { get; set; }
    public decimal Humidity { get; set; }
    public DateTimeOffset At { get; set; }
    public bool WithinRange { get; set; }
    public bool IsDemonstration { get; set; }
}
public sealed class PositionReport {
    public string Id { get; set; } = "";
    public string MonitoringId { get; set; } = "";
    public string ReadingId { get; set; } = "";
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public DateTimeOffset At { get; set; }
}
