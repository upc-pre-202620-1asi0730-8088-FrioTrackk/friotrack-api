# FríoTrack API

Independent Web Services source for FríoTrack. This ASP.NET Core 10 API authenticates accounts, authorizes each request and processes refrigerated-shipment operations. The shipments and telemetry in `seed.json` are fictional demonstration data.

This repository is the canonical API source. A compatible transition copy remains under `api/` in [friotrack-frontend](https://github.com/upc-pre-202620-1asi0730-8088-FrioTrackk/friotrack-frontend) so existing commands and deployment scripts continue to work. Changes must be kept consistent during the transition. The [project report](https://github.com/upc-pre-202620-1asi0730-8088-FrioTrackk/report-friotrack) distinguishes this implementation from the target backend design.

## Local execution

Requires the **.NET 10 SDK**. Set `FRIOTRACK_COORDINATOR_PASSWORD` and `FRIOTRACK_CLIENT_PASSWORD` to passwords of at least 12 characters in your local environment. They initialize the account store only on its first run; they do not reset an existing store.

From this repository's root:

```powershell
dotnet run --project FrioTrack.Api.csproj --urls http://localhost:5080
```

The initial account emails are `coordinador@friotrack.app` and `cliente@friotrack.app`. Supply their passwords privately through the environment. Do not commit a populated database or credentials file.

| Configuration | Purpose and default |
| --- | --- |
| `FRIOTRACK_COORDINATOR_PASSWORD` | Initial coordinator password, required when creating the store |
| `FRIOTRACK_CLIENT_PASSWORD` | Initial cargo-client password, required when creating the store |
| `FRIOTRACK_DATA_FILE` | Private state file; defaults to `HOME/data/friotrack.json` or local `data/friotrack.json` |
| `FRIOTRACK_ORIGINS` | Comma-separated CORS origins; defaults to `http://localhost:5173,http://127.0.0.1:5173` |

The frontend's API base URL is `http://localhost:5080/api` for this local configuration. Production requires HTTPS and explicit allowed origins. Publishing source changes here does not deploy or change the existing Azure service.

## REST contract

| Endpoint | Authorization | Purpose |
| --- | --- | --- |
| GET `/api/health` | Public | Service availability |
| POST `/api/auth/login` | Email and password | Start an eight-hour session |
| POST `/api/auth/register` | Public; cargo clients only | Register and sign in |
| GET `/api/workspace` | Bearer session token | Profile and authorized workspace |
| POST `/api/commands` | Bearer session token | Validated operation and updated workspace |
| POST `/api/auth/logout` | Bearer session token | Revoke the current session |

The service uses opaque session tokens, not JWT. Passwords use PBKDF2-SHA256 with random individual salts and 210,000 iterations. Tokens contain 256 random bits; only SHA-256 digests are persisted. Authentication endpoints allow 15 requests per minute per remote IP. Public registration cannot create coordinators and profile changes cannot change the account role.

Cargo clients receive only their own profile, assigned shipments, associated resources, alerts and permitted notifications. Coordinator commands validate resource availability, capacity, thermal ranges, dates and record version. Corrective actions acknowledge alerts; resolution requires a later reading within both configured ranges.

## Code structure

| Location | Responsibility |
| --- | --- |
| `Program.cs` | Composition, dependency injection, CORS and rate limiting |
| `Interfaces/` | HTTP endpoint mapping and error responses |
| `Shared/Application/WorkspaceService.cs` | Session, visibility and serialized workspace use cases |
| `Shared/Application/WorkspaceCommands.cs` | Current JSON command validation and processing |
| `Shared/Application/Ports/IWorkspacePersistence.cs` | Persistence contract consumed by application |
| `Shared/Infrastructure/JsonWorkspacePersistence.cs` | Initial state and replacement file writes |
| `Iam/Application/Ports/IPasswordHasher.cs` | Password hashing contract |
| `Iam/Infrastructure/Pbkdf2PasswordHasher.cs` | PBKDF2 implementation |

The application depends on ports and the composition root supplies adapters. The singleton application service serializes operations with a process lock, while HTTP requests and file paths are handled at their respective boundaries.

The store is a JSON file for a single course-demo instance. The technical separation does not implement the target DDD aggregate classes, EF Core/PostgreSQL repositories, organization tenancy, physical sensor ingestion, email recovery, OpenAPI or event dispatch. These remain backend evolution work.

## Verification

Requires Node.js 22 or newer for the standalone integration script; no npm dependencies are needed.

```powershell
dotnet publish FrioTrack.Api.csproj -c Release -o output/api-publish --no-self-contained
node scripts/test-api.mjs
```

The suite starts a temporary service on an available loopback port with a fresh private store and random disposable passwords. Its 26 checks cover CORS, credentials, account registration, role escalation, client isolation, persistence, shipment commands, alerts, revoked sessions and rate limiting. It writes `tmp/local-api-verification.json` and removes the temporary store. It has no remote or Azure execution mode.

CI runs compilation and the local suite for pull requests and changes to `main` and `develop`. See [contribution workflow](CONTRIBUTING.md) for branches and release conventions.
