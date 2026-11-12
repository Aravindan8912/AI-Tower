---
name: omnix-ai-mvp
description: Build and maintain the OMNIX AI (AAT) MVP: an enterprise AI observability control tower with a .NET backend, Angular dashboard, PostgreSQL storage, and a local observer agent. Use when implementing features, APIs, telemetry, database changes, agent integrations, analytics, or tests for this project.
---

# OMNIX AI MVP Engineering Skill

## Product goal
Build a secure, multi-tenant AI observability platform that monitors AI/LLM executions and infrastructure telemetry without making the observer a dependency of the customer application.

The MVP is monitoring-first:
1. Register an organization, environment, and observer agent.
2. Connect one local LLM runtime (Ollama first).
3. Collect execution metadata and hardware metrics.
4. Send telemetry asynchronously to the cloud/API.
5. Store and query telemetry by tenant, agent, model, and time.
6. Display dashboards, agent health, execution history, and analytics.
7. Add optimization recommendations and approval/deployment only after monitoring is reliable.

## Approved baseline stack
- Backend: C# / ASP.NET Core Web API on .NET 10 LTS.
- Architecture: modular monolith with clean separation: API, Application, Domain, Infrastructure, Telemetry, Worker.
- Frontend: Angular 20 with TypeScript and Tailwind CSS.
- Database: PostgreSQL with EF Core/Npgsql.
- Observer: .NET Worker Service.
- API contract: REST + OpenAPI; use batch telemetry ingestion.
- Local development: Docker Compose for PostgreSQL and optional supporting services.
- AI optimization: defer to a later phase; isolate behind an interface/provider adapter. Do not make external AI calls in the monitoring MVP.

## Non-negotiable engineering rules
1. Never send raw prompts, completions, system instructions, credentials, or sensitive payloads to the cloud telemetry API.
2. Telemetry must be best-effort and asynchronous. A telemetry failure must never block or fail the customer's LLM request.
3. Use bounded local buffering, retry with backoff, and observable dropped-event counters. Avoid unbounded memory growth.
4. Use UTC timestamps in storage and APIs. Use stable event/request IDs for correlation and idempotency.
5. Enforce tenant isolation on every tenant-owned query and command. Never trust an OrganizationId supplied by a client without validating it against the authenticated principal or enrolled agent.
6. Store secrets only in secure configuration/secret stores. Never commit API keys, tokens, passwords, or private signing keys.
7. Validate all incoming DTOs; set sensible batch-size and payload-size limits; rate-limit machine endpoints.
8. Use structured logging. Never log raw prompt or completion content.
9. Use migrations for database changes. Do not manually change schema without a migration.
10. Add unit/integration tests for business rules and API behavior.
11. Keep controllers thin. Put business logic in Application services/use cases.
12. Prefer explicit interfaces and dependency injection. Avoid premature microservices and unnecessary infrastructure.
13. Use pagination for execution history and audit data.
14. Document breaking API changes and update OpenAPI/DTO tests.
15. Do not implement skill auto-deployment in the monitoring milestone.

## MVP domain concepts
- Organization: tenant boundary.
- User / Role: dashboard identity and permissions.
- Environment: Development, Staging, Production, or customer-defined.
- Agent: enrolled observer installation with status, version, and last heartbeat.
- AI Application: monitored app or workload.
- Model: provider/runtime and model name.
- ExecutionTrace: one LLM execution with timings, token counts, status, and correlation IDs.
- HardwareMetric: timestamped CPU/RAM/GPU/VRAM sample reported by an agent.
- AgentHeartbeat: health and last-seen record.
- AuditLog: security-relevant administrative activity.

## Initial API surface
- POST /api/v1/agents/enroll
- POST /api/v1/agents/{agentId}/heartbeat
- GET  /api/v1/agents
- GET  /api/v1/agents/{agentId}
- POST /api/v1/telemetry/executions/batch
- POST /api/v1/telemetry/hardware/batch
- GET  /api/v1/executions
- GET  /api/v1/analytics/overview
- GET  /api/v1/analytics/models
- GET  /api/v1/analytics/hardware
- GET  /api/v1/health

Machine-to-machine endpoints must authenticate the enrolled agent. Dashboard endpoints must use user authentication and tenant authorization.

## Telemetry contract principles
Execution event fields:
- eventId, organizationId (resolved/validated server-side where possible), agentId,
- environmentId, applicationId, requestId, provider, model,
- promptTokens, completionTokens, timeToFirstTokenMs, totalLatencyMs,
- tokensPerSecond, status, errorCode (sanitized), startedAtUtc, completedAtUtc.

Hardware event fields:
- eventId, agentId, sampledAtUtc, cpuPercent, memoryUsedBytes,
- memoryTotalBytes, gpuUtilizationPercent, gpuMemoryUsedBytes,
- gpuMemoryTotalBytes, gpuName, collectionSource.

Never include promptText, completionText, authorization headers, API keys, or arbitrary request bodies.

## Hardware attribution rule
Hardware measurements are host-level samples. Correlating them with request time windows provides an estimate of resource pressure, not exact per-request GPU ownership. Label visualizations and reports as correlated/estimated. Do not claim exact per-request GPU consumption unless the runtime supplies trustworthy per-process/per-request metrics.

## Recommended implementation sequence
1. Repository and solution skeleton; local Docker Compose; health endpoint.
2. PostgreSQL schema, EF Core migrations, tenant model.
3. Agent enrollment, agent authentication, heartbeat and status.
4. Execution and hardware batch ingestion with validation and idempotency.
5. Observer worker with local bounded queue, retry/backoff, and no prompt capture.
6. Ollama integration/instrumentation and sample workload.
7. Dashboard: overview, agents, executions, model analytics, hardware.
8. Tests, security review, deployment guide and demo seed data.
9. Only after the above is stable: optimization proposals, admin approval, signed artifact pull, staged rollout and rollback.

## Definition of done for every feature
- Compiles with the pinned SDK/package versions.
- Has validation and meaningful error responses.
- Enforces tenant/agent authorization.
- Has unit tests; API/data features also have integration tests where practical.
- Has structured logs without sensitive payloads.
- Has migration and API documentation updates if applicable.
- Has a concise README update with run/test instructions.

## How Cursor should work
- First inspect the repository and existing conventions; do not overwrite working code.
- Before large changes, provide a short implementation plan and list files to be changed.
- Implement one vertical slice at a time and keep the solution runnable.
- Do not invent missing product decisions. Ask one focused question if a decision blocks implementation; otherwise choose the simplest secure default and document it.
- Run build/tests after each meaningful slice and report exact commands/results.
- Do not claim tests passed unless they were actually run.
