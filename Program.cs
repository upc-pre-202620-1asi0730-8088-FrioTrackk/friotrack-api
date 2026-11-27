using FrioTrack.Api.Iam.Application.CommandServices;
using FrioTrack.Api.Iam.Application.QueryServices;
using FrioTrack.Api.AlertsReporting.Application.QueryServices;
using FrioTrack.Api.Iam.Application;
using FrioTrack.Api.AlertsReporting.Application;
using FrioTrack.Api.AlertsReporting.Infrastructure;
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
using Scalar.AspNetCore;
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
    o.AddPolicy("contact", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=3,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
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
    builder.Services.AddScoped<IAccountSecurityCommandService,AccountSecurityService>();
    builder.Services.AddScoped<IAccountAccessCommandService,AccountAdministration>();
    builder.Services.AddScoped<IAccountAccessQueryService,AccountAdministration>();
    builder.Services.AddScoped<IOperationalQueryService,OperationalReporting>();
    builder.Services.AddScoped<FrioTrack.Api.ShipmentManagement.Application.QueryServices.IShipmentQueryService,FrioTrack.Api.ShipmentManagement.Application.ShipmentQueries>();
    builder.Services.AddSingleton<FrioTrack.Api.AlertsReporting.Application.Ports.IReportDocumentRenderer,ReportPdfRenderer>();
    builder.Services.AddControllers().AddJsonOptions(options=>options.JsonSerializerOptions.RespectRequiredConstructorParameters=true);
    builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options=>options.InvalidModelStateResponseFactory=context=>new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new ErrorResponse("requiredFields","Check the required request fields.")));
    builder.Services.AddScoped<IRecoveryMail,SmtpRecoveryMail>();
    builder.Services.AddScoped<FrioTrack.Api.CommercialContact.Application.Ports.ICommercialContactMail,FrioTrack.Api.CommercialContact.Infrastructure.SmtpCommercialContactMail>();
    ReportPdfRenderer.ConfigureFonts();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options => {
        options.SwaggerDoc("v1", new OpenApiInfo { Title="FríoTrack REST API", Version="0.4.0", Description="Servicio de gestión de envíos refrigerados, flota, alertas e historial. Las lecturas y coordenadas de demostración no representan sensores físicos. Inicie sesión en /api/auth/login y utilice el token opaco recibido en la autenticación Bearer. Accept-Language: es permite consultar los mensajes en español." });
        options.OperationFilter<PublicOperationFilter>();
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
    app.MapScalarApiReference(options => options
        .WithTitle("FríoTrack API — Documentación")
        .WithOpenApiRoutePattern("/swagger/{documentName}/swagger.json")
        .DisableAgent());
    app.UseSwaggerUI(options => { options.SwaggerEndpoint("/swagger/v1/swagger.json", "FríoTrack AV2 v1"); options.DocumentTitle="FríoTrack API documentation"; });
    app.MapAv2Endpoints();
    app.MapControllers();
    app.MapGet("/api/capabilities",(IAccountSecurityCommandService service,FrioTrack.Api.CommercialContact.Application.Ports.ICommercialContactMail contact)=>new{passwordRecovery=service.RecoveryAvailable,commercialContact=contact.IsConfigured,monthlyPdf=true,analytics=true,quarterlyAnalytics=true}).WithTags("Health");
    await DatabaseInitializer.Initialize(app);
}
app.Run();
