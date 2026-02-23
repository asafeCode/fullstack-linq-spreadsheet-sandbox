# Fullstack LINQ Spreadsheet Sandbox

A fullstack application for uploading spreadsheets (CSV/XLSX), optionally unifying sheets, and running LINQ queries in a sandboxed environment.

## Tech Stack

- Backend: ASP.NET Core (.NET 10)
- Frontend: React + Vite + TypeScript
- Query sandbox: isolated process (`SpreadsheetFilterApp.QuerySandboxHost`)
- Local infrastructure: Docker Compose

## Project Structure

```text
backend/
  src/
  tests/
  docker-compose.yml
  Dockerfile
frontend/
  src/
  public/
  Dockerfile
```

## Prerequisites

- Docker + Docker Compose
- (Optional for non-Docker execution)
  - .NET SDK 10
  - Node.js 22+
  - npm

## Run with Docker (recommended)

From the `backend` folder:

```bash
docker compose up -d --build
```

Services:

- Frontend: `http://localhost:5173`
- Backend API: `http://localhost:5063`
- Swagger: `http://localhost:5063/swagger`

Stop:

```bash
docker compose down
```

## Run without Docker

### Backend

```bash
cd backend/src/SpreadsheetFilterApp.Web
dotnet restore
dotnet run --urls http://localhost:5063
```

### Frontend

```bash
cd frontend
npm install
npm run dev
```

Local frontend typically runs at `http://localhost:5173`.

## Functional Flow

1. Upload 1 to 3 files (`file1`, `file2`, `file3`)
2. Track parsing progress/status
3. (Optional) Unify sheets by key
4. Validate LINQ
5. Execute query in sandbox
6. Preview results and download output

## Main Endpoints (current runtime)

Base URL: `http://localhost:5063`

- `POST /api/query/upload` (multipart: `file1`, `file2?`, `file3?`)
- `GET /api/query/status/{jobId}`
- `GET /api/query/contract?jobId={jobId}`
- `GET /api/query/{jobId}/preview/{sheetName}?page=1&pageSize=200`
- `POST /api/query/{jobId}/unify`
- `POST /api/query/{jobId}/execute`
- `GET /api/query/execute/{queryId}`
- `POST /api/spreadsheets/validate` (dedicated query validation endpoint)

## Tests

### Backend

```bash
cd backend
dotnet test SpreadsheetFilterApp.sln --nologo
```

### Frontend

```bash
cd frontend
npm test -- --run
```

## Important Notes

- In local Docker, the backend runs on HTTP (`5063`).
- In local production-like mode, frontend uses same-origin routes (`/api`, `/hubs`) through Nginx proxy.
- In cloud deployments, the recommended setup is TLS at ingress/load balancer and HTTP inside the private network.

## Troubleshooting

### "Sandbox host not found"

Make sure the backend image was rebuilt after Dockerfile/runtime changes:

```bash
cd backend
docker compose up -d --build backend
```

### Query execution timeout

- Reduce query complexity
- Reduce the number of rows being processed
- Increase `timeoutMs` in the execute request when appropriate

### Unify validation error ("Primary sheet not found")

Check whether `primarySheetName` exists in the contract returned by `/api/query/contract`.

## License

Define your project license here (for example, MIT), if applicable.
