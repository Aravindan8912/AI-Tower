# OMNIX AI (AAT) – End-to-End MVP Flow

## 1. System context
Customer AI application / Ollama runtime → local observer → secure telemetry API → PostgreSQL → analytics API → Angular dashboard.

The observer is a sidecar/worker and must not be a synchronous proxy dependency for inference. For the first MVP, use explicit instrumentation or an OpenTelemetry-compatible integration. Do not promise transparent interception of arbitrary traffic until a supported integration pattern is selected and tested.

## 2. Runtime flow
1. Organization administrator creates an organization and environment.
2. Administrator enrolls an observer agent and receives a one-time enrollment token.
3. Observer starts, authenticates, receives an agent identity, and sends periodic heartbeats.
4. Customer connects one Ollama instance and enables supported instrumentation.
5. Each LLM execution produces metadata only: event ID, request ID, model/provider, token counts, TTFT, total latency, status, and timestamps.
6. Observer records host hardware samples (CPU, memory, and where supported GPU/VRAM) with UTC timestamps.
7. Observer places telemetry in a bounded local queue.
8. A background sender batches events and sends them over outbound HTTPS.
9. API authenticates the agent, validates payloads, applies tenant mapping, deduplicates event IDs, and stores records.
10. Analytics queries aggregate executions, tokens, latency, errors, agent status, and hardware trends.
11. Angular dashboard refreshes using polling initially; SignalR can be added if needed.
12. If cloud connectivity fails, observer buffers within configured limits, retries with backoff, and drops oldest/newest according to a documented policy when full. It increments a dropped-event counter. LLM execution is unaffected.

## 3. Data flow and privacy
Allowed: identifiers, provider/model names, token counts, timings, status, sanitized error codes, host metrics, timestamps, agent version.
Prohibited: raw prompts, completions, system messages, credentials, authorization headers, arbitrary request/response bodies.
Do not log secrets or sensitive payloads.
Hardware metrics are host-level samples. Request-window correlation is an estimate, not exact per-request resource attribution.

## 4. MVP screens
- Login
- Organization/environment selector
- Overview dashboard
- Agent list and agent details
- Execution history with filters and pagination
- Model analytics
- Hardware monitoring
- Basic audit log

## 5. API flow
- POST /api/v1/agents/enroll
- POST /api/v1/agents/{agentId}/heartbeat
- GET /api/v1/agents
- POST /api/v1/telemetry/executions/batch
- POST /api/v1/telemetry/hardware/batch
- GET /api/v1/analytics/overview
- GET /api/v1/analytics/models
- GET /api/v1/analytics/hardware
- GET /api/v1/executions

Agent endpoints require machine authentication. Dashboard endpoints require user authentication and tenant authorization.

## 6. Delivery phases
### Phase 0 – Foundation
Solution skeleton, Angular shell, PostgreSQL, Docker Compose, health endpoint, CI/build conventions.

### Phase 1 – Identity and agent lifecycle
Tenant model, user login, roles, agent enrollment, agent heartbeat, online/offline status.

### Phase 2 – Telemetry ingestion
Validated batch DTOs, idempotency, execution events, hardware samples, retention/indexing.

### Phase 3 – Observer
.NET Worker Service, bounded queue, retry/backoff, Ollama integration, privacy-safe metadata collection.

### Phase 4 – Dashboard and analytics
Overview KPIs, agent list, execution history, model breakdown, hardware time series, filters and pagination.

### Phase 5 – Hardening and demo
Integration tests, load tests, telemetry outage tests, tenant isolation tests, security review, Docker deployment and demo guide.

### Later release – Optimization and governance
Only after monitoring is stable: optimization jobs, authorized skill content analysis, recommendations, approval, signed artifacts, agent pull, local verification, staged deployment, measurement and rollback.

## 7. Acceptance criteria
- Agent can enroll and heartbeat.
- One Ollama workload can generate observable execution events.
- No prompt or completion text is transmitted or persisted.
- API ingestion is authenticated, validated, tenant-safe, and idempotent.
- Observer retries without blocking inference and uses bounded storage.
- Dashboard shows real persisted data, not hard-coded sample values.
- Execution history is paginated and filterable.
- Hardware data shows timestamps and collection source.
- GPU attribution is labelled estimated unless exact metrics exist.
- Tests cover agent auth, tenant isolation, ingestion validation, idempotency, and observer outage behavior.
