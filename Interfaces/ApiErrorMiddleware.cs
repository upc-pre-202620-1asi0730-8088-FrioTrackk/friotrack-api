using FrioTrack.Api.Shared.Application;

namespace FrioTrack.Api.Interfaces;

public sealed class ApiErrorMiddleware(RequestDelegate next) {
    public async Task InvokeAsync(HttpContext context) {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        try { await next(context); }
        catch (ApiError error) {
            context.Response.StatusCode = error.Status;
            await context.Response.WriteAsJsonAsync(new { code = error.Code });
        }
        catch (System.Text.Json.JsonException) {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { code = "requiredFields" });
        }
    }
}
