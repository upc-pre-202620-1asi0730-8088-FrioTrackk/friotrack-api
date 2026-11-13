using System.Text.RegularExpressions;
using FrioTrack.Api.Shared.Domain;
namespace FrioTrack.Api.Iam.Domain;

public sealed class Organization {
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}
public sealed class UserAccount {
    public string Id { get; private set; } = "";
    public string OrganizationId { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string CompanyName { get; private set; } = "";
    public string Role { get; private set; } = "cargo-client";
    public string Language { get; private set; } = "en";
    public string PasswordHash { get; private set; } = "";
    public string PasswordSalt { get; private set; } = "";
    public bool Enabled { get; private set; } = true;
    private UserAccount() { }
    public UserAccount(string id, string organizationId, string name, string email, string companyName, string role, string hash, string salt) {
        Id = id; OrganizationId = organizationId; Rules.Require(role is "coordinator" or "cargo-client", "forbidden"); Role = role; PasswordHash = hash; PasswordSalt = salt; Update(name, email, companyName, "en");
    }
    public void Update(string name, string email, string companyName, string? language) {
        Name = Rules.Text(name, 90, "invalidProfile"); CompanyName = Rules.Text(companyName, 100, "invalidProfile"); Email = Rules.Text(email, 140, "invalidProfile").ToLowerInvariant(); Rules.Require(Regex.IsMatch(Email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"), "invalidProfile");
        if (language is not null) { Rules.Require(language is "en" or "es", "invalidLanguage"); Language = language; }
    }
}
public sealed class AuthSession {
    public string TokenHash { get; set; } = "";
    public string UserId { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
}
public sealed class PasswordResetToken {
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}
