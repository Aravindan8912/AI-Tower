# OMNIX AI Analytic Tower (AAT)

Monitoring-first control tower for one organization, one observer, and one Ollama workload. The observer records execution metadata and host metrics, then uploads them asynchronously. It never sends prompts, completions, system instructions, credentials, or request bodies.

Optimization, approval, and deployment workflows are intentionally not part of this release.

## Layout

- `backend/AAT.API` — ASP.NET Core API
- `backend/AAT.Application` — use cases and contracts
- `backend/AAT.Domain` — tenant, agent, trace, and metric model
- `backend/AAT.Infrastructure` — PostgreSQL persistence
- `backend/AAT.Telemetry` — privacy contract, validation, bounded queue, retry backoff
- `backend/AAT.Worker` — optional retention host. The API process already runs retention.
- `observer/AAT.Observer` — local worker, loopback ingest, heartbeat, and hardware sampler
- `frontend/omnix-dashboard` — Angular 20 dashboard
- `samples/OllamaWorkload` — explicit Ollama instrumentation sample
- `deployment` — Docker Compose, API image, and dashboard image

## Local setup

Requirements: .NET 10 SDK, Node.js with npm, and Docker.

```powershell
docker compose --env-file .env -f deployment/docker-compose.yml up -d postgres
dotnet run --project backend/AAT.API
```

`.env` is already present for local development and is not committed. In Development the API reads it and ignores placeholder values such as `<password from .env>`. Sign in on the dashboard as `admin` / `Admin@123`.

The API listens on `http://localhost:5080`. In Development it applies EF Core migrations on startup. Health check: `http://localhost:5080/api/v1/health`.

```powershell
npm --prefix frontend/omnix-dashboard start
```

The dashboard is at `http://localhost:4200` and proxies `/api` to the API.

Enroll an observer after creating an organization in the dashboard and copying the one-time token:

```powershell
dotnet run --project observer/AAT.Observer -- --enroll <token> --name local-observer
```

Put the printed `agentId` and `agentApiKey` in environment variables, not in a committed file:

```powershell
$env:Observer__AgentId = "<agent id>"
$env:Observer__AgentKey = "<agent api key>"
dotnet run --project observer/AAT.Observer
```

The observer accepts metadata only on `http://127.0.0.1:47321/v1/executions`. Queue capacity defaults to 1000. When the buffer is full, the oldest event is dropped and `/metrics` reports `droppedEvents`. Uploads retry on network failures, HTTP 429, and HTTP 5xx with exponential backoff and jitter. Other 4xx responses are discarded because retrying them would not succeed. Inference is not on this path.

Record one Ollama call without forwarding the prompt or completion:

```powershell
dotnet run --project samples/OllamaWorkload -- --model llama3.2 --prompt "Reply with the word ready."
```

If Ollama is down, the sample exits without pretending an execution happened. If Ollama succeeds and the observer is down, the sample still exits successfully.

## Tests

```powershell
dotnet test AI-Tower.slnx
```

Application and observer tests do not need Docker. API tests start PostgreSQL with Testcontainers, so the Docker engine must be running.

```powershell
npm --prefix frontend/omnix-dashboard run build
```

## Privacy and hardware

Telemetry fields are an allow-list: identifiers, provider, model, token counts, timings, status, a sanitized error code, and host metrics. Any other JSON field, including prompt or completion text, is rejected and is not stored.

Hardware samples are host-level. The hardware API returns an attribution string that labels request-window overlap as an estimate. GPU numbers appear only when `nvidia-smi` succeeds; otherwise GPU fields are empty and `collectionSource` stays `host`.

Agent credentials and enrollment tokens are shown once and stored only as SHA-256 hashes. Dashboard passwords are hashed with PBKDF2. Set `Auth:JwtSigningKey` and the database password through environment variables or `dotnet user-secrets`. Do not commit them.

An agent is online when its last heartbeat is within 90 seconds. Heartbeats default to every 30 seconds.

## Full stack containers

```powershell
docker compose --env-file .env -f deployment/docker-compose.yml up --build
```

The API is published on port 5080 and the dashboard on port 8088. TLS should terminate at a reverse proxy in any shared environment. This compose file is for a local demo and turns on `Database:AutoMigrate`.
