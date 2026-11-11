using System.Text.Json.Nodes;

namespace FrioTrack.Api.Iam.Application.Ports;

public interface IPasswordHasher {
    JsonObject Hash(string value);
    bool Matches(string value, JsonObject credentials);
}
