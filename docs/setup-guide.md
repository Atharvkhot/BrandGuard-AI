# BrandGuard AI &mdash; Setup & Deployment Guide

This guide describes how to configure, run, and deploy BrandGuard AI locally or in a cloud environment.

---

## 1. Prerequisites

Ensure the following runtimes are installed on your system:
- **.NET SDK:** Version 9.0 or higher (`dotnet --version`)
- **Node.js:** Version 18.x, 20.x, or later (`node -v`)
- **npm:** Package manager (`npm -v`)
- **Optional:** Microsoft SQL Server (LocalDB, Express, Azure SQL, or Docker instance). *Note: The system automatically uses SQLite fallback if SQL Server is not reachable.*

---

## 2. Configuration & Environment Variables

All configuration is managed in `BrandGuardAI.API/appsettings.json` or through environment variables.

### 2.1 Database Settings
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost,1433;Database=BrandGuardDb;User Id=sa;Password=YourPassword123!;TrustServerCertificate=True",
  "SqliteConnection": "Data Source=BrandGuardAI.db"
},
"Database": {
  "Provider": "Auto"
}
```
- `Database:Provider`: Set to `"SqlServer"`, `"Sqlite"`, or `"Auto"`. In `"Auto"` mode, the platform verifies whether SQL Server is reachable; if not, it automatically boots up SQLite without throwing a connection error.

### 2.2 AI Provider Configuration
The platform supports OpenRouter, OpenAI, and Anthropic:
- **Environment Variables:**
  - `OPENROUTER_API_KEY`: Key for OpenRouter API.
  - `OPENAI_API_KEY`: Alternative key for OpenAI API.
- **appsettings.json:**
```json
"AI": {
  "Provider": "OpenRouter",
  "ChatEndpoint": "https://openrouter.ai/api/v1/chat/completions",
  "ChatModel": "google/gemini-2.5-flash",
  "EmbeddingEndpoint": "https://openrouter.ai/api/v1/embeddings",
  "EmbeddingModel": "text-embedding-3-small"
}
```

---

## 3. Local Execution Steps

### 3.1 Clone & Build Backend
```bash
# Navigate to workspace root
dotnet restore BrandGuardAI.sln
dotnet build BrandGuardAI.sln
```

### 3.2 Run Database Migrations (Optional)
Database tables are automatically created on initial startup via `EnsureCreated()`. To manually run migrations against a dedicated SQL Server:
```bash
dotnet ef database update --project BrandGuardAI.Infrastructure --startup-project BrandGuardAI.API
```

### 3.3 Start the ASP.NET Core API
```bash
dotnet run --project BrandGuardAI.API --launch-profile http
```
The API is live on: `http://localhost:5000`  
Swagger Documentation: `http://localhost:5000/swagger`

### 3.4 Start the Angular Frontend
In a new terminal window:
```bash
cd brandguard-ui
npm install
npm start
```
The UI is live on: `http://localhost:4200`

---

## 4. Production Deployment

### Backend (Docker / Azure App Service / AWS)
1. Publish the backend binary:
```bash
dotnet publish BrandGuardAI.API/BrandGuardAI.API.csproj -c Release -o ./publish/api
```
2. Configure environment variables in host (`ConnectionStrings__DefaultConnection`, `OPENROUTER_API_KEY`, `ASPNETCORE_ENVIRONMENT=Production`).

### Frontend (Static Web Apps / Nginx / Vercel / Cloudflare)
1. Build the production Angular bundle:
```bash
cd brandguard-ui
npm run build
```
2. Deploy the generated distribution folder (`brandguard-ui/dist/brandguard-ui/browser`) to any static hosting provider.
3. Configure the API reverse proxy or set the production API URL in `api.service.ts`.
