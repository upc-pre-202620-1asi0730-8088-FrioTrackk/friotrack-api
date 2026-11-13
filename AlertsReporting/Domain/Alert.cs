using FrioTrack.Api.Shared.Domain;
namespace FrioTrack.Api.AlertsReporting.Domain;

public sealed record RecoveryEvidence(string ReadingId, DateTimeOffset At, bool BothRangesNormal);
public sealed class Alert {
    public string Id { get; private set; } = "";
    public string ShipmentId { get; private set; } = "";
    public string Type { get; private set; } = "";
    public decimal? Reading { get; private set; }
    public string? TriggerReadingId { get; private set; }
    public DateTimeOffset At { get; private set; }
    public DateTimeOffset LastDeviationAt { get; private set; }
    public string Severity { get; private set; } = "warning";
    public string Status { get; private set; } = "active";
    public DateTimeOffset? ResolvedAt { get; private set; }
    public string? RecoveryReadingId { get; private set; }
    public List<CorrectiveAction> Actions { get; private set; } = [];
    private Alert() { }
    public Alert(string shipmentId, string type, decimal? reading, string? readingId, DateTimeOffset at, string? id = null) { Id = id ?? Guid.NewGuid().ToString(); ShipmentId = shipmentId; Type = type; Reading = reading; TriggerReadingId = readingId; At = at.ToUniversalTime(); LastDeviationAt = At; }
    public void Observe(decimal? reading, DateTimeOffset at, int toleranceMinutes) { if (at > LastDeviationAt) { LastDeviationAt = at.ToUniversalTime(); Reading = reading; } Severity = LastDeviationAt - At >= TimeSpan.FromMinutes(toleranceMinutes) ? "critical" : "warning"; }
    public void Acknowledge(string actorId, string note, bool notifyClient, DateTimeOffset at) {
        Rules.Require(Status != "resolved", "alreadyResolved"); note = Rules.Text(note, 1000, "requiredAction"); Rules.Require(note.Length >= 8, "requiredAction");
        Actions.Add(new CorrectiveAction { Id = Guid.NewGuid().ToString(), AlertId = Id, ActorId = actorId, Note = note, NotifyClient = notifyClient, At = at.ToUniversalTime() }); Status = "acknowledged";
    }
    public bool TryResolveThermal(RecoveryEvidence evidence) {
        if (Status == "resolved" || Type == "signal" || !evidence.BothRangesNormal || evidence.At <= LastDeviationAt) return false;
        Status = "resolved"; ResolvedAt = evidence.At.ToUniversalTime(); RecoveryReadingId = evidence.ReadingId; return true;
    }
    public bool TryRestoreSignal(DateTimeOffset readingAt, DateTimeOffset receivedAt, int maxSilenceMinutes) {
        if (Status == "resolved" || Type != "signal" || readingAt <= At || receivedAt - readingAt > TimeSpan.FromMinutes(maxSilenceMinutes)) return false;
        Status = "resolved"; ResolvedAt = readingAt.ToUniversalTime(); return true;
    }
}
public sealed class CorrectiveAction {
    public string Id { get; set; } = "";
    public string AlertId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string Note { get; set; } = "";
    public bool NotifyClient { get; set; }
    public DateTimeOffset At { get; set; }
}
public sealed class Notification {
    public string Id { get; private set; } = Guid.NewGuid().ToString();
    public string ShipmentId { get; private set; } = "";
    public string RecipientId { get; private set; } = "";
    public string Type { get; private set; } = "";
    public DateTimeOffset At { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    private Notification() { }
    public Notification(string shipmentId, string recipientId, string type, DateTimeOffset at) { ShipmentId = shipmentId; RecipientId = recipientId; Type = type; At = at.ToUniversalTime(); }
    public void MarkRead(string actorId, DateTimeOffset at) { Rules.Require(actorId == RecipientId, "forbidden"); ReadAt ??= at.ToUniversalTime(); }
}
public sealed class Incident {
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ShipmentId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTimeOffset At { get; set; }
}
public sealed class ThermalReport {
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ShipmentId { get; set; } = "";
    public string GeneratedBy { get; set; } = "";
    public DateTimeOffset At { get; set; }
    public string Snapshot { get; set; } = "{}";
}
