# FríoTrack API

Canonical Web Services source for FríoTrack AV2. ASP.NET Core 10, Entity Framework Core and PostgreSQL implement the refrigerated shipment workflow. The API defaults to relational persistence; the historical TB1 JSON adapter remains available only through an explicit configuration choice. No existing Azure service or private JSON file is changed by publishing this source.

`seed.json` contains fictional demonstration accounts, shipments, readings and coordinates. Manually recorded readings retain demonstration provenance; this release does not claim physical sensor ingestion, email delivery or verified cold-chain conditions.

## Run locally

Requires the .NET 10 SDK and PostgreSQL 17 (or the Docker Compose setup below). Supply the connection string privately through `FRIOTRACK_DB`; never commit its password. Set `FRIOTRACK_MIGRATE=true` when explicitly applying the included initial migration. For an empty demonstration database, also set `FRIOTRACK_SEED=true` and the two initial passwords before starting:

```powershell
dotnet restore
dotnet run --project FrioTrack.Api.csproj --urls http://localhost:5080
```

The API base URL is `http://localhost:5080/api`; interactive Swagger documentation is at `http://localhost:5080/swagger`, with the OpenAPI document at `/swagger/v1/swagger.json`.

| Configuration | Purpose |
| --- | --- |
| `FRIOTRACK_PERSISTENCE` | `postgres` by default. `json` explicitly selects the historical TB1 adapter. |
| `FRIOTRACK_DB` | PostgreSQL connection string; `ConnectionStrings__FrioTrack` is an alternative. |
| `FRIOTRACK_MIGRATE` | `true` applies the reviewed EF migrations; absent/false does not modify the schema. |
| `FRIOTRACK_SEED` | `true` initializes only an empty relational database with demonstration data. It does not reset accounts or import an existing JSON store. |
| `FRIOTRACK_COORDINATOR_PASSWORD` | Private initial coordinator password, at least 12 characters when seeding. |
| `FRIOTRACK_CLIENT_PASSWORD` | Private initial password for the two fictional cargo-client accounts, at least 12 characters when seeding. |
| `FRIOTRACK_PUBLIC_ORGANIZATION_ID` | Organization receiving public cargo-client registrations; default `demo-organization`. It must already exist. |
| `FRIOTRACK_ORIGINS` | Explicit CORS origins; defaults to `http://localhost:5173,http://127.0.0.1:5173`. |
| `FRIOTRACK_DATA_FILE` | Used only in explicit legacy JSON mode; never read or rewritten by PostgreSQL mode. |

The primary demonstration emails are `coordinador@friotrack.app` and `cliente@friotrack.app`. Their passwords are provided through the environment, not this repository. Public registration always creates a cargo client; it cannot select coordinator privileges or arbitrary organization ownership.

For Docker, copy [.env.example](.env.example) to a private `.env`, supply the required database password and optionally enable demonstration seeding. Then run:

```powershell
docker compose up --build
```

The default Compose URL is `http://localhost:5180/swagger`. PostgreSQL data is stored in a named volume; seeding never overwrites an existing organization. Review migrations and take a backup before updating any non-disposable database. Container configuration is reproducible setup, not evidence that a cloud deployment occurred.

## REST and authentication contract

Swagger documents the typed request schemas, response statuses and field formats. Sign in using `/api/auth/login`, then use **Authorize** with the returned opaque session token. Tokens are not JWT: only their SHA-256 digests are persisted. Passwords use salted PBKDF2-SHA256 with 210,000 iterations; sessions expire after eight hours and logout revokes them. Authentication requests are rate limited.

| Method and resource | Behavior |
| --- | --- |
| GET `/api/health` | Process liveness, service version, persistence mode and demonstration provenance; this does not probe database readiness. |
| POST `/api/auth/login`, `/api/auth/register`, `/api/auth/logout` | Start a server-authorized session, register a cargo client or revoke the session. |
| GET `/api/workspace` | Composed SPA projection `{profile,state}`; cargo clients receive only their own assigned data. |
| GET `/api/shipments`, `/api/shipments/{id}` | List or retrieve authorized shipments. |
| POST `/api/shipments` | Validate the plan and atomically reserve vehicle and driver. |
| PUT `/api/shipments/{id}` | Edit a scheduled shipment and its reservation using the current `version`. |
| DELETE `/api/shipments/{id}?version=...` | Delete only a scheduled shipment with no readings. |
| POST `/api/shipments/{id}/transitions` | Start transit, deliver, or cancel before transit with a reason. |
| POST `/api/shipments/{id}/readings` | Record an authorized demonstration reading and evaluate both environmental intervals. |
| POST `/api/shipments/{id}/signal-evaluations` | Explicitly evaluate communication silence; no background ingestion is claimed. |
| POST `/api/shipments/{id}/incidents` | Register an operational incident with its actor and timestamp. |
| GET `/api/alerts`, `/api/alerts/{id}` | Read alerts for authorized shipments. |
| POST `/api/alerts/{id}/corrective-actions` | Acknowledge attention without resolving an excursion. |
| GET/POST/PUT `/api/vehicles`, `/api/drivers`, `/api/sensors` | Query or manage independent fleet roots; PUT uses `/{id}`. |
| GET/PUT `/api/profiles/me` | Read or update the current profile without changing role or organization ownership. |
| GET `/api/notifications`; POST `/api/notifications/read` | Read and explicitly mark the current recipient's notifications. |
| POST `/api/shipments/{id}/reports` | Explicitly generate an immutable snapshot for a delivered shipment. |
| GET `/api/reports/{id}` | Retrieve the authorized report snapshot as JSON. |

