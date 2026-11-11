using System.Text.Json.Nodes;
using FrioTrack.Api.Shared.Application;

namespace FrioTrack.Api.Interfaces;

public static class WorkspaceEndpoints {
    public static void MapWorkspaceEndpoints(this WebApplication app) {
        app.MapGet("/api/health", () => new { status = "ok", service = "FrioTrack", version = "2.0" });
        app.MapPost("/api/auth/login", (JsonObject body, WorkspaceService service) => service.Login(body)).RequireRateLimiting("auth");
        app.MapPost("/api/auth/register", (JsonObject body, WorkspaceService service) => service.Register(body)).RequireRateLimiting("auth");
        app.MapGet("/api/workspace", (HttpContext context, WorkspaceService service) => service.Workspace(context.Request.Headers.Authorization.ToString()));
        app.MapPost("/api/commands", (JsonObject body, HttpContext context, WorkspaceService service) => service.Command(context.Request.Headers.Authorization.ToString(), body));
        app.MapPost("/api/auth/logout", (HttpContext context, WorkspaceService service) => {
            service.Logout(context.Request.Headers.Authorization.ToString());
            return Results.NoContent();
        });
    }
}
