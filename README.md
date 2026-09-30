# SmallBusinessCRM - Production-Grade Cloud CRM

ASP.NET Core 8 Web API + Decoupled React 18 TypeScript SPA + Redis Distributed Caching + Polly Resilience + Serilog/Seq Logging + OpenTelemetry Metrics.

---

## Architecture Overview

```
                            +------------------------------------+
                            |         React 18 + Vite SPA        |
                            |       (Tailwind + SignalR Hub)     |
                            +-----------------+------------------+
                                              | Reverse Proxy / CORS
                                              v
+-----------------------------------------------------------------------------------+
|                            ASP.NET Core 8 Web API                                 |
|  - JWT & Cookie Auth       - Global EF Query Filters     - Resilience (Polly v8)  |
|  - CorrelationIdMiddleware - Serilog LogContext          - OpenTelemetry /metrics |
+-------------+---------------------+-------------------------------+---------------+
              |                     |                               |
              v                     v                               v
+-------------------+ +---------------------------+ +-------------------------------+
|  MSSQL / SQLite   | |     Redis Distributed     | |     Seq Structured Logs       |
|  (EF Core 8 DB)   | |  (L2 Cache with Fallback) | |  (Centralized Event Viewer)   |
+-------------------+ +---------------------------+ +-------------------------------+
```

### Key Engineering Features (Phase 1 & Phase 2)
1. **Decoupled Modern Frontend (`/client`)**: React 18, TypeScript, Vite, Tailwind CSS, Lucide icons, Sortable Kanban board, Chart.js analytics widgets, real-time SignalR notifications, dark/light theme switching, and accessible modals.
2. **Enhanced Legacy Frontend (`/wwwroot`)**: Modernized responsive Tailwind UI with dark mode support, live stats, Chart.js visualizations, and zero-FOUC script.
3. **Resilience & Fault Tolerance**: Polly v8 resilience pipelines featuring exponential backoff with jitter and circuit breaking for external HTTP dependencies.
4. **Distributed Caching (`ICrmCacheService`)**: Redis-backed distributed cache with proactive invalidation on lead/task mutations and silent database fallback when Redis is offline.
5. **Observability & Telemetry**:
   - Correlation ID tracking across requests (`X-Correlation-ID` header and Serilog `LogContext`).
   - Centralized structured logging sink to **Seq**.
   - OpenTelemetry metrics exporter exposing runtime and HTTP metrics on `/metrics`.
6. **Containerization**: Multi-stage, non-root Docker builds for API and client SPA orchestrated with **Docker Compose**.

---

## Quick Start (Two Ways)

### Option 1: One-Command Docker Compose (Full Stack)

Ensure Docker Desktop is running, then:

```bash
# 1. Copy the environment template
cp .env.example .env

# 2. Launch DB, Redis, Seq, API, and React Client
docker compose up -d --build
```

Access the applications:
- **React 18 SPA Client**: [http://localhost:3000](http://localhost:3000)
- **ASP.NET Core Web API / Swagger**: [http://localhost:5000/swagger](http://localhost:5000/swagger)
- **OpenTelemetry Prometheus Metrics**: [http://localhost:5000/metrics](http://localhost:5000/metrics)
- **Seq Log Viewer**: [http://localhost:5341](http://localhost:5341)

### Option 2: Local Development Run

#### 1. Backend (.NET 8 Web API)
```bash
# Restore & build
dotnet restore
dotnet build

# Run unit & integration tests
dotnet test

# Start the API server (uses SQLite or LocalDB automatically)
cd src/SmallBusinessCRM.Api
dotnet run
```
API runs on `http://localhost:5000` (or `https://localhost:5001`).

#### 2. Frontend (React 18 + Vite SPA)
```bash
cd client
npm install
npm run dev
```
Client dev server runs on `http://localhost:3000` and automatically proxies `/api`, `/account`, and `/hubs` to the API on port 5000.

---

## Seeded Credentials

| Role | Username | Password |
| :--- | :--- | :--- |
| **Admin** | `admin` | `ChangeMe123!` |
| **Manager** | `manager` | `ChangeMe123!` |
| **Sales Rep** | `sales` | `ChangeMe123!` |

---

## Testing & Verification

Run the entire xUnit test suite (33 tests including cache fallbacks, Polly resilience, and analytics reports):

```bash
dotnet test
```

