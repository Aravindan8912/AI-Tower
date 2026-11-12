# OMNIX AI Analytic Tower

This repository is the monitoring control tower. One local observer watches an AI workload, sends execution metadata and host metrics to the API, and the Angular dashboard shows what was stored.

The observer does not sit in the model call. If the API is down, the model call still finishes. Prompts, completions, system instructions, credentials, and request bodies are not collected.

This release stops at monitoring. It does not approve or deploy a model change.

## What you are running

| Piece | Project | Address |
| --- | --- | --- |
| PostgreSQL 16 | `deployment/docker-compose.yml` | `localhost:5432` |
| API | `backend/AAT.API` | `http://localhost:5080` |
| Dashboard | `frontend/omnix-dashboard` | `http://localhost:4200` |
| Observer | `observer/AAT.Observer` | `http://127.0.0.1:47321` |
| Sample workload | `samples/OllamaWorkload` | calls Ollama, then the observer |

You need the .NET 10 SDK (`global.json` pins 10.0.402), Node.js with npm, and Docker.

## 1. Configure secrets

From the repository root, copy the example file if `.env` is not already there:

```powershell
Copy-Item .env.example .env
```

Open `.env` and replace both placeholder values with real local values. The database password can be any string of at least 8 characters. The signing key must be at least 32 characters.

```text
POSTGRES_USER=aat
POSTGRES_PASSWORD=your-local-database-password
POSTGRES_DB=aat
AAT_JWT_SIGNING_KEY=your-local-signing-key-32-characters
```

`.env` stays on this machine. It is listed in `.gitignore`.

In Development the API reads that file on startup. Leave `ASPNETCORE_ENVIRONMENT` as `Development`, which `dotnet run` already sets for the API.

## 2. Start PostgreSQL

```powershell
docker compose --env-file .env -f deployment/docker-compose.yml up -d postgres
```

Wait until the container is healthy:

```powershell
docker compose --env-file .env -f deployment/docker-compose.yml ps
```

## 3. Start the API

```powershell
dotnet run --project backend/AAT.API
```

On this first Development start the API applies the EF Core migration and creates the local administrator:

- Username: `admin`
- Password: `Admin@123`
- Organization: `OMNIX AI`
- Environment: `Development`

Check `http://localhost:5080/api/v1/health`. The body contains `Healthy` when PostgreSQL is reachable.

Leave this terminal open.

## 4. Start the dashboard

In a second terminal:

```powershell
npm --prefix frontend/omnix-dashboard install
npm --prefix frontend/omnix-dashboard start
```

Open `http://localhost:4200`. The dev server proxies `/api` to `http://localhost:5080`.

Sign in with `admin` and `Admin@123`.

## 5. Enroll the observer

Enrollment is two steps. The dashboard shows a token once. The enroll command prints the agent id and agent key once. Both values are stored only as hashes, so a lost value has to be created again.

1. Open **Agents**.
2. Choose the `Development` environment and an agent name, for example `local-observer`.
3. Click **Create enrollment token**.
4. Copy the entire token in the amber box. It starts with `aat_enroll_`.

In a third terminal, from the repository root, paste that exact token:

```powershell
dotnet run --project observer/AAT.Observer -- --enroll "paste-the-full-token" --name local-observer
```

A successful command prints JSON and exits. Copy `agentId` and `agentApiKey` from that JSON. `agentApiKey` starts with `aat_agent_`.

| Result | Meaning |
| --- | --- |
| JSON with `agentId` and `agentApiKey` | The agent is enrolled. Continue to step 6. |
| `Enrollment failed (401)` | The token does not match a stored token, or it has expired. Create a new token and copy the whole value. |
| `Enrollment failed (409)` | That token was already used. Create a new one. |

## 6. Run the observer

Set the two values in the same terminal, then start the observer. Use the values from step 5, and keep them out of git.

```powershell
$env:Observer__AgentId = "paste-the-agent-id"
$env:Observer__AgentKey = "paste-the-agent-api-key"
dotnet run --project observer/AAT.Observer
```

The observer listens on `http://127.0.0.1:47321`. It sends a heartbeat about every 30 seconds and a host sample about every 15 seconds. The Agents page marks it **Online** when a heartbeat has arrived within 90 seconds. **Reported health** is the status the observer sent. Those two are separate: an agent can report Healthy and still be Offline if heartbeats stop.

