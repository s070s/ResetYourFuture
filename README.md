# Reset Your Future

A Blazor WebAssembly + ASP.NET Core Web API application with JWT-based authentication, designed for future mobile app compatibility.

---

## Prerequisites

- .NET SDK 9.x
- (No database server required — uses SQLite)

---

## Quick Start

### 1. Build

```powershell
dotnet build ResetYourFuture.sln
```

### 2. Restore Local Tools

```powershell
dotnet tool restore
```

> This installs `dotnet-ef` locally for migration commands.

### 3. Database

The database is created automatically on first run (SQLite file: `ResetYourFuture.db`).

To recreate from scratch:

```powershell
Remove-Item src/ResetYourFuture.Api/ResetYourFuture.db -ErrorAction SilentlyContinue
dotnet run --project src/ResetYourFuture.Api
```

### 4. Run

**Option A — Two terminals:**

```powershell
# Terminal 1 (API on https://localhost:7003)
dotnet run --project src/ResetYourFuture.Api --launch-profile https

# Terminal 2 (Client on https://localhost:7083)
dotnet run --project src/ResetYourFuture.Client
```

**Option B — VS Code task:**

Run `Run Both (Client + API)` from Tasks → Run Task.

### 5. Access

| Service | URL |
|---------|-----|
| Client (Blazor) | https://localhost:7083 |
| API | https://localhost:7003 |
| OpenAPI | https://localhost:7003/openapi/v1.json |

---

## Project Structure

```
ResetYourFuture/
├── src/
│   ├── ResetYourFuture.Api/          # ASP.NET Core Web API
│   │   ├── Controllers/              # AuthController, AdminController
│   │   ├── Data/                     # ApplicationDbContext
│   │   ├── Identity/                 # ApplicationUser, UserStatus
│   │   ├── Logging/                  # File-based logger
│   │   └── Services/                 # TokenService (JWT)
│   ├── ResetYourFuture.Client/       # Blazor WebAssembly
│   │   ├── Pages/                    # Login, Register, Profile, Admin
│   │   └── Services/                 # AuthService, JwtAuthStateProvider
│   └── ResetYourFuture.Shared/       # Shared DTOs
│       └── Auth/                     # Request/Response models
└── tests/                            # (placeholder)
```

---

## Authentication

### Endpoints

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/api/auth/register` | Create account | No |
| POST | `/api/auth/login` | Get JWT token | No |
| GET | `/api/auth/confirm-email` | Confirm email | No |
| POST | `/api/auth/forgot-password` | Request reset | No |
| POST | `/api/auth/reset-password` | Reset password | No |
| GET | `/api/auth/me` | Current user info | Yes |

### Roles

- **Student** — Default role on registration
- **Admin** — Full user/role management access

### Default Admin Account (Development Only)

An admin user is created automatically on first run:

| Field | Value |
|-------|-------|
| **Email** | `admin@resetyourfuture.local` |
| **Password** | `Admin123!` |

> Credentials are configured in `appsettings.Development.json` under `AdminUser`.

### Email Confirmation (Development)

Since no email service is configured, the API returns the confirmation URL in the registration response:

```json
{
  "message": "Registration successful. Please confirm your email.",
  "devConfirmationUrl": "https://localhost:7003/api/Auth/confirm-email?userId=...&token=..."
}
```

**To confirm:** Copy the `devConfirmationUrl` and paste it into your browser or use PowerShell:

```powershell
Invoke-RestMethod -Uri "<paste-the-url-here>" -Method Get
```

### Admin Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/users` | List all users |
| GET | `/api/admin/users/{id}` | Get user details |
| POST | `/api/admin/users/{id}/roles/{role}` | Assign role |
| DELETE | `/api/admin/users/{id}/roles/{role}` | Remove role |
| DELETE | `/api/admin/users/{id}` | Delete user |
| GET | `/api/admin/roles` | List roles |
| POST | `/api/admin/roles/{name}` | Create role |

---

## Database

The application uses **SQLite** for local development. The database file (`ResetYourFuture.db`) is created automatically on first run.

### Common Commands

```powershell
# Delete database (will be recreated on next run)
Remove-Item src/ResetYourFuture.Api/ResetYourFuture.db

# View database with SQLite CLI (if installed)
sqlite3 src/ResetYourFuture.Api/ResetYourFuture.db ".tables"

# Query users
sqlite3 src/ResetYourFuture.Api/ResetYourFuture.db "SELECT Email, FirstName, LastName FROM AspNetUsers"
```

