namespace FrioTrack.Api.Shared.Domain;

public sealed class DomainRuleException(string code) : Exception(code) {
    public string Code { get; } = code;
}
public static class Rules {
    public static void Require(bool condition, string code) { if (!condition) throw new DomainRuleException(code); }
    public static string Text(string? value, int maximum, string code = "requiredFields") {
        var text = value?.Trim() ?? "";
        Require(text.Length > 0 && text.Length <= maximum, code); return text;
    }
    public static void Version(long actual, long expected) => Require(actual == expected, "staleShipment");
}
