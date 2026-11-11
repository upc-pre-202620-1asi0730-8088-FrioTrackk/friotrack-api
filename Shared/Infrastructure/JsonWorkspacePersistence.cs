using System.Text.Json.Nodes;
using FrioTrack.Api.Shared.Application.Ports;
using FrioTrack.Api.Iam.Application.Ports;

namespace FrioTrack.Api.Shared.Infrastructure;

public sealed class JsonWorkspacePersistence : IWorkspacePersistence {
    readonly string path;
    readonly string seedPath;
    readonly IConfiguration configuration;
    readonly IPasswordHasher passwords;

    public JsonWorkspacePersistence(IConfiguration configuration, IWebHostEnvironment environment, IPasswordHasher passwords) {
        this.configuration = configuration;
        this.passwords = passwords;
        var home = Environment.GetEnvironmentVariable("HOME");
        path = configuration["FRIOTRACK_DATA_FILE"] ?? Path.Combine(home ?? environment.ContentRootPath, "data", "friotrack.json");
        seedPath = Path.Combine(environment.ContentRootPath, "seed.json");
    }
    public JsonObject Load() {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path)) return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var state = JsonNode.Parse(File.ReadAllText(seedPath))!.AsObject();
        var credentials = new JsonObject();
        foreach (var (id, setting) in new[] { ("coordinator-1", "FRIOTRACK_COORDINATOR_PASSWORD"), ("cargo-1", "FRIOTRACK_CLIENT_PASSWORD") }) {
            var password = configuration[setting] ?? throw new InvalidOperationException($"Missing {setting}");
            if (password.Length < 12) throw new InvalidOperationException("Initial passwords require at least 12 characters.");
            credentials[id] = passwords.Hash(password);
        }
        var database = new JsonObject { ["state"] = state, ["credentials"] = credentials, ["sessions"] = new JsonObject() };
        Save(database);
        return database;
    }
    public void Save(JsonObject database) {
        File.WriteAllText(path + ".tmp", database.ToJsonString());
        File.Move(path + ".tmp", path, true);
    }
}
