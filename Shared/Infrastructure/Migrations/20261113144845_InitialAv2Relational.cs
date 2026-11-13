using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrioTrack.Api.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialAv2Relational : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "drivers",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    organization_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    license = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_drivers", x => x.id);
                    table.ForeignKey(
                        name: "FK_drivers_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    organization_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    email = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    company_name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    role = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    language = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    password_salt = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.CheckConstraint("ck_user_role", "role IN ('coordinator','cargo-client')");
                    table.ForeignKey(
                        name: "FK_users_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    organization_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    plate = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    capacity = table.Column<decimal>(type: "numeric(12,5)", precision: 12, scale: 5, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicles", x => x.id);
                    table.CheckConstraint("ck_vehicle_capacity", "capacity > 0 AND capacity <= 100");
                    table.ForeignKey(
                        name: "FK_vehicles_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "auth_sessions",
                columns: table => new
                {
                    token_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auth_sessions", x => x.token_hash);
                    table.ForeignKey(
                        name: "FK_auth_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_password_reset_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sensors",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    organization_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    serial_code = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    vehicle_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensors", x => x.id);
                    table.ForeignKey(
                        name: "FK_sensors_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sensors_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shipments",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    organization_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    coordinator_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    customer_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    vehicle_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    driver_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cargo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    weight = table.Column<decimal>(type: "numeric(12,5)", precision: 12, scale: 5, nullable: false),
                    origin = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    destination = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    departure = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    arrival = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    min_temp = table.Column<decimal>(type: "numeric(12,5)", precision: 12, scale: 5, nullable: false),
                    max_temp = table.Column<decimal>(type: "numeric(12,5)", precision: 12, scale: 5, nullable: false),
                    min_humidity = table.Column<decimal>(type: "numeric(12,5)", precision: 12, scale: 5, nullable: false),
                    max_humidity = table.Column<decimal>(type: "numeric(12,5)", precision: 12, scale: 5, nullable: false),
                    tolerance_minutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipments", x => x.id);
                    table.CheckConstraint("ck_shipment_dates", "arrival > departure");
                    table.CheckConstraint("ck_shipment_range", "min_temp < max_temp AND min_humidity < max_humidity AND min_humidity >= 0 AND max_humidity <= 100 AND tolerance_minutes >= 0");
                    table.CheckConstraint("ck_shipment_status", "status IN ('scheduled','in-transit','delivered','cancelled')");
                    table.CheckConstraint("ck_shipment_weight", "weight > 0");
                    table.ForeignKey(
                        name: "FK_shipments_drivers_driver_id",
                        column: x => x.driver_id,
                        principalTable: "drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shipments_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shipments_users_coordinator_id",
                        column: x => x.coordinator_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shipments_users_customer_id",
                        column: x => x.customer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shipments_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fleet_allocations",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    shipment_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    vehicle_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    driver_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fleet_allocations", x => x.id);
                    table.CheckConstraint("ck_allocation_interval", "until > \"from\"");
                    table.ForeignKey(
                        name: "FK_fleet_allocations_drivers_driver_id",
                        column: x => x.driver_id,
                        principalTable: "drivers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fleet_allocations_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fleet_allocations_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "incidents",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    shipment_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    actor_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incidents", x => x.id);
                    table.ForeignKey(
                        name: "FK_incidents_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_incidents_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    shipment_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    recipient_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_notifications_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notifications_users_recipient_id",
                        column: x => x.recipient_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shipment_history",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    shipment_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    actor_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    action = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_shipment_history_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shipment_history_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shipment_monitorings",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    shipment_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    min_temp = table.Column<decimal>(type: "numeric(12,5)", precision: 12, scale: 5, nullable: false),
                    max_temp = table.Column<decimal>(type: "numeric(12,5)", precision: 12, scale: 5, nullable: false),
                    min_humidity = table.Column<decimal>(type: "numeric(12,5)", precision: 12, scale: 5, nullable: false),
                    max_humidity = table.Column<decimal>(type: "numeric(12,5)", precision: 12, scale: 5, nullable: false),
                    tolerance_minutes = table.Column<int>(type: "integer", nullable: false),
                    max_silence_minutes = table.Column<int>(type: "integer", nullable: false),
                    signal_status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    stopped_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_reading_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipment_monitorings", x => x.id);
                    table.CheckConstraint("ck_monitoring_range", "min_temp < max_temp AND min_humidity < max_humidity AND min_humidity >= 0 AND max_humidity <= 100");
                    table.ForeignKey(
                        name: "FK_shipment_monitorings_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "thermal_reports",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    shipment_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    generated_by = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    snapshot = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_thermal_reports", x => x.id);
                    table.ForeignKey(
                        name: "FK_thermal_reports_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_thermal_reports_users_generated_by",
                        column: x => x.generated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sensor_readings",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    monitoring_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    temperature = table.Column<decimal>(type: "numeric(12,5)", precision: 12, scale: 5, nullable: false),
                    humidity = table.Column<decimal>(type: "numeric(12,5)", precision: 12, scale: 5, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    within_range = table.Column<bool>(type: "boolean", nullable: false),
                    is_demonstration = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_readings", x => x.id);
                    table.CheckConstraint("ck_reading_values", "humidity >= 0 AND humidity <= 100 AND temperature >= -50 AND temperature <= 50");
                    table.ForeignKey(
                        name: "FK_sensor_readings_shipment_monitorings_monitoring_id",
                        column: x => x.monitoring_id,
                        principalTable: "shipment_monitorings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "alerts",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    shipment_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    reading = table.Column<decimal>(type: "numeric(12,5)", precision: 12, scale: 5, nullable: true),
                    trigger_reading_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_deviation_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    severity = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    recovery_reading_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerts", x => x.id);
                    table.CheckConstraint("ck_alert_status", "status IN ('active','acknowledged','resolved')");
                    table.ForeignKey(
                        name: "FK_alerts_sensor_readings_trigger_reading_id",
                        column: x => x.trigger_reading_id,
                        principalTable: "sensor_readings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_alerts_shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "position_reports",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    monitoring_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reading_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(12,5)", precision: 12, scale: 5, nullable: false),
                    longitude = table.Column<decimal>(type: "numeric(12,5)", precision: 12, scale: 5, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_position_reports", x => x.id);
                    table.CheckConstraint("ck_position_coordinates", "latitude >= -90 AND latitude <= 90 AND longitude >= -180 AND longitude <= 180");
                    table.ForeignKey(
                        name: "FK_position_reports_sensor_readings_reading_id",
                        column: x => x.reading_id,
                        principalTable: "sensor_readings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_position_reports_shipment_monitorings_monitoring_id",
                        column: x => x.monitoring_id,
                        principalTable: "shipment_monitorings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "corrective_actions",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    alert_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    actor_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    notify_client = table.Column<bool>(type: "boolean", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_corrective_actions", x => x.id);
                    table.ForeignKey(
                        name: "FK_corrective_actions_alerts_alert_id",
                        column: x => x.alert_id,
                        principalTable: "alerts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_corrective_actions_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alerts_shipment_id_status",
                table: "alerts",
                columns: new[] { "shipment_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_alerts_trigger_reading_id",
                table: "alerts",
                column: "trigger_reading_id");

            migrationBuilder.CreateIndex(
                name: "IX_auth_sessions_expires_at",
                table: "auth_sessions",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_auth_sessions_user_id",
                table: "auth_sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_corrective_actions_actor_id",
                table: "corrective_actions",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "IX_corrective_actions_alert_id",
                table: "corrective_actions",
                column: "alert_id");

            migrationBuilder.CreateIndex(
                name: "IX_drivers_license",
                table: "drivers",
                column: "license",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_drivers_organization_id",
                table: "drivers",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_fleet_allocations_driver_id_from_until",
                table: "fleet_allocations",
                columns: new[] { "driver_id", "from", "until" });

            migrationBuilder.CreateIndex(
                name: "IX_fleet_allocations_shipment_id",
                table: "fleet_allocations",
                column: "shipment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fleet_allocations_vehicle_id_from_until",
                table: "fleet_allocations",
                columns: new[] { "vehicle_id", "from", "until" });

            migrationBuilder.CreateIndex(
                name: "IX_incidents_actor_id",
                table: "incidents",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_shipment_id",
                table: "incidents",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_recipient_id_read_at",
                table: "notifications",
                columns: new[] { "recipient_id", "read_at" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_shipment_id",
                table: "notifications",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_token_hash",
                table: "password_reset_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_user_id",
                table: "password_reset_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_position_reports_monitoring_id",
                table: "position_reports",
                column: "monitoring_id");

            migrationBuilder.CreateIndex(
                name: "IX_position_reports_reading_id",
                table: "position_reports",
                column: "reading_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sensor_readings_monitoring_id_at",
                table: "sensor_readings",
                columns: new[] { "monitoring_id", "at" });

            migrationBuilder.CreateIndex(
                name: "IX_sensors_organization_id",
                table: "sensors",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_sensors_serial_code",
                table: "sensors",
                column: "serial_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sensors_vehicle_id",
                table: "sensors",
                column: "vehicle_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipment_history_actor_id",
                table: "shipment_history",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_history_shipment_id",
                table: "shipment_history",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_monitorings_shipment_id",
                table: "shipment_monitorings",
                column: "shipment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shipments_coordinator_id",
                table: "shipments",
                column: "coordinator_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_customer_id",
                table: "shipments",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_driver_id",
                table: "shipments",
                column: "driver_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipments_organization_id_status",
                table: "shipments",
                columns: new[] { "organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_shipments_vehicle_id",
                table: "shipments",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "IX_thermal_reports_generated_by",
                table: "thermal_reports",
                column: "generated_by");

            migrationBuilder.CreateIndex(
                name: "IX_thermal_reports_shipment_id",
                table: "thermal_reports",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_organization_id",
                table: "users",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_organization_id",
                table: "vehicles",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_plate",
                table: "vehicles",
                column: "plate",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auth_sessions");

            migrationBuilder.DropTable(
                name: "corrective_actions");

            migrationBuilder.DropTable(
                name: "fleet_allocations");

            migrationBuilder.DropTable(
                name: "incidents");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropTable(
                name: "position_reports");

            migrationBuilder.DropTable(
                name: "sensors");

            migrationBuilder.DropTable(
                name: "shipment_history");

            migrationBuilder.DropTable(
                name: "thermal_reports");

            migrationBuilder.DropTable(
                name: "alerts");

            migrationBuilder.DropTable(
                name: "sensor_readings");

            migrationBuilder.DropTable(
                name: "shipment_monitorings");

            migrationBuilder.DropTable(
                name: "shipments");

            migrationBuilder.DropTable(
                name: "drivers");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "vehicles");

            migrationBuilder.DropTable(
                name: "organizations");
        }
    }
}
