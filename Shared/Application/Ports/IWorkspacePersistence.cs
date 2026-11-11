using System.Text.Json.Nodes;

namespace FrioTrack.Api.Shared.Application.Ports;

public interface IWorkspacePersistence {
    JsonObject Load();
    void Save(JsonObject database);
}
