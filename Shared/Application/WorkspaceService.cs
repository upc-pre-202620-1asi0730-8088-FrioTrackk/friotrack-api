using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using FrioTrack.Api.Shared.Application.Ports;
using FrioTrack.Api.Iam.Application.Ports;

namespace FrioTrack.Api.Shared.Application;

public sealed class WorkspaceService {
    readonly object gate = new();
    JsonObject database;
    static string Text(JsonNode? n, string key) => n?[key]?.ToString() ?? "";
    static string Now() => DateTimeOffset.UtcNow.ToString("O");
    static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    static JsonObject Copy(JsonObject value) => (JsonObject)value.DeepClone();
    readonly IWorkspacePersistence persistence;
    readonly IPasswordHasher passwords;
    public WorkspaceService(IWorkspacePersistence persistence, IPasswordHasher passwords) {
        this.persistence = persistence;
        this.passwords = passwords;
        database = persistence.Load();
    }
    void Save() => persistence.Save(database);
    JsonObject State => database["state"]!.AsObject();
    JsonObject Sessions => database["sessions"]!.AsObject();
    JsonObject Account(string authorization) {
        var header = authorization;
        if (!header.StartsWith("Bearer ", StringComparison.Ordinal)) throw new ApiError("sessionExpired", 401);
        var session = Sessions[Digest(header[7..])];
        if (session is null || !DateTimeOffset.TryParse(Text(session, "expiresAt"), out var expiry) || expiry <= DateTimeOffset.UtcNow) throw new ApiError("sessionExpired", 401);
        return State["profiles"]!.AsArray().OfType<JsonObject>().FirstOrDefault(p => Text(p, "id") == Text(session, "profileId")) ?? throw new ApiError("sessionExpired", 401);
    }
    JsonObject StartSession(JsonObject account) {
        foreach (var expired in Sessions.Where(s => DateTimeOffset.Parse(Text(s.Value, "expiresAt")) <= DateTimeOffset.UtcNow).Select(s => s.Key).ToArray()) Sessions.Remove(expired);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expires = DateTimeOffset.UtcNow.AddHours(8).ToString("O");
        Sessions[Digest(token)] = new JsonObject { ["profileId"] = Text(account, "id"), ["expiresAt"] = expires };
        Save();
        return new JsonObject { ["token"] = token, ["expiresAt"] = expires, ["profile"] = account.DeepClone(), ["state"] = Visible(account) };
    }
    public JsonObject Login(JsonObject body) { lock (gate) {
        var email = Text(body, "email").Trim().ToLowerInvariant();
        var password = Text(body, "password");
        if (email.Length > 140 || password.Length > 128) throw new ApiError("invalidCredentials", 401);
        var account = State["profiles"]!.AsArray().OfType<JsonObject>().FirstOrDefault(p => Text(p, "email").ToLowerInvariant() == email);
        var credentials = account is null ? null : database["credentials"]?[Text(account, "id")] as JsonObject;
        // Always perform a password derivation, including unknown accounts.
        var valid = credentials is null ? passwords.Matches(password, passwords.Hash("invalid-account-placeholder")) : passwords.Matches(password, credentials);
        if (credentials is null || !valid) throw new ApiError("invalidCredentials", 401);
        return StartSession(account!);
    } }
    public JsonObject Register(JsonObject body) { lock (gate) {
        var password = Text(body, "password");
        if (password.Length < 12 || password.Length > 128) throw new ApiError("invalidPassword");
        if (body["consent"]?.ToString() != "true") throw new ApiError("requiredConsent");
        if (Text(body, "role") is not ("" or "cargo-client")) throw new ApiError("forbidden", 403);
        var account = new JsonObject { ["id"] = "profile-" + Guid.NewGuid(), ["name"] = Text(body, "name").Trim(), ["email"] = Text(body, "email").Trim().ToLowerInvariant(), ["organization"] = Text(body, "organization").Trim(), ["role"] = "cargo-client" };
        WorkspaceCommands.ValidateProfile(State, account, "");
        State["profiles"]!.AsArray().Add(account);
        database["credentials"]![Text(account, "id")] = passwords.Hash(password);
        return StartSession(account);
    } }
    JsonObject Visible(JsonObject account) {
        var state = Copy(State);
        if (Text(account, "role") == "coordinator") return state;
        var id = Text(account, "id");
        var shipments = state["shipments"]!.AsArray().OfType<JsonObject>().Where(s => Text(s, "customerId") == id).ToArray();
        var ids = shipments.Select(s => Text(s, "id")).ToHashSet();
        void Filter(string key, Func<JsonObject, bool> predicate) {
            var array = state[key]!.AsArray();
            for (var i = array.Count - 1; i >= 0; i--) if (!predicate(array[i]!.AsObject())) array.RemoveAt(i);
        }
        Filter("profiles", p => Text(p, "id") == id);
        Filter("shipments", s => ids.Contains(Text(s, "id")));
        Filter("alerts", a => ids.Contains(Text(a, "shipmentId")));
        Filter("notifications", n => ids.Contains(Text(n, "shipmentId")) && (n["recipientProfileIds"] is not JsonArray recipients || recipients.Any(r => r?.ToString() == id)));
        Filter("vehicles", v => shipments.Any(s => Text(s, "vehicleId") == Text(v, "id")));
        Filter("drivers", d => shipments.Any(s => Text(s, "driverId") == Text(d, "id")));
        return state;
    }
    public JsonObject Workspace(string authorization) { lock (gate) { var account = Account(authorization); return new JsonObject { ["profile"] = account.DeepClone(), ["state"] = Visible(account) }; } }
    public JsonObject Command(string authorization, JsonObject body) { lock (gate) {
        var account = Account(authorization);
        var state = Copy(State);
        var result = WorkspaceCommands.Apply(state, account, Text(body, "type"), body["payload"] as JsonObject ?? new JsonObject());
        database["state"] = state;
        Save();
        var current = state["profiles"]!.AsArray().OfType<JsonObject>().First(p => Text(p, "id") == Text(account, "id"));
        return new JsonObject { ["profile"] = current.DeepClone(), ["state"] = Visible(current), ["result"] = result?.DeepClone() };
    } }
    public void Logout(string authorization) { lock (gate) { Account(authorization); Sessions.Remove(Digest(authorization[7..])); Save(); } }
}
