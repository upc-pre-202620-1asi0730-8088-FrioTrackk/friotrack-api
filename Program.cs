using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using FrioTrack.Api.Interfaces;
using FrioTrack.Api.Shared.Application;
using FrioTrack.Api.Shared.Application.Ports;
using FrioTrack.Api.Shared.Infrastructure;
using FrioTrack.Api.Iam.Application.Ports;
using FrioTrack.Api.Iam.Infrastructure;
using FrioTrack.Api.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options => {
    options.SerializerOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString;
    options.SerializerOptions.RespectRequiredConstructorParameters = true;
});
var persistenceMode = builder.Configuration["FRIOTRACK_PERSISTENCE"] ?? "postgres";
if (persistenceMode is not ("postgres" or "json")) throw new InvalidOperationException("FRIOTRACK_PERSISTENCE must be postgres or the explicit legacy json mode.");
var origins = (builder.Configuration["FRIOTRACK_ORIGINS"] ?? "http://localhost:5173,http://127.0.0.1:5173").Split(',', StringSplitOptions.RemoveEmptyEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(o => {
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
if (persistenceMode == "json") {
    builder.Services.AddSingleton<IWorkspacePersistence, JsonWorkspacePersistence>();
    builder.Services.AddSingleton<WorkspaceService>();
} else {
    var connection = builder.Configuration["FRIOTRACK_DB"] ?? builder.Configuration.GetConnectionString("FrioTrack");
    if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("PostgreSQL mode requires FRIOTRACK_DB or ConnectionStrings__FrioTrack. Use FRIOTRACK_PERSISTENCE=json only to reproduce the historical TB1 service.");
    builder.Services.AddDbContext<FrioTrackDbContext>(options => options.UseNpgsql(connection));
    builder.Services.AddScoped(typeof(IAggregateRepository<>), typeof(EfAggregateRepository<>));
    builder.Services.AddScoped<RelationalWorkspaceService>();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options => {
        options.SwaggerDoc("v1", new OpenApiInfo { Title="FríoTrack AV2 REST API", Version="0.2.0", Description="PostgreSQL/EF Core service for refrigerated shipment operations. Seed records and manually recorded telemetry are demonstration data. Sign in through /api/auth/login, then enter the opaque token through Authorize. Messages default to English; Accept-Language: es enables Spanish." });
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type=SecuritySchemeType.Http, Scheme="bearer", BearerFormat="Opaque session token", Description="Use the token returned by login. This is not a JWT." });
        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer",document)] = [] });
    });
}
var app = builder.Build();
app.UseCors();
app.UseRateLimiter();
app.UseMiddleware<ApiErrorMiddleware>();
if (persistenceMode == "json") app.MapWorkspaceEndpoints();
else {
    app.UseSwagger();
    app.UseSwaggerUI(options => { options.SwaggerEndpoint("/swagger/v1/swagger.json", "FríoTrack AV2 v1"); options.DocumentTitle="FríoTrack API documentation"; });
    app.MapAv2Endpoints();
    await DatabaseInitializer.Initialize(app);
}
app.Run();
