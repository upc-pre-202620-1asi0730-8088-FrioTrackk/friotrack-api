using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using FrioTrack.Api.Interfaces;
using FrioTrack.Api.Shared.Application;
using FrioTrack.Api.Shared.Application.Ports;
using FrioTrack.Api.Shared.Infrastructure;
using FrioTrack.Api.Iam.Application.Ports;
using FrioTrack.Api.Iam.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var origins = (builder.Configuration["FRIOTRACK_ORIGINS"] ?? "http://localhost:5173,http://127.0.0.1:5173").Split(',', StringSplitOptions.RemoveEmptyEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(o => {
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IWorkspacePersistence, JsonWorkspacePersistence>();
builder.Services.AddSingleton<WorkspaceService>();
var app = builder.Build();
app.UseCors();
app.UseRateLimiter();
app.UseMiddleware<ApiErrorMiddleware>();
app.MapWorkspaceEndpoints();
app.Run();
