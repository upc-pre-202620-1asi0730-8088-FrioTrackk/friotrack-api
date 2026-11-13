using Xunit;
using FrioTrack.Api.Shared.Domain;
using FrioTrack.Api.ShipmentManagement.Domain;
using FrioTrack.Api.Monitoring.Domain;
using FrioTrack.Api.AlertsReporting.Domain;
using FrioTrack.Api.Fleet.Domain;
using FrioTrack.Api.Iam.Domain;
using FrioTrack.Api.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FrioTrack.Api.Tests;

public sealed class DomainRulesTests {
    static readonly DateTimeOffset Now = new(2026,11,13,12,0,0,TimeSpan.Zero);
    static Shipment Scheduled(bool reserved=true) => Shipment.Schedule("shipment","org","coordinator","client","vehicle","driver","Avocado",3,"Lima","Ica",Now.AddDays(1),Now.AddDays(2),new ThermalRange(2,6,70,95),reserved,Now);

    [Theory]
    [InlineData(6,6,70,95)]
    [InlineData(8,2,70,95)]
    [InlineData(2,6,95,95)]
    [InlineData(2,6,-1,95)]
    public void Invalid_environmental_intervals_are_rejected(decimal minimum,decimal maximum,decimal humidityMinimum,decimal humidityMaximum) => Assert.Throws<DomainRuleException>(()=>new ThermalRange(minimum,maximum,humidityMinimum,humidityMaximum));

