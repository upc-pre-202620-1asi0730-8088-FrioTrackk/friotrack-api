using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using FrioTrack.Api.Iam.Application.Ports;
using FrioTrack.Api.Iam.Domain;
using FrioTrack.Api.Fleet.Domain;
using FrioTrack.Api.ShipmentManagement.Domain;
using FrioTrack.Api.Monitoring.Domain;
using FrioTrack.Api.AlertsReporting.Domain;

namespace FrioTrack.Api.Shared.Infrastructure;

public static class DatabaseInitializer {
    static string S(JsonNode? n,string key) => n?[key]?.ToString() ?? "";
    static decimal N(JsonNode? n,string key) => decimal.Parse(S(n,key),System.Globalization.CultureInfo.InvariantCulture);
    static DateTimeOffset D(JsonNode? n,string key) => DateTimeOffset.Parse(S(n,key)).ToUniversalTime();
    public static async Task Initialize(WebApplication app) {
        await using var scope=app.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<FrioTrackDbContext>();
        if(app.Configuration["FRIOTRACK_MIGRATE"]=="true")await db.Database.MigrateAsync();
        if(app.Configuration["FRIOTRACK_SEED"]!="true")return;
        await using var tx=await db.Database.BeginTransactionAsync();await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(742136572)");
        if(await db.Organizations.AnyAsync()){await tx.CommitAsync();return;}
        var coordinatorPassword=app.Configuration["FRIOTRACK_COORDINATOR_PASSWORD"] ?? "";var clientPassword=app.Configuration["FRIOTRACK_CLIENT_PASSWORD"] ?? "";
        if(coordinatorPassword.Length<12 || clientPassword.Length<12)throw new InvalidOperationException("Empty database seeding requires two privately supplied passwords of at least 12 characters.");
        var passwords=scope.ServiceProvider.GetRequiredService<IPasswordHasher>();var seed=JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(app.Environment.ContentRootPath,"seed.json")))!.AsObject();
        const string organizationId="demo-organization";db.Organizations.Add(new Organization{Id=organizationId,Name="ColdWay Logistics — demonstration"});
        foreach(var p in seed["profiles"]!.AsArray()) {
            var password=S(p,"role")=="coordinator"?coordinatorPassword:clientPassword;var credentials=passwords.Hash(password);
            db.Users.Add(new UserAccount(S(p,"id"),organizationId,S(p,"name"),S(p,"email"),S(p,"organization"),S(p,"role"),S(credentials,"hash"),S(credentials,"salt")));
        }
        foreach(var v in seed["vehicles"]!.AsArray()){db.Vehicles.Add(new Vehicle(S(v,"id"),organizationId,S(v,"plate"),N(v,"capacity"),S(v,"enabled")!="false"));if(!string.IsNullOrEmpty(S(v,"sensor")))db.Sensors.Add(new Sensor("sensor-"+S(v,"id"),organizationId,S(v,"sensor"),S(v,"id"),true));}
        foreach(var d in seed["drivers"]!.AsArray())db.Drivers.Add(new Driver(S(d,"id"),organizationId,S(d,"name"),S(d,"license"),S(d,"enabled")!="false"));
        foreach(var s in seed["shipments"]!.AsArray()) {
            var range=new ThermalRange(N(s,"minTemp"),N(s,"maxTemp"),N(s,"minHumidity"),N(s,"maxHumidity"));
            var shipment=Shipment.Schedule(S(s,"id"),organizationId,"coordinator-1",S(s,"customerId"),S(s,"vehicleId"),S(s,"driverId"),S(s,"cargo"),N(s,"weight"),S(s,"origin"),S(s,"destination"),D(s,"departure"),D(s,"arrival"),range,true,D(s,"departure").AddDays(-1));
            var history=s!["history"]!.AsArray().Select(h=>new ShipmentHistoryEvent{Id=Guid.NewGuid().ToString(),ShipmentId=shipment.Id,ActorId="coordinator-1",Action=S(h,"action"),Note=S(h,"note"),At=D(h,"at")});
            shipment.ImportHistory(S(s,"status"),(long)N(s,"version"),history);db.Shipments.Add(shipment);
            var allocation=new FleetAllocation(shipment.Id,shipment.VehicleId,shipment.DriverId,shipment.Departure,shipment.Arrival);if(shipment.Status is "delivered" or "cancelled")allocation.Release(shipment.Arrival);db.Allocations.Add(allocation);
            if(shipment.Status is "in-transit" or "delivered") {
                var monitoring=new ShipmentMonitoring(shipment.Id,new MonitoredRange(range.MinTemp,range.MaxTemp,range.MinHumidity,range.MaxHumidity,range.ToleranceMinutes),shipment.Departure);
                foreach(var r in s["readings"]!.AsArray())monitoring.Record(N(r,"temperature"),N(r,"humidity"),N(r,"lat"),N(r,"lng"),D(r,"at"),D(r,"at"));
                if(shipment.Status=="delivered")monitoring.Stop(shipment.Arrival);
                else if(S(s,"offline")=="true")monitoring.EvaluateSignal((monitoring.LastReadingAt ?? monitoring.StartedAt).AddMinutes(monitoring.MaxSilenceMinutes+1));
                db.Monitorings.Add(monitoring);
            }
        }
        foreach(var a in seed["alerts"]!.AsArray()) {
            var alertType=S(a,"type")=="offline"?"signal":S(a,"type");
            var alert=new Alert(S(a,"shipmentId"),alertType,decimal.TryParse(S(a,"reading"),System.Globalization.CultureInfo.InvariantCulture,out var value)?value:null,null,D(a,"at"),S(a,"id"));
            foreach(var action in a!["actions"]?.AsArray() ?? [])alert.Acknowledge("coordinator-1",S(action,"note"),false,D(action,"at"));
            if(S(a,"resolved")=="true") {
                var resolvedAt=DateTimeOffset.TryParse(S(a,"resolvedAt"),out var parsedResolution)?parsedResolution:D(a,"at").AddMinutes(1);
                if(alertType=="signal")alert.TryRestoreSignal(resolvedAt,resolvedAt,15);
                else alert.TryResolveThermal(new RecoveryEvidence("historical-demonstration",resolvedAt,true));
            }
            db.Alerts.Add(alert);
        }
        foreach(var n in seed["notifications"]!.AsArray()) {
            var shipment=seed["shipments"]!.AsArray().FirstOrDefault(s=>S(s,"id")==S(n,"shipmentId"));if(shipment is null)continue;
            var recipients=n!["recipientProfileIds"]?.AsArray().Select(x=>x!.ToString()).ToArray() ?? ["coordinator-1",S(shipment,"customerId")];
            foreach(var recipient in recipients.Distinct()) {var notice=new Notification(S(n,"shipmentId"),recipient,S(n,"type"),D(n,"at"));if(n["readBy"]?.AsArray().Any(x=>x?.ToString()==recipient)==true)notice.MarkRead(recipient,D(n,"at"));db.Notifications.Add(notice);}
        }
        await db.SaveChangesAsync();await tx.CommitAsync();
        app.Logger.LogInformation("Seeded an empty PostgreSQL database with explicitly identified demonstration records. Existing JSON files were not imported or modified.");
    }
}