`http://127.0.0.1:47321/metrics` shows the local queue depth and how many events were dropped. The queue holds 1000 items. When it is full, the oldest item is dropped.

## 7. Record one model call

Install and run Ollama separately, with a model already pulled. Then:

```powershell
dotnet run --project samples/OllamaWorkload -- --model llama3.2 --prompt "Reply with the word ready."
```

The sample calls Ollama first. It then posts metadata to the observer: model name, token counts, latency, and status. The prompt text and the completion text stay on this machine.

- If Ollama is not running, the sample exits with an error and records nothing.
- If Ollama succeeds and the observer is down, the sample still exits successfully. The model call is not failed by monitoring.

Within a few seconds, **Executions** shows the row and **Overview** includes it in the last 24 hours.

## What each page is for

Refresh the dashboard after the observer has been running.

- **Overview** shows the last 24 hours. Execution counts, error rate, tokens, latency, and agents online come from stored rows. The token bar splits succeeded, failed, and cancelled use. Failed and cancelled tokens are the amount a repeat of those calls would spend again. The page does not call a model.
- **Agents** lists reachability from the 90 second heartbeat rule, the health string reported by the agent, the version, and the last heartbeat time in UTC.
- Open an agent for that same split limited to the agent, the latest host sample, recent executions, and the gap between heartbeats. The host sample is the whole machine. Overlap with a request is an estimate, and the page says so. GPU numbers appear only when `nvidia-smi` returns data.
- **Executions** is the event list. Filters are metadata only.
- **Models** groups the same rows by provider and model. Share is that model's tokens divided by all tokens in the window.
- **Hardware** is the host sample series for the selected agent.
- **Audit** lists administrative actions such as enrollment. It does not list prompts.

The environment menu filters overview, executions, models, agents, and hardware. An empty environment shows zero agents.

## How a recorded call moves

1. The workload finishes the model call.
2. It posts metadata to `http://127.0.0.1:47321/v1/executions`.
3. The observer checks the JSON against an allow-list and puts the event on a bounded queue. The HTTP call returns as soon as the event is queued.
4. A background sender batches the queue to `http://localhost:5080/api/v1/telemetry/executions/batch` with the `X-Agent-Key` header.
5. The API resolves the organization from that key. A client-supplied organization id is not trusted.
6. The same `(organization, event id)` is stored once.
7. The dashboard reads the stored rows.

Uploads retry after a network failure, HTTP 429, HTTP 409, and HTTP 5xx, using exponential backoff and jitter. Other 4xx responses are discarded. A payload that includes a prompt or completion field is rejected and the text is not stored.

## Tests

Application and observer tests do not need Docker. API tests start PostgreSQL through Testcontainers, so Docker must be running.

```powershell
dotnet test tests/AAT.Application.Tests
dotnet test tests/AAT.Observer.Tests
dotnet test tests/AAT.Api.Tests
```

Dashboard production build:

```powershell
npm --prefix frontend/omnix-dashboard run build
```

## Run the API and dashboard in containers

This path is for a local demo of the API and dashboard. The observer still runs on the host so it can see this machine.

```powershell
docker compose --env-file .env -f deployment/docker-compose.yml up --build
```

The API is on port 5080 and the dashboard on port 8088. The compose file sets `Database:AutoMigrate` and is not a production deployment. Put TLS on a reverse proxy before sharing it.

The container API uses `Production`, so it does not seed `admin` / `Admin@123`. Register the first organization from the dashboard, then enroll an observer with the same commands as steps 5 and 6, pointed at `http://localhost:5080`.

## Project map

- `backend/AAT.API` — HTTP, authentication, and thin controllers
- `backend/AAT.Application` — use cases and the token rollup
- `backend/AAT.Domain` — organizations, agents, traces, and metrics
- `backend/AAT.Infrastructure` — EF Core and PostgreSQL
- `backend/AAT.Telemetry` — the allow-list, validation, queue, and retry delay
- `backend/AAT.Worker` — retention. The API process already runs it, so leave the worker stopped during local development.
- `observer/AAT.Observer` — queue, heartbeat, hardware sample, and loopback ingest
- `frontend/omnix-dashboard` — Angular 20 dashboard
- `docs/END_TO_END_FLOW.md` — the monitoring flow this repository follows

Schema changes belong in an EF Core migration. Business rules belong in the application services, with a test next to the rule.