    [Fact] public void Both_environmental_dimensions_must_be_normal() { var range=new ThermalRange(2,6,70,95);Assert.True(range.Contains(4,80));Assert.False(range.Contains(4,99));Assert.False(range.Contains(8,80)); }
    [Fact] public void Scheduling_cannot_confirm_without_a_resource_reservation() => Assert.Throws<DomainRuleException>(()=>Scheduled(false));
    [Fact] public void Stale_version_cannot_transition_and_does_not_change_state() {var shipment=Scheduled();var exception=Assert.Throws<DomainRuleException>(()=>shipment.Transition(0,"in-transit","","coordinator",Now));Assert.Equal("staleShipment",exception.Code);Assert.Equal("scheduled",shipment.Status);Assert.Equal(1,shipment.Version);}
    [Fact] public void In_transit_shipment_cannot_be_cancelled() {var shipment=Scheduled();shipment.Transition(1,"in-transit","","coordinator",Now);Assert.Throws<DomainRuleException>(()=>shipment.Transition(2,"cancelled","Customer cancellation","coordinator",Now));Assert.Equal("in-transit",shipment.Status);}
    [Fact] public void Cancelling_before_transit_requires_a_reason() {var shipment=Scheduled();Assert.Throws<DomainRuleException>(()=>shipment.Transition(1,"cancelled"," ","coordinator",Now));Assert.Equal("scheduled",shipment.Status);}
    [Fact] public void Touching_allocation_intervals_do_not_overlap_and_release_ends_reservation() {var allocation=new FleetAllocation("s","v","d",Now,Now.AddHours(1));Assert.False(allocation.Overlaps(Now.AddHours(1),Now.AddHours(2)));Assert.True(allocation.Overlaps(Now.AddMinutes(30),Now.AddHours(2)));allocation.Release(Now);Assert.False(allocation.Overlaps(Now,Now.AddHours(1)));}
    [Fact] public void Sensor_has_an_independent_lifecycle_and_optional_assignment() {var sensor=new Sensor("sensor","org","serial-1",null,true);Assert.Null(sensor.VehicleId);sensor.Update(sensor.SerialCode,"vehicle",true);Assert.Equal("vehicle",sensor.VehicleId);sensor.Update(sensor.SerialCode,null,false);Assert.Null(sensor.VehicleId);Assert.False(sensor.Enabled);}
    [Fact] public void Corrective_action_acknowledges_without_resolving() {var alert=new Alert("s","temperature",8,"r",Now);alert.Acknowledge("coordinator","Inspected refrigeration",true,Now.AddMinutes(1));Assert.Equal("acknowledged",alert.Status);Assert.Null(alert.ResolvedAt);Assert.Single(alert.Actions);}
    [Fact] public void Recovery_requires_normal_both_ranges_and_a_later_reading_than_latest_deviation() {var alert=new Alert("s","temperature",8,"r1",Now);alert.Observe(9,Now.AddMinutes(5),2);Assert.False(alert.TryResolveThermal(new RecoveryEvidence("r2",Now.AddMinutes(1),true)));Assert.False(alert.TryResolveThermal(new RecoveryEvidence("r3",Now.AddMinutes(6),false)));Assert.Equal("critical",alert.Severity);Assert.True(alert.TryResolveThermal(new RecoveryEvidence("r4",Now.AddMinutes(7),true)));Assert.Equal("resolved",alert.Status);}
    [Fact] public void Restored_signal_does_not_close_a_thermal_excursion() {var thermal=new Alert("s","temperature",8,"r",Now);Assert.False(thermal.TryRestoreSignal(Now.AddMinutes(1),Now.AddMinutes(1),15));Assert.Equal("active",thermal.Status);var signal=new Alert("s","signal",null,null,Now);Assert.True(signal.TryRestoreSignal(Now.AddMinutes(1),Now.AddMinutes(1),15));Assert.Equal("resolved",signal.Status);}
    [Fact] public void Stale_communication_does_not_prove_signal_recovery() {var signal=new Alert("s","signal",null,null,Now);Assert.False(signal.TryRestoreSignal(Now.AddMinutes(1),Now.AddHours(1),15));Assert.Equal("active",signal.Status);}
    [Fact] public void Only_the_notification_recipient_can_mark_it_read() {var notification=new Notification("s","client","alert",Now);Assert.Null(notification.ReadAt);Assert.Throws<DomainRuleException>(()=>notification.MarkRead("other",Now));notification.MarkRead("client",Now.AddMinutes(1));Assert.Equal(Now.AddMinutes(1),notification.ReadAt);}
    [Fact] public void Manual_readings_before_monitoring_started_are_rejected_without_state_changes() {var monitor=new ShipmentMonitoring("s",new MonitoredRange(2,6,70,95,0),Now);var error=Assert.Throws<DomainRuleException>(()=>monitor.Record(8,80,-12,-77,Now.AddSeconds(-1),Now));Assert.Equal("invalidReadingDate",error.Code);Assert.Empty(monitor.Readings);Assert.Empty(monitor.Positions);Assert.Null(monitor.LastReadingAt);Assert.Equal("unknown",monitor.SignalStatus);}
    [Fact] public void Out_of_order_manual_readings_cannot_replace_a_later_normal_reading() {var monitor=new ShipmentMonitoring("s",new MonitoredRange(2,6,70,95,0),Now);var normal=monitor.Record(4,80,-12,-77,Now.AddMinutes(2),Now.AddMinutes(2));Assert.Throws<DomainRuleException>(()=>monitor.Record(8,80,-12,-77,Now.AddMinutes(1),Now.AddMinutes(3)));Assert.Equal(Now.AddMinutes(2),monitor.LastReadingAt);Assert.Same(normal,Assert.Single(monitor.Readings));Assert.True(normal.WithinRange);Assert.Single(monitor.Positions);}
    [Fact] public void Duplicate_manual_reading_timestamps_are_rejected() {var monitor=new ShipmentMonitoring("s",new MonitoredRange(2,6,70,95,0),Now);monitor.Record(4,80,-12,-77,Now,Now);Assert.Throws<DomainRuleException>(()=>monitor.Record(8,80,-12,-77,Now,Now.AddSeconds(1)));Assert.Single(monitor.Readings);Assert.Single(monitor.Positions);}
    [Fact] public void Profile_updates_cannot_change_organization_ownership_or_role() {var user=new UserAccount("u","org","Name","name@example.invalid","Cargo company","cargo-client","hash","salt");user.Update("Changed","new@example.invalid","New company","es");Assert.Equal("cargo-client",user.Role);Assert.Equal("org",user.OrganizationId);Assert.Equal("es",user.Language);}
    [Fact] public void Relational_model_has_normalized_tables_constraints_and_independent_sensor_root() {using var db=new FrioTrackDbContext(new DbContextOptionsBuilder<FrioTrackDbContext>().UseNpgsql("Host=localhost;Database=model_only;Username=model").Options);var model=db.GetService<IDesignTimeModel>().Model;var tables=model.GetEntityTypes().Select(e=>e.GetTableName()).Distinct().ToArray();Assert.Equal(18,tables.Length);Assert.Contains("auth_sessions",tables);Assert.Contains("sensors",tables);Assert.True(model.FindEntityType(typeof(Shipment))!.FindProperty(nameof(Shipment.Version))!.IsConcurrencyToken);Assert.True(model.FindEntityType(typeof(Sensor))!.FindProperty(nameof(Sensor.VehicleId))!.IsNullable);Assert.NotEmpty(model.FindEntityType(typeof(Shipment))!.GetCheckConstraints());}
}