Mutation responses preserve `{profile,state,result}` for the frontend gateway. Authentication responses include `{token,expiresAt,profile,state}`. Required constructor parameters must be present in JSON; omitted numeric fields never become invented zero-valued measurements. Errors preserve a stable `code` and a user-facing `message`: English by default, Spanish with `Accept-Language: es` or `es-419`. Invalid input returns `400`, missing/expired sessions `401`, forbidden roles `403`, unavailable records `404`, conflicting reservations or stale versions `409`, and unavailable database connections `503`. Coordinators and the assigned cargo client can access a shipment's reports; reports do not assert compliance where readings are absent.

## Domain and persistence structure

| Location | Responsibility |
| --- | --- |
| `ShipmentManagement/Domain` | Shipment root, immutable thermal range and owned audit history. |
| `Monitoring/Domain` | Monitoring root, local range snapshot, readings and positions. |
| `Fleet/Domain` | Vehicle, driver, sensor and allocation roots. Sensor may exist unassigned. |
| `AlertsReporting/Domain` | Alert attention/recovery, notifications, incidents and report snapshots. |
| `Iam/Domain` | Organization/account roots, hashed sessions and the planned reset-token structure. |
| `Shared/Domain` | Domain exceptions, invariant helpers and aggregate repository contracts. |
| `Shared/Application/RelationalWorkspaceService.cs` | Scoped transactional use cases, authorization and compatibility projections. |
| `Shared/Infrastructure/FrioTrackDbContext.cs` | Normalized EF model, keys, constraints, indexes and repository implementation. |
| `Shared/Infrastructure/Migrations` | Initial migration and EF model snapshot. |
| `Interfaces` | Typed REST requests, endpoints, documentation and localized error responses. |

The initial schema has **18 tables**: the prior 17-table design plus `auth_sessions` for opaque server sessions. Public identifiers remain bounded strings to preserve existing fixture and UI references; this increment does not claim that historical IDs are UUIDs. Aggregate boundaries are distinct from table boundaries. Cross-context persistence references use IDs, while domain classes have no dependency on EF Core or ASP.NET.

Scheduling and reservation changes share one EF transaction. A PostgreSQL transaction-scoped advisory lock serializes operational writers within an organization before availability is read, preventing double booking across API instances. `Shipment.Version` is also an optimistic concurrency token. Cancellation and delivery release reservations; started shipments cannot be cancelled or deleted. The current implementation favors a small modular monolith over independently deployed context services.

Manual readings must be at or after monitoring started and strictly later than the previous reading; duplicate and historical insertion timestamps are rejected before state changes. A timestamp may be at most one minute ahead of server receipt. Thermal recovery requires a later reading than the latest deviation, normal in both temperature and humidity. A corrective action only acknowledges the alert. Communication restoration is a separate rule and never closes a thermal excursion by itself. Notifications are read only by their recipient's explicit action. Delivered status enables a report request; it does not automatically create a report.

Public onboarding currently targets a configured existing organization. This is not a complete multi-tenant subscription or organization-provisioning product. Password reset delivery, physical IoT authentication, external email, payments and a background event dispatcher remain outside this AV2 increment.

## Verification

```powershell
dotnet test tests/FrioTrack.Api.Tests/FrioTrack.Api.Tests.csproj
dotnet publish FrioTrack.Api.csproj -c Release -o output/api-publish --no-self-contained
node scripts/test-postgres.mjs
node scripts/test-api.mjs
```

Domain tests cover invalid intervals, reservation preconditions, lifecycle/version protection, sensor independence, notification ownership and the distinction between thermal and signal recovery. The PostgreSQL suite requires `FRIOTRACK_TEST_DB` with an isolated **loopback** PostgreSQL administration connection and `psql` in `PATH` (or `FRIOTRACK_PSQL`). It creates and drops only its own randomly named test database, checks actual tables and constraints, exercises REST authorization and transactions, and verifies persistence across an API restart. It refuses cloud hosts.

`scripts/test-api.mjs` exercises only the explicit historical JSON adapter with temporary accounts and storage. Passing it does not certify PostgreSQL. CI runs the unit tests, checks migration consistency, publishes the API and runs both suites against an isolated PostgreSQL service.

## Migration and deployment boundary

The old private JSON database is neither automatically imported nor replaced. A production move requires a confirmed PostgreSQL destination, a reviewed migration and an explicit data/account migration decision. The new source defaults to PostgreSQL, so **do not redeploy it over the old JSON-backed Azure service without configuring the new database and confirming the migration plan**. Source releases and local receipts do not certify an Azure deployment or team acceptance. See [contribution workflow](CONTRIBUTING.md) for branches and release conventions.
