using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using FrioTrack.Api.Shared.Infrastructure;
using FrioTrack.Api.Shared.Domain;
using FrioTrack.Api.Iam.Application.Ports;
using FrioTrack.Api.Iam.Domain;
using FrioTrack.Api.ShipmentManagement.Domain;
using FrioTrack.Api.Monitoring.Domain;
using FrioTrack.Api.AlertsReporting.Domain;
using FrioTrack.Api.Fleet.Domain;

namespace FrioTrack.Api.Shared.Application;

/// <summary>Scoped PostgreSQL use cases. Commands commit one EF unit of work before projecting a response.</summary>
public sealed class RelationalWorkspaceService(FrioTrackDbContext db, IPasswordHasher passwords, IConfiguration configuration) {
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static JsonObject Object(object value) => JsonSerializer.SerializeToNode(value, Json)!.AsObject();
    static string S(JsonNode? value, string key) => value?[key]?.ToString() ?? "";
    static decimal N(JsonNode? value, string key, decimal? fallback = null) => decimal.TryParse(S(value,key), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : fallback ?? throw new ApiError("requiredFields");
    static bool B(JsonNode? value, string key, bool fallback = false) => bool.TryParse(S(value,key), out var b) ? b : fallback;
    static DateTimeOffset D(JsonNode? value, string key) => DateTimeOffset.TryParse(S(value,key), out var d) ? d.ToUniversalTime() : throw new ApiError("invalidDates");
    static string Id() => Guid.NewGuid().ToString();
    static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    static JsonObject Profile(UserAccount u) => Object(new { u.Id, u.Name, u.Email, organization = u.CompanyName, u.Role, language = u.Language });
    static void Require(bool condition, string code, int status = 400) { if (!condition) throw new ApiError(code,status); }
    public async Task<UserAccount> Account(string authorization) {
        Require(authorization.StartsWith("Bearer ", StringComparison.Ordinal) && authorization.Length <= 200, "sessionExpired", 401);
        var digest = Digest(authorization[7..]); var now = DateTimeOffset.UtcNow;
        var session = await db.Sessions.SingleOrDefaultAsync(x => x.TokenHash == digest && x.ExpiresAt > now);
        Require(session is not null, "sessionExpired", 401);
        return await db.Users.SingleOrDefaultAsync(x => x.Id == session!.UserId && x.Enabled) ?? throw new ApiError("sessionExpired",401);
    }
    public async Task<JsonObject> Login(JsonObject body) {
        var email = S(body,"email").Trim().ToLowerInvariant(); var password = S(body,"password");
        Require(email.Length <= 140 && password.Length <= 128, "invalidCredentials",401);
        var account = await db.Users.SingleOrDefaultAsync(x => x.Email == email && x.Enabled);
        var credentials = account is null ? passwords.Hash("unknown-account-derivation") : new JsonObject { ["salt"] = account.PasswordSalt, ["hash"] = account.PasswordHash };
        var valid = passwords.Matches(password, credentials); Require(account is not null && valid, "invalidCredentials",401);
        return await StartSession(account!);
    }
    async Task<JsonObject> StartSession(UserAccount account) {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); var expires = DateTimeOffset.UtcNow.AddHours(8);
        db.Sessions.Add(new AuthSession { TokenHash = Digest(token), UserId = account.Id, ExpiresAt = expires }); await db.SaveChangesAsync();
        return new JsonObject { ["token"] = token, ["expiresAt"] = expires.ToString("O"), ["profile"] = Profile(account), ["state"] = await State(account) };
    }
    public async Task<JsonObject> Register(JsonObject body) {
        var password = S(body,"password"); Require(password.Length is >= 12 and <= 128, "invalidPassword"); Require(B(body,"consent"),"requiredConsent"); Require(S(body,"role") is "" or "cargo-client", "forbidden",403);
        var email = S(body,"email").Trim().ToLowerInvariant(); Require(!await db.Users.AnyAsync(x => x.Email == email),"duplicateProfile",409);
        var organizationId = configuration["FRIOTRACK_PUBLIC_ORGANIZATION_ID"] ?? "demo-organization";
        Require(await db.Organizations.AnyAsync(x => x.Id == organizationId), "registrationUnavailable",503);
        var hashed = passwords.Hash(password);
        var account = new UserAccount("profile-"+Id(),organizationId,S(body,"name"),email,S(body,"organization"),"cargo-client",S(hashed,"hash"),S(hashed,"salt"));
        db.Users.Add(account); await db.SaveChangesAsync(); return await StartSession(account);
    }
    public async Task Logout(string authorization) {
        await Account(authorization); var hash = Digest(authorization[7..]); var session = await db.Sessions.SingleAsync(x => x.TokenHash == hash); db.Sessions.Remove(session); await db.SaveChangesAsync();
    }
    public async Task<JsonObject> Workspace(string authorization) { var account = await Account(authorization); return new JsonObject { ["profile"] = Profile(account), ["state"] = await State(account) }; }
    public async Task<JsonObject> State(UserAccount actor) {
        var shipments = await db.Shipments.AsNoTracking().Include(x => x.History).Where(x => x.OrganizationId == actor.OrganizationId && (actor.Role == "coordinator" || x.CustomerId == actor.Id)).OrderByDescending(x => x.Departure).ToListAsync();
        var ids = shipments.Select(x=>x.Id).ToArray(); var vehicleIds = shipments.Select(x=>x.VehicleId).ToArray(); var driverIds = shipments.Select(x=>x.DriverId).ToArray();
        var users = await db.Users.AsNoTracking().Where(x => x.OrganizationId == actor.OrganizationId && (actor.Role == "coordinator" || x.Id == actor.Id)).ToListAsync();
        var vehicles = await db.Vehicles.AsNoTracking().Where(x=>x.OrganizationId == actor.OrganizationId && (actor.Role == "coordinator" || vehicleIds.Contains(x.Id))).OrderBy(x=>x.Plate).ToListAsync();
        var drivers = await db.Drivers.AsNoTracking().Where(x=>x.OrganizationId == actor.OrganizationId && (actor.Role == "coordinator" || driverIds.Contains(x.Id))).OrderBy(x=>x.Name).ToListAsync();
        var sensors = await db.Sensors.AsNoTracking().Where(x=>x.OrganizationId == actor.OrganizationId && (actor.Role == "coordinator" || x.VehicleId != null && vehicleIds.Contains(x.VehicleId))).ToListAsync();
        var monitoring = await db.Monitorings.AsNoTracking().Include(x=>x.Readings).Include(x=>x.Positions).Where(x=>ids.Contains(x.ShipmentId)).ToListAsync();
        var alerts = await db.Alerts.AsNoTracking().Include(x=>x.Actions).Where(x=>ids.Contains(x.ShipmentId)).OrderByDescending(x=>x.At).ToListAsync();
        var notifications = await db.Notifications.AsNoTracking().Where(x=>ids.Contains(x.ShipmentId) && x.RecipientId == actor.Id).OrderByDescending(x=>x.At).ToListAsync();
        var state = new JsonObject { ["schema"]=2, ["revision"]=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), ["dataSource"]="demonstration", ["persistence"]="postgresql" };
        state["profiles"] = new JsonArray(users.Select(x=>(JsonNode)Profile(x)).ToArray());
        state["vehicles"] = JsonSerializer.SerializeToNode(vehicles.Select(v=>new {v.Id,v.Plate,v.Capacity,v.Enabled,sensor=sensors.FirstOrDefault(s=>s.VehicleId==v.Id)?.SerialCode ?? ""}),Json);
        state["drivers"] = JsonSerializer.SerializeToNode(drivers.Select(d=>new {d.Id,d.Name,d.License,d.Enabled}),Json);
        state["sensors"] = JsonSerializer.SerializeToNode(sensors.Select(s=>new{s.Id,s.SerialCode,s.VehicleId,s.Enabled}),Json);
        state["shipments"] = JsonSerializer.SerializeToNode(shipments.Select(s=> {
            var monitor = monitoring.SingleOrDefault(m=>m.ShipmentId==s.Id);
            return new {s.Id,s.Version,s.Cargo,s.Weight,s.Origin,s.Destination,s.CustomerId,s.VehicleId,s.DriverId,s.Departure,s.Arrival,s.Status,s.Range.MinTemp,s.Range.MaxTemp,s.Range.MinHumidity,s.Range.MaxHumidity,s.Range.ToleranceMinutes,
                offline=monitor?.SignalStatus=="lost", dataSource="demonstration",
                readings=(monitor?.Readings ?? []).OrderBy(r=>r.At).Select(r=>new {r.Id,r.At,r.Temperature,r.Humidity,lat=monitor?.Positions.FirstOrDefault(p=>p.ReadingId==r.Id)?.Latitude,lng=monitor?.Positions.FirstOrDefault(p=>p.ReadingId==r.Id)?.Longitude,source="demonstration"}),
                history=s.History.OrderBy(h=>h.At).Select(h=>new {h.At,h.Action,h.Note,actor=users.FirstOrDefault(u=>u.Id==h.ActorId)?.Name ?? "Logistics coordinator"})};
        }),Json);
        state["alerts"] = JsonSerializer.SerializeToNode(alerts.Select(a=>new {a.Id,a.ShipmentId,a.Type,a.Reading,a.At,a.LastDeviationAt,a.Severity,resolved=a.Status=="resolved",acknowledged=a.Status=="acknowledged" || a.Actions.Count>0,a.ResolvedAt,actions=a.Actions.OrderBy(x=>x.At).Select(x=>new {x.Note,x.At,actor=users.FirstOrDefault(u=>u.Id==x.ActorId)?.Name ?? "Logistics coordinator"})}),Json);
        state["notifications"] = JsonSerializer.SerializeToNode(notifications.Select(n=>new {n.Id,n.ShipmentId,n.Type,n.At,recipientProfileIds=new[]{n.RecipientId},readBy=n.ReadAt is null ? Array.Empty<string>() : new[]{n.RecipientId}}),Json);
        return state;
    }
    async Task<Shipment> FindShipment(UserAccount actor,string id) => await db.Shipments.Include(x=>x.History).SingleOrDefaultAsync(x=>x.Id==id && x.OrganizationId==actor.OrganizationId && (actor.Role=="coordinator" || x.CustomerId==actor.Id)) ?? throw new ApiError("notFound",404);
    async Task Notify(Shipment shipment,string type,DateTimeOffset at,bool clientOnly=false) {
        var recipients = clientOnly ? new[]{shipment.CustomerId} : new[]{shipment.CustomerId,shipment.CoordinatorId};
        foreach(var recipient in recipients.Distinct()) db.Notifications.Add(new Notification(shipment.Id,recipient,type,at));
        await Task.CompletedTask;
    }
    async Task ValidateAssignment(UserAccount actor,JsonObject p,string shipmentId="") {
        var vehicleId=S(p,"vehicleId");var driverId=S(p,"driverId");var customerId=S(p,"customerId");
        var vehicle=await db.Vehicles.SingleOrDefaultAsync(x=>x.Id==vehicleId && x.OrganizationId==actor.OrganizationId && x.Enabled);
        Require(vehicle is not null && await db.Drivers.AnyAsync(x=>x.Id==driverId && x.OrganizationId==actor.OrganizationId && x.Enabled),"missingAssignment");
        Require(await db.Users.AnyAsync(x=>x.Id==customerId && x.OrganizationId==actor.OrganizationId && x.Role=="cargo-client" && x.Enabled),"missingCustomer");
        Require(N(p,"weight")<=vehicle!.Capacity,"overCapacity"); var from=D(p,"departure");var until=D(p,"arrival");
        var overlapping=db.Allocations.Where(x=>x.ShipmentId!=shipmentId && x.Status=="reserved" && from<x.Until && until>x.From);
        Require(!await overlapping.AnyAsync(x=>x.VehicleId==vehicleId),"vehicleBusy",409);Require(!await overlapping.AnyAsync(x=>x.DriverId==driverId),"driverBusy",409);
    }
    ThermalRange Range(JsonObject p) => new(N(p,"minTemp"),N(p,"maxTemp"),N(p,"minHumidity"),N(p,"maxHumidity"),(int)N(p,"toleranceMinutes",0));
    public async Task<JsonObject> Command(string authorization,string type,JsonObject p) {
        var actor=await Account(authorization);var manager=actor.Role=="coordinator";
        Require(manager || type is "updateProfile" or "readNotifications" or "generateReport","forbidden",403);
        await using var transaction=await db.Database.BeginTransactionAsync();
        // PostgreSQL transaction-scoped lock coordinates reservations across API processes.
        // Every operational writer for this organization takes the same lock before reading availability.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({actor.OrganizationId}, 0))");
        var now=DateTimeOffset.UtcNow;JsonNode? result=null;var id=S(p,"id");
        switch(type) {
            case "createShipment": {
                await ValidateAssignment(actor,p);var shipmentId="FT-"+Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
                var shipment=Shipment.Schedule(shipmentId,actor.OrganizationId,actor.Id,S(p,"customerId"),S(p,"vehicleId"),S(p,"driverId"),S(p,"cargo"),N(p,"weight"),S(p,"origin"),S(p,"destination"),D(p,"departure"),D(p,"arrival"),Range(p),true,now);
                db.Shipments.Add(shipment);db.Allocations.Add(new FleetAllocation(shipment.Id,shipment.VehicleId,shipment.DriverId,shipment.Departure,shipment.Arrival));await Notify(shipment,"created",now);result=Object(new{shipment.Id});break;
            }
            case "updateShipment": {
                var shipment=await FindShipment(actor,id);await ValidateAssignment(actor,p,id);
                shipment.Update((long)N(p,"version"),S(p,"customerId"),S(p,"vehicleId"),S(p,"driverId"),S(p,"cargo"),N(p,"weight"),S(p,"origin"),S(p,"destination"),D(p,"departure"),D(p,"arrival"),Range(p),actor.Id,now);
                var allocation=await db.Allocations.SingleAsync(x=>x.ShipmentId==id);allocation.Reschedule(shipment.VehicleId,shipment.DriverId,shipment.Departure,shipment.Arrival);result=Object(new{shipment.Id});break;
            }
            case "transition": {
                var shipment=await FindShipment(actor,id);var next=S(p,"status");shipment.Transition((long)N(p,"version"),next,S(p,"note"),actor.Id,now);
                if(next=="in-transit")db.Monitorings.Add(new ShipmentMonitoring(id,new MonitoredRange(shipment.Range.MinTemp,shipment.Range.MaxTemp,shipment.Range.MinHumidity,shipment.Range.MaxHumidity,shipment.Range.ToleranceMinutes),now));
                if(next is "cancelled" or "delivered") { var allocation=await db.Allocations.SingleOrDefaultAsync(x=>x.ShipmentId==id);allocation?.Release(now);var monitoring=await db.Monitorings.SingleOrDefaultAsync(x=>x.ShipmentId==id);monitoring?.Stop(now); }
                await Notify(shipment,next,now);result=Object(new{shipment.Id});break;
            }
            case "deleteShipment": {
                var shipment=await FindShipment(actor,id);var hasReadings=await db.Monitorings.AnyAsync(x=>x.ShipmentId==id && x.Readings.Any());shipment.CheckDeletion((long)N(p,"version"),hasReadings);db.Shipments.Remove(shipment);result=JsonValue.Create(id);break;
            }
            case "addReading": {
                var shipment=await FindShipment(actor,id);Require(shipment.Status=="in-transit","readingsTransitOnly");
                var monitoring=await db.Monitorings.Include(x=>x.Readings).Include(x=>x.Positions).SingleAsync(x=>x.ShipmentId==id);var at=string.IsNullOrEmpty(S(p,"at"))?now:D(p,"at");
                var reading=monitoring.Record(N(p,"temperature"),N(p,"humidity"),N(p,"lat"),N(p,"lng"),at,now);
                var active=await db.Alerts.Where(x=>x.ShipmentId==id && x.Status!="resolved").ToListAsync();
                foreach(var (kind,value,min,max) in new[]{("temperature",reading.Temperature,monitoring.MinTemp,monitoring.MaxTemp),("humidity",reading.Humidity,monitoring.MinHumidity,monitoring.MaxHumidity)}) {
                    if(value>=min && value<=max)continue;
                    var alert=active.FirstOrDefault(x=>x.Type==kind);
                    if(alert is null){alert=new Alert(id,kind,value,reading.Id,reading.At);db.Alerts.Add(alert);active.Add(alert);await Notify(shipment,"alert",now);}
                    alert.Observe(value,reading.At,monitoring.ToleranceMinutes);
                }
                foreach(var alert in active){alert.TryResolveThermal(new RecoveryEvidence(reading.Id,reading.At,reading.WithinRange));alert.TryRestoreSignal(reading.At,now,monitoring.MaxSilenceMinutes);}
                shipment.Audit(actor.Id,"sampleReading","Demonstration reading recorded",now);result=Object(new{reading.Id,reading.At,reading.Temperature,reading.Humidity,lat=N(p,"lat"),lng=N(p,"lng")});break;
            }
            case "checkSignal": {
                var shipment=await FindShipment(actor,id);var monitoring=await db.Monitorings.SingleOrDefaultAsync(x=>x.ShipmentId==id) ?? throw new ApiError("notFound",404);monitoring.EvaluateSignal(now);
                if(monitoring.SignalStatus=="lost" && !await db.Alerts.AnyAsync(x=>x.ShipmentId==id && x.Type=="signal" && x.Status!="resolved")){db.Alerts.Add(new Alert(id,"signal",null,null,now));await Notify(shipment,"alert",now);}result=Object(new{signalStatus=monitoring.SignalStatus});break;
            }
            case "recordIncident": {
                var shipment=await FindShipment(actor,id);var note=Rules.Text(S(p,"note"),1000,"requiredAction");Require(note.Length>=8,"requiredAction");db.Incidents.Add(new Incident{ShipmentId=id,ActorId=actor.Id,Note=note,At=now});shipment.Audit(actor.Id,"incident",note,now);result=Object(new{shipment.Id});break;
            }
            case "recordCorrectiveAction": case "resolveAlert": {
                var alert=await db.Alerts.Include(x=>x.Actions).SingleOrDefaultAsync(x=>x.Id==id) ?? throw new ApiError("notFound",404);var shipment=await FindShipment(actor,alert.ShipmentId);
                alert.Acknowledge(actor.Id,S(p,"note"),B(p,"notifyClient"),now);shipment.Audit(actor.Id,"correctiveAction",S(p,"note"),now);if(B(p,"notifyClient"))await Notify(shipment,"correctiveAction",now,true);result=Object(new{alert.Id});break;
            }
            case "saveVehicle": {
                var vehicle=id.Length==0?null:await db.Vehicles.SingleOrDefaultAsync(x=>x.Id==id && x.OrganizationId==actor.OrganizationId);Require(id.Length==0 || vehicle is not null,"notFound",404);if(vehicle is not null)Require(!await db.Allocations.AnyAsync(x=>x.VehicleId==id && x.Status=="reserved"),"assignedResource",409);
                var plate=S(p,"plate").Trim().ToUpperInvariant();Require(!await db.Vehicles.AnyAsync(x=>x.Id!=id && x.Plate==plate),"duplicateVehicle",409);
                if(vehicle is null){vehicle=new Vehicle("vehicle-"+Id(),actor.OrganizationId,plate,N(p,"capacity"),B(p,"enabled",true));db.Vehicles.Add(vehicle);}else vehicle.Update(plate,N(p,"capacity"),B(p,"enabled",true));
                if(!string.IsNullOrWhiteSpace(S(p,"sensor"))) {
                    var serial=S(p,"sensor").Trim().ToUpperInvariant();var sensor=await db.Sensors.SingleOrDefaultAsync(x=>x.SerialCode==serial);Require(sensor is null || sensor.OrganizationId==actor.OrganizationId && (sensor.VehicleId is null || sensor.VehicleId==vehicle.Id),"duplicateVehicle",409);
                    var previous=await db.Sensors.SingleOrDefaultAsync(x=>x.VehicleId==vehicle.Id);if(previous is not null && previous.SerialCode!=serial)previous.Update(previous.SerialCode,null,previous.Enabled);
                    if(sensor is null)db.Sensors.Add(new Sensor("sensor-"+Id(),actor.OrganizationId,serial,vehicle.Id,true));else sensor.Update(serial,vehicle.Id,sensor.Enabled);
                }
                result=Object(new{vehicle.Id});break;
            }
            case "saveDriver": {
                var driver=id.Length==0?null:await db.Drivers.SingleOrDefaultAsync(x=>x.Id==id && x.OrganizationId==actor.OrganizationId);Require(id.Length==0 || driver is not null,"notFound",404);if(driver is not null)Require(!await db.Allocations.AnyAsync(x=>x.DriverId==id && x.Status=="reserved"),"assignedResource",409);
                var license=S(p,"license").Trim().ToUpperInvariant();Require(!await db.Drivers.AnyAsync(x=>x.Id!=id && x.License==license),"duplicateDriver",409);
                if(driver is null){driver=new Driver("driver-"+Id(),actor.OrganizationId,S(p,"name"),license,B(p,"enabled",true));db.Drivers.Add(driver);}else driver.Update(S(p,"name"),license,B(p,"enabled",true));result=Object(new{driver.Id});break;
            }
            case "saveSensor": {
                var sensor=id.Length==0?null:await db.Sensors.SingleOrDefaultAsync(x=>x.Id==id && x.OrganizationId==actor.OrganizationId);Require(id.Length==0 || sensor is not null,"notFound",404);
                var serial=S(p,"serialCode").Trim().ToUpperInvariant();var vehicleId=string.IsNullOrWhiteSpace(S(p,"vehicleId"))?null:S(p,"vehicleId");Require(!await db.Sensors.AnyAsync(x=>x.Id!=id && x.SerialCode==serial),"duplicateSensor",409);
                if(vehicleId is not null){Require(await db.Vehicles.AnyAsync(x=>x.Id==vehicleId && x.OrganizationId==actor.OrganizationId),"notFound",404);Require(!await db.Sensors.AnyAsync(x=>x.Id!=id && x.VehicleId==vehicleId),"sensorAssigned",409);Require(!await db.Allocations.AnyAsync(x=>x.VehicleId==vehicleId && x.Status=="reserved"),"assignedResource",409);}
                if(sensor?.VehicleId is not null)Require(!await db.Allocations.AnyAsync(x=>x.VehicleId==sensor.VehicleId && x.Status=="reserved"),"assignedResource",409);
                if(sensor is null){sensor=new Sensor("sensor-"+Id(),actor.OrganizationId,serial,vehicleId,B(p,"enabled",true));db.Sensors.Add(sensor);}else sensor.Update(serial,vehicleId,B(p,"enabled",true));result=Object(new{sensor.Id});break;
            }
            case "updateProfile": {
                var email=S(p,"email").Trim().ToLowerInvariant();Require(!await db.Users.AnyAsync(x=>x.Id!=actor.Id && x.Email==email),"duplicateProfile",409);actor.Update(S(p,"name"),email,S(p,"organization"),string.IsNullOrEmpty(S(p,"language"))?null:S(p,"language"));result=Profile(actor);break;
            }
            case "readNotifications": {
                var notifications=await db.Notifications.Where(x=>x.RecipientId==actor.Id && x.ReadAt==null).ToListAsync();foreach(var notice in notifications)notice.MarkRead(actor.Id,now);break;
            }
            case "generateReport": {
                var shipment=await FindShipment(actor,id);Require(shipment.Status=="delivered","reportDeliveredOnly");var state=await State(actor);var snapshot=new JsonObject{["shipment"]=state["shipments"]!.AsArray().First(x=>S(x,"id")==id)!.DeepClone(),["alerts"]=new JsonArray(state["alerts"]!.AsArray().Where(x=>S(x,"shipmentId")==id).Select(x=>x!.DeepClone()).ToArray()),["generatedAt"]=now.ToString("O"),["dataSource"]="demonstration",["notice"]="Demonstration records; gaps are not evidence of thermal compliance."};var report=new ThermalReport{ShipmentId=id,GeneratedBy=actor.Id,At=now,Snapshot=snapshot.ToJsonString(Json)};db.Reports.Add(report);result=Object(new{report.Id,report.ShipmentId,report.At});break;
            }
            default:throw new ApiError("unknownCommand");
        }
        await db.SaveChangesAsync();await transaction.CommitAsync();
        var stateAfter=await State(actor);
        if(result is JsonObject resultObject && resultObject["id"] is not null) {
            var resultId=S(resultObject,"id");string? collection=type switch {"createShipment" or "updateShipment" or "transition" or "recordIncident"=>"shipments","saveVehicle"=>"vehicles","saveDriver"=>"drivers","saveSensor"=>"sensors","recordCorrectiveAction" or "resolveAlert"=>"alerts",_=>null};
            if(collection is not null) result=stateAfter[collection]!.AsArray().FirstOrDefault(x=>S(x,"id")==resultId)?.DeepClone() ?? result;
        }
        return new JsonObject{["profile"]=Profile(actor),["state"]=stateAfter,["result"]=result};
    }
    public async Task<JsonObject> ReadReport(string authorization,string reportId) {
        var actor=await Account(authorization);var report=await db.Reports.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==reportId) ?? throw new ApiError("notFound",404);await FindShipment(actor,report.ShipmentId);return JsonNode.Parse(report.Snapshot)!.AsObject();
    }
}
