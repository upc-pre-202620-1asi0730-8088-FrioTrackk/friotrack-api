using System.Security.Cryptography;
using System.Text.Json.Nodes;
using FrioTrack.Api.Iam.Application.Ports;

namespace FrioTrack.Api.Iam.Infrastructure;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher {
    public JsonObject Hash(string value) {
        var salt = RandomNumberGenerator.GetBytes(16);
        return new JsonObject {
            ["salt"] = Convert.ToBase64String(salt),
            ["hash"] = Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(value, salt, 210_000, HashAlgorithmName.SHA256, 32))
        };
    }
    public bool Matches(string value, JsonObject credentials) {
        var salt = Convert.FromBase64String(credentials["salt"]!.ToString());
        var expected = Convert.FromBase64String(credentials["hash"]!.ToString());
        var actual = Rfc2898DeriveBytes.Pbkdf2(value, salt, 210_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
