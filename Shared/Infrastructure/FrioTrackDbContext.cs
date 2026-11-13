using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using FrioTrack.Api.ShipmentManagement.Domain;
using FrioTrack.Api.Fleet.Domain;
using FrioTrack.Api.Monitoring.Domain;
using FrioTrack.Api.AlertsReporting.Domain;
using FrioTrack.Api.Iam.Domain;
using FrioTrack.Api.Shared.Domain;

namespace FrioTrack.Api.Shared.Infrastructure;

public sealed class FrioTrackDbContext(DbContextOptions<FrioTrackDbContext> options) : DbContext(options) {
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentHistoryEvent> ShipmentHistory => Set<ShipmentHistoryEvent>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Sensor> Sensors => Set<Sensor>();
    public DbSet<FleetAllocation> Allocations => Set<FleetAllocation>();
    public DbSet<ShipmentMonitoring> Monitorings => Set<ShipmentMonitoring>();
    public DbSet<SensorReading> Readings => Set<SensorReading>();
    public DbSet<PositionReport> Positions => Set<PositionReport>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<CorrectiveAction> CorrectiveActions => Set<CorrectiveAction>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<ThermalReport> Reports => Set<ThermalReport>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<AuthSession> Sessions => Set<AuthSession>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder model) {
        model.Entity<Organization>().ToTable("organizations").HasKey(x => x.Id);
        model.Entity<UserAccount>(b => {
            b.ToTable("users", t => t.HasCheckConstraint("ck_user_role", "role IN ('coordinator','cargo-client')")); b.HasKey(x => x.Id); b.HasIndex(x => x.Email).IsUnique(); b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AuthSession>(b => { b.ToTable("auth_sessions"); b.HasKey(x => x.TokenHash); b.HasIndex(x => x.ExpiresAt); b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); });
        model.Entity<PasswordResetToken>(b => { b.ToTable("password_reset_tokens"); b.HasKey(x => x.Id); b.HasIndex(x => x.TokenHash).IsUnique(); b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); });
        model.Entity<Vehicle>(b => { b.ToTable("vehicles", t => t.HasCheckConstraint("ck_vehicle_capacity", "capacity > 0 AND capacity <= 100")); b.HasKey(x => x.Id); b.HasIndex(x => x.Plate).IsUnique(); b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict); });
        model.Entity<Driver>(b => { b.ToTable("drivers"); b.HasKey(x => x.Id); b.HasIndex(x => x.License).IsUnique(); b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict); });
        model.Entity<Sensor>(b => { b.ToTable("sensors"); b.HasKey(x => x.Id); b.HasIndex(x => x.SerialCode).IsUnique(); b.HasIndex(x => x.VehicleId).IsUnique(); b.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict); b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict); });
        model.Entity<Shipment>(b => {
            b.ToTable("shipments", t => { t.HasCheckConstraint("ck_shipment_dates", "arrival > departure"); t.HasCheckConstraint("ck_shipment_weight", "weight > 0"); t.HasCheckConstraint("ck_shipment_range", "min_temp < max_temp AND min_humidity < max_humidity AND min_humidity >= 0 AND max_humidity <= 100 AND tolerance_minutes >= 0"); t.HasCheckConstraint("ck_shipment_status", "status IN ('scheduled','in-transit','delivered','cancelled')"); });
            b.HasKey(x => x.Id); b.Property(x => x.Version).IsConcurrencyToken(); b.HasIndex(x => new { x.OrganizationId, x.Status }); b.HasIndex(x => x.CustomerId);
            b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict); b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CoordinatorId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict); b.HasOne<Driver>().WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
            b.OwnsOne(x => x.Range, range => { range.Property<string>("ShipmentId").HasColumnName("id"); range.Property(x => x.MinTemp).HasColumnName("min_temp"); range.Property(x => x.MaxTemp).HasColumnName("max_temp"); range.Property(x => x.MinHumidity).HasColumnName("min_humidity"); range.Property(x => x.MaxHumidity).HasColumnName("max_humidity"); range.Property(x => x.ToleranceMinutes).HasColumnName("tolerance_minutes"); });
            b.Navigation(x => x.Range).IsRequired(); b.HasMany(x => x.History).WithOne().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<ShipmentHistoryEvent>(b => { b.ToTable("shipment_history"); b.HasKey(x => x.Id); b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict); });
        model.Entity<FleetAllocation>(b => { b.ToTable("fleet_allocations", t => t.HasCheckConstraint("ck_allocation_interval", "until > \"from\"")); b.HasKey(x => x.Id); b.HasIndex(x => x.ShipmentId).IsUnique(); b.HasIndex(x => new { x.VehicleId, x.From, x.Until }); b.HasIndex(x => new { x.DriverId, x.From, x.Until }); b.HasOne<Shipment>().WithOne().HasForeignKey<FleetAllocation>(x => x.ShipmentId).OnDelete(DeleteBehavior.Cascade); b.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict); b.HasOne<Driver>().WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict); });
        model.Entity<ShipmentMonitoring>(b => { b.ToTable("shipment_monitorings", t => t.HasCheckConstraint("ck_monitoring_range", "min_temp < max_temp AND min_humidity < max_humidity AND min_humidity >= 0 AND max_humidity <= 100")); b.HasKey(x => x.Id); b.HasIndex(x => x.ShipmentId).IsUnique(); b.HasOne<Shipment>().WithOne().HasForeignKey<ShipmentMonitoring>(x => x.ShipmentId).OnDelete(DeleteBehavior.Cascade); b.HasMany(x => x.Readings).WithOne().HasForeignKey(x => x.MonitoringId).OnDelete(DeleteBehavior.Cascade); b.HasMany(x => x.Positions).WithOne().HasForeignKey(x => x.MonitoringId).OnDelete(DeleteBehavior.Cascade); });
        model.Entity<SensorReading>(b => { b.ToTable("sensor_readings", t => t.HasCheckConstraint("ck_reading_values", "humidity >= 0 AND humidity <= 100 AND temperature >= -50 AND temperature <= 50")); b.HasKey(x => x.Id); b.HasIndex(x => new { x.MonitoringId, x.At }); });
        model.Entity<PositionReport>(b => { b.ToTable("position_reports", t => t.HasCheckConstraint("ck_position_coordinates", "latitude >= -90 AND latitude <= 90 AND longitude >= -180 AND longitude <= 180")); b.HasKey(x => x.Id); b.HasOne<SensorReading>().WithOne().HasForeignKey<PositionReport>(x => x.ReadingId).OnDelete(DeleteBehavior.Cascade); });
        model.Entity<Alert>(b => { b.ToTable("alerts", t => t.HasCheckConstraint("ck_alert_status", "status IN ('active','acknowledged','resolved')")); b.HasKey(x => x.Id); b.HasIndex(x => new { x.ShipmentId, x.Status }); b.HasOne<Shipment>().WithMany().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Cascade); b.HasOne<SensorReading>().WithMany().HasForeignKey(x => x.TriggerReadingId).OnDelete(DeleteBehavior.Restrict); b.HasMany(x => x.Actions).WithOne().HasForeignKey(x => x.AlertId).OnDelete(DeleteBehavior.Cascade); });
        model.Entity<CorrectiveAction>(b => { b.ToTable("corrective_actions"); b.HasKey(x => x.Id); b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict); });
        model.Entity<Notification>(b => { b.ToTable("notifications"); b.HasKey(x => x.Id); b.HasIndex(x => new { x.RecipientId, x.ReadAt }); b.HasOne<Shipment>().WithMany().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Cascade); b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Restrict); });
        model.Entity<Incident>(b => { b.ToTable("incidents"); b.HasKey(x => x.Id); b.HasOne<Shipment>().WithMany().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Cascade); b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict); });
        model.Entity<ThermalReport>(b => { b.ToTable("thermal_reports"); b.HasKey(x => x.Id); b.Property(x => x.Snapshot).HasColumnType("jsonb"); b.HasOne<Shipment>().WithMany().HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Cascade); b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.GeneratedBy).OnDelete(DeleteBehavior.Restrict); });
        foreach (var entity in model.Model.GetEntityTypes()) foreach (var property in entity.GetProperties()) {
            if (property.GetColumnName() == property.Name) property.SetColumnName(Regex.Replace(property.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());
            if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?)) { property.SetPrecision(12); property.SetScale(5); }
            if (property.ClrType == typeof(string) && property.Name != "Snapshot") property.SetMaxLength(property.Name is "Id" or "TokenHash" || property.Name.EndsWith("Id") ? 100 : 2000);
        }
    }
}

public sealed class EfAggregateRepository<T>(FrioTrackDbContext db) : IAggregateRepository<T> where T : class {
    public async Task<T?> FindAsync(string id, CancellationToken cancellationToken = default) => await db.Set<T>().FindAsync([id], cancellationToken);
    public void Add(T aggregate) => db.Set<T>().Add(aggregate);
    public void Remove(T aggregate) => db.Set<T>().Remove(aggregate);
}