### EF Core Migrations (Optional)

For production deployments, you may want to use migrations instead of `EnsureCreated()`:

```powershell
cd src/ResetYourFuture.Api

# Restore EF tools
dotnet tool restore

# Add migration
dotnet ef migrations add MigrationName --project src/ResetYourFuture.Api --context ApplicationDbContext

# Apply migrations
dotnet ef database update

# Remove last migration (if needed)
 dotnet ef migrations remove --project src/ResetYourFuture.Api

# Generate SQL script
dotnet ef migrations script --output migration.sql

# Rollback to Migration 0
dotnet ef database update 0
```

---

## Configuration

### appsettings.json (API)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=ResetYourFuture.db"
  },
  "Jwt": {
    "Key": "CHANGE_THIS_IN_PRODUCTION_MIN_32_CHARS!!",
    "Issuer": "ResetYourFuture.Api",
    "Audience": "ResetYourFuture.Client",
    "AccessTokenExpirationMinutes": 60
  },
  "AllowedClientOrigin": "https://localhost:7083"
}
```

### appsettings.json (Client - wwwroot)

```json
{
  "ApiBaseUrl": "https://localhost:7003"
}
```

---

## File-Based Logging

Logs are written to `src/ResetYourFuture.Api/Logs/log-YYYY-MM-DD.txt`.

### Tail logs in PowerShell

```powershell
Get-Content -Path .\src\ResetYourFuture.Api\Logs\log-$(Get-Date -Format yyyy-MM-dd).txt -Wait -Tail 50
```

### Usage in code

```csharp
app.MapGet("/api/example", (ILogger<Program> log) =>
{
    log.LogInformation("Endpoint called");
    return Results.Ok();
});
```

---

## Test API with PowerShell

### Register

```powershell
$body = @{
    email = "test@example.com"
    password = "Password123"
    confirmPassword = "Password123"
    firstName = "Test"
    lastName = "User"
    gdprConsent = $true
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://localhost:7003/api/auth/register" -Method Post -Body $body -ContentType "application/json"
```

### Login

```powershell
$body = @{ email = "test@example.com"; password = "Password123" } | ConvertTo-Json
$response = Invoke-RestMethod -Uri "https://localhost:7003/api/auth/login" -Method Post -Body $body -ContentType "application/json"
$token = $response.token
```

### Authenticated request

```powershell
Invoke-RestMethod -Uri "https://localhost:7003/api/auth/me" -Headers @{ Authorization = "Bearer $token" }
```

---

## Troubleshooting

### Client build fails with static web assets error

Already mitigated in `.csproj`. If it recurs:

```powershell
Remove-Item -Recurse -Force src/ResetYourFuture.Client/obj
dotnet build ResetYourFuture.sln
```

### Database connection fails

Delete the SQLite file and restart the API to recreate:

```powershell
Remove-Item src/ResetYourFuture.Api/ResetYourFuture.db -ErrorAction SilentlyContinue
```

### JWT token invalid

Check that `Jwt:Key` in `appsettings.json` is at least 32 characters.

### HTTPS certificate not trusted

If the browser shows certificate warnings:

```powershell
dotnet dev-certs https --trust
```

### Port already in use

Stop all running dotnet processes:

```powershell
Stop-Process -Name dotnet -Force -ErrorAction SilentlyContinue
```

---

## VS Code Tasks

Available via **Terminal → Run Task**:

| Task | Description |
|------|-------------|
| Build Solution | Build all projects |
| Clean Solution | Clean build outputs |
| Restore Packages | Restore NuGet packages |
| Restore Tools | Install local dotnet tools (dotnet-ef) |
| Run API | Start API server (HTTPS on 7003) |
| Run Client | Start Blazor client (HTTPS on 7083) |
| Run Both (Client + API) | Start both in parallel |
| Stop All Servers | Kill all dotnet processes |
| Reset Database | Delete SQLite database file |
| Drop Database | Delete SQLite database file |
| Add Migration | Create new EF migration |
| Update Database | Apply pending migrations |
| Tail Logs | Watch API log file |
| Clean Client Cache | Remove Client obj folder |
| Test API Health | Quick API connectivity test |
| Login as Admin | Get JWT token for admin user |
| Trust Dev Certificates | Trust ASP.NET Core dev certs |

---

## Architecture Notes

- **JWT tokens** stored in localStorage (Blazor WASM)
- **Refresh tokens** generated but not persisted server-side (placeholder for production)
- **Email confirmation** tokens returned in dev response (no email service configured)
- **Parental consent** placeholder for under-18 users (logged, not enforced)
- **GDPR consent** required at registration

---

## For Evaluators

The solution builds and runs locally with **no external dependencies**. Database uses SQLite (auto-created). All auth flows are functional via API. Client UI is minimal but complete.

```powershell
# Full setup from scratch
dotnet build ResetYourFuture.sln
dotnet tool restore

# Start API (creates SQLite DB automatically)
Start-Process powershell -ArgumentList "dotnet run --project src/ResetYourFuture.Api --launch-profile https"

# Start Client
dotnet run --project src/ResetYourFuture.Client
```

### Quick API Test

```powershell
# Health check (after both are running)
Invoke-RestMethod -Uri "https://localhost:7003/api/students" -Method Get
```

### Tasks.json in .vscode

``` json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "Build Solution",
      "type": "shell",
      "command": "dotnet",
      "args": ["build", "ResetYourFuture/ResetYourFuture.sln"],
      "group": { "kind": "build", "isDefault": false },
      "problemMatcher": "$msCompile",
      "presentation": { "reveal": "always", "panel": "shared" }
    },
    {
      "label": "Clean Solution",
      "type": "shell",
      "command": "dotnet",
      "args": ["clean", "ResetYourFuture/ResetYourFuture.sln"],
      "problemMatcher": "$msCompile",
      "presentation": { "reveal": "always", "panel": "shared" }
    },
    {
      "label": "Restore Packages",
      "type": "shell",
      "command": "dotnet",
      "args": ["restore", "ResetYourFuture/ResetYourFuture.sln"],
      "problemMatcher": "$msCompile",
      "presentation": { "reveal": "always", "panel": "shared" }
    },
    {
      "label": "Restore Tools",
      "type": "shell",
      "command": "dotnet",
      "args": ["tool", "restore"],
      "options": { "cwd": "${workspaceFolder}/ResetYourFuture" },
      "problemMatcher": [],
      "presentation": { "reveal": "always", "panel": "shared" }
    },
    {
      "label": "Reset Database",
      "type": "shell",
      "command": "Remove-Item",
      "args": ["ResetYourFuture/src/ResetYourFuture.Api/ResetYourFuture.db", "-ErrorAction", "SilentlyContinue"],
      "problemMatcher": [],
      "presentation": { "reveal": "always", "panel": "shared" }
    },
    {
      "label": "Run API",
      "type": "shell",
      "command": "dotnet",
      "args": ["run", "--project", "ResetYourFuture/src/ResetYourFuture.Api/ResetYourFuture.Api.csproj", "--launch-profile", "https"],
      "problemMatcher": "$msCompile",
      "isBackground": true,
      "presentation": { "reveal": "always", "panel": "dedicated", "group": "servers" }
    },
    {
      "label": "Run Client",
      "type": "shell",
      "command": "dotnet",
      "args": ["run", "--project", "ResetYourFuture/src/ResetYourFuture.Client/ResetYourFuture.Client.csproj"],
      "problemMatcher": "$msCompile",
      "isBackground": true,
      "presentation": { "reveal": "always", "panel": "dedicated", "group": "servers" }
    },
    {
      "label": "Run Both (Client + API)",
      "dependsOn": ["Run API", "Run Client"],
      "dependsOrder": "parallel",
      "group": { "kind": "build", "isDefault": true },
      "problemMatcher": [],
      "presentation": { "reveal": "always", "panel": "shared" }
    },
    {
      "label": "Add Migration",
      "type": "shell",
      "command": "dotnet",
      "args": [
        "ef", "migrations", "add", "${input:migrationName}",
        "--context", "ApplicationDbContext",
        "--project", "ResetYourFuture/src/ResetYourFuture.Api/ResetYourFuture.Api.csproj"
      ],
      "problemMatcher": "$msCompile",
      "presentation": { "reveal": "always", "panel": "shared" }
    },
    {
      "label": "Update Database",
      "type": "shell",
      "command": "dotnet",
      "args": [
        "ef", "database", "update",
        "--context", "ApplicationDbContext",
        "--project", "ResetYourFuture/src/ResetYourFuture.Api/ResetYourFuture.Api.csproj"
      ],
      "problemMatcher": "$msCompile",
      "presentation": { "reveal": "always", "panel": "shared" }
    },
    {
      "label": "Drop Database",
      "type": "shell",
      "command": "Remove-Item",
      "args": ["ResetYourFuture/src/ResetYourFuture.Api/ResetYourFuture.db", "-Force", "-ErrorAction", "SilentlyContinue"],
      "problemMatcher": [],
      "presentation": { "reveal": "always", "panel": "shared" }
    },
    {
      "label": "Remove Last Migration",
      "type": "shell",
      "command": "dotnet",
      "args": [
        "ef", "migrations", "remove",
        "--context", "ApplicationDbContext",
        "--project", "ResetYourFuture/src/ResetYourFuture.Api/ResetYourFuture.Api.csproj"
      ],
      "problemMatcher": "$msCompile",
      "presentation": { "reveal": "always", "panel": "shared" }
    },
    {
      "label": "List Migrations",
      "type": "shell",
      "command": "dotnet",
      "args": [
        "ef", "migrations", "list",
        "--context", "ApplicationDbContext",
        "--project", "ResetYourFuture/src/ResetYourFuture.Api/ResetYourFuture.Api.csproj"
      ],
      "problemMatcher": "$msCompile",
      "presentation": { "reveal": "always", "panel": "shared" }
    },
    {
      "label": "Generate SQL Script",
      "type": "shell",
      "command": "dotnet",
      "args": [
        "ef", "migrations", "script",
        "--context", "ApplicationDbContext",
        "--project", "ResetYourFuture/src/ResetYourFuture.Api/ResetYourFuture.Api.csproj",
        "--output", "ResetYourFuture/migration.sql"
      ],
      "problemMatcher": "$msCompile",
      "presentation": { "reveal": "always", "panel": "shared" }
    },
    {
      "label": "Tail Logs",
      "type": "shell",
      "command": "Get-Content",
      "args": [
        "-Path", "ResetYourFuture/src/ResetYourFuture.Api/Logs/log-$(Get-Date -Format yyyy-MM-dd).txt",
        "-Wait", "-Tail", "50"
      ],
      "isBackground": true,
      "problemMatcher": [],
      "presentation": { "reveal": "always", "panel": "dedicated", "group": "logs" }
    },
    {
      "label": "Clean Client Cache",
      "type": "shell",
      "command": "Remove-Item",
      "args": ["-Recurse", "-Force", "ResetYourFuture/src/ResetYourFuture.Client/obj", "-ErrorAction", "SilentlyContinue"],
      "problemMatcher": [],
      "presentation": { "reveal": "always", "panel": "shared" }
    },
    {
      "label": "Test API Health",
      "type": "shell",
      "command": "Invoke-RestMethod",
      "args": ["-Uri", "https://localhost:7003/api/students", "-Method", "Get"],
      "problemMatcher": [],
      "presentation": { "reveal": "always", "panel": "shared" }
    },
    {
      "label": "Login as Admin",
      "type": "shell",
      "command": "powershell",
      "args": [
        "-Command",
        "$body = @{ email = 'admin@resetyourfuture.local'; password = 'Admin123!' } | ConvertTo-Json; $r = Invoke-RestMethod -Uri 'https://localhost:7003/api/auth/login' -Method Post -Body $body -ContentType 'application/json'; Write-Host 'Token:' $r.token"
      ],
      "problemMatcher": [],
      "presentation": { "reveal": "always", "panel": "shared" }
    },
    {
      "label": "Trust Dev Certificates",
      "type": "shell",
      "command": "dotnet",
      "args": ["dev-certs", "https", "--trust"],
      "problemMatcher": [],
      "presentation": { "reveal": "always", "panel": "shared" }
    },
    {
      "label": "Stop All Servers",
      "type": "shell",
      "command": "Stop-Process",
      "args": ["-Name", "dotnet", "-Force", "-ErrorAction", "SilentlyContinue"],
      "problemMatcher": [],
      "presentation": { "reveal": "always", "panel": "shared" }
    }
  ],
  "inputs": [
    {
      "id": "migrationName",
      "description": "Name for the new migration",
      "type": "promptString",
      "default": "NewMigration"
    }
  ]
}
```