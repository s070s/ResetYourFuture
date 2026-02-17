# Reset Your Future

A Blazor WebAssembly + ASP.NET Core Web API application with JWT-based authentication, designed for future mobile app compatibility.

---

## Prerequisites

- .NET SDK 9.x
- (Developed using LocalDB Microsoft SQL Server)

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

### 3. Run

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


**Option C — Visual Studio:**

1. Open the solution in Visual Studio.
2. Set the `ResetYourFuture.Api` and `ResetYourFuture.Client` projects as startup projects.
3. Start debugging (F5) to run both projects.

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

**To confirm:** Copy the `devConfirmationUrl` and paste it into your browser or click the button:


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

The application uses **SQL Server** with LocalDB and uses Migrations.


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

# Remove last migration (multiple times to remove older ones)
 dotnet ef migrations remove --project src/ResetYourFuture.Api

# Generate SQL script
dotnet ef migrations script --output migration.sql

# Rollback to Migration 0
dotnet ef database update 0




Example
dotnet ef database update 0 --project src/ResetYourFuture.Api
dotnet ef migrations remove --project src/ResetYourFuture.Api
dotnet ef migrations add MigrationName --project src/ResetYourFuture.Api --context ApplicationDbContext
dotnet ef database update --project src/ResetYourFuture.Api

```

---

## Configuration

### appsettings.json (API)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "AllowedClientOrigin": "https://localhost:7083",

  "ConnectionStrings": {
    "DefaultConnection": "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=ResetYourFutureDb;Integrated Security=True;Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;Application Name=\"SQL Server Management Studio\";Command Timeout=0"
  },

  "Jwt": {
    "Key": "CHANGE_THIS_IN_PRODUCTION_MIN_32_CHARS!!",
    "Issuer": "ResetYourFuture.Api",
    "Audience": "ResetYourFuture.Client",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  },
  "SeedData": {

    "JsonPaths": {
      "Courses": "../ResetYourFuture.Shared/JSON/Courses",
      "Assessments": "../ResetYourFuture.Shared/JSON/Assessments"
    }
  }
}

```

### appsettings.json (Client - wwwroot)

```json
{
  "ApiBaseUrl": "https://localhost:7003",
  "Social": {
    "Instagram": "https://instagram.com/",
    "Youtube": "https://youtube.com"
  }
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

 
## Troubleshooting

 

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

---

## Assessment Authoring Guide

Assessments are seeded from JSON files at startup. Each file describes one assessment and lives in:

```
src/ResetYourFuture.Shared/JSON/Assessments/
```

> The seeder runs **only when the database has no assessments**. To re-seed, delete existing assessments from the DB (or drop and re-create) and restart the API.

### 1. Create the seed file

Add a new `.json` file in the folder above. Name it after the `Key` (e.g. `stress_check_v1.json`).

```json
{
  "Key": "stress_check_v1",
  "Title": "Stress Check",
  "Description": "A quick check-in to reflect on your current stress levels.",
  "IsPublished": true,
  "CreatedAt": "2025-07-01T08:00:00Z",
  "PublishedAt": "2025-07-01T08:00:00Z",
  "SchemaJson": "<escaped JSON string — see step 2>"
}
```

| Field | Required | Notes |
|---|---|---|
| `Key` | ✅ | Unique identifier (`snake_case` recommended). |
| `Title` | ✅ | Display name shown to students and admins. |
| `Description` | ❌ | Shown on the assessment card before starting. |
| `SchemaJson` | ✅ | **Escaped** JSON string containing the questions (see below). |
| `IsPublished` | ❌ | `true` = visible to students immediately. Default `false`. |
| `CreatedAt` | ❌ | ISO 8601. Defaults to current UTC time. |
| `PublishedAt` | ❌ | ISO 8601. Auto-set when `IsPublished` is `true`. |

### 2. Write the schema

`SchemaJson` is a JSON **string** (escaped inside the seed file). Its inner structure:

```json
{
  "id": "stress_check_v1",
  "title": "Stress Check",
  "version": "1.0",
  "questions": [ ]
}
```

### 3. Supported question types

#### `text` — Free-text input

```json
{ "id": "q1", "type": "text", "label": "What is your biggest challenge right now?", "required": true }
```

#### `choice` — Single-select dropdown

```json
{
  "id": "q2",
  "type": "choice",
  "label": "Where are you in your career journey?",
  "options": ["Exploring options", "Starting out", "Growing skills", "Changing direction", "Established"],
  "required": true
}
```

#### `rating` — Numeric scale (button group, configurable range)

```json
{ "id": "q3", "type": "rating", "label": "How clear are your career goals?", "min": 1, "max": 5, "required": true }
```

#### `multi-select` — Multiple-choice checkboxes

```json
{
  "id": "q4",
  "type": "multi-select",
  "label": "Which skills are you developing?",
  "options": ["Communication", "Leadership", "Technical", "Creative"],
  "required": false
}
```

#### `likert` — 1–5 scale (alias of `rating`, defaults to min=1 max=5)

```json
{ "id": "q5", "type": "likert", "label": "I feel confident about my next career step.", "required": true }
```

#### `date` — Date picker

```json
{ "id": "q6", "type": "date", "label": "When did you start your current role?", "required": false }
```

### 4. Question field reference

| Field | Required | Type | Notes |
|---|---|---|---|
| `id` | ✅ | `string` | Unique within the assessment (e.g. `q1`, `q2`). |
| `type` | ✅ | `string` | `text` · `choice` · `rating` · `multi-select` · `likert` · `date` |
| `label` | ✅ | `string` | The question text shown to the student. |
| `options` | ⚠️ | `string[]` | **Required** for `choice` and `multi-select`. |
| `min` | ❌ | `int` | Lower bound for `rating`. Default `1`. |
| `max` | ❌ | `int` | Upper bound for `rating`. Default `5`. |
| `required` | ❌ | `bool` | Shows a `*` indicator on the form. Default `false`. |

### 5. Full working example

**File:** `src/ResetYourFuture.Shared/JSON/Assessments/stress_check_v1.json`

```json
{
  "Key": "stress_check_v1",
  "Title": "Stress Check",
  "Description": "A quick check-in to reflect on your current stress levels.",
  "IsPublished": true,
  "CreatedAt": "2025-07-01T08:00:00Z",
  "PublishedAt": "2025-07-01T08:00:00Z",
  "SchemaJson": "{\"id\":\"stress_check_v1\",\"title\":\"Stress Check\",\"version\":\"1.0\",\"questions\":[{\"id\":\"q1\",\"type\":\"rating\",\"label\":\"How stressed have you felt this week?\",\"min\":1,\"max\":5,\"required\":true},{\"id\":\"q2\",\"type\":\"choice\",\"label\":\"What is your main source of stress?\",\"options\":[\"Work\",\"Finances\",\"Relationships\",\"Health\",\"Other\"],\"required\":true},{\"id\":\"q3\",\"type\":\"multi-select\",\"label\":\"What helps you manage stress?\",\"options\":[\"Exercise\",\"Meditation\",\"Talking to someone\",\"Hobbies\",\"Sleep\"],\"required\":false},{\"id\":\"q4\",\"type\":\"text\",\"label\":\"Anything else you would like to share?\",\"required\":false}]}"
}
```

> **Tip:** Write the schema as pretty-printed JSON first, then escape it into a single-line string for `SchemaJson`. Most editors or online tools can do this.

### 6. Verify

1. Place the `.json` file in `src/ResetYourFuture.Shared/JSON/Assessments/`.
2. Clear existing assessments (drop DB or delete rows) and restart the API.
3. Check the API logs — the seeder logs each loaded assessment.
4. **Admin** → `/admin/assessments` — confirm it appears and can be published/unpublished.
5. **Student** → `/assessments` — take the assessment and view results in `/assessments/mine`.

---

## Course Authoring Guide

Courses are seeded from JSON files at startup. Each file describes one course (with modules and lessons) and lives in:

```
src/ResetYourFuture.Shared/JSON/Courses/
```

> The seeder runs **only when the database has no courses**. To re-seed, delete existing courses from the DB (or drop and re-create) and restart the API.

### 1. Create the seed file

Add a new `.json` file in the folder above. Use a kebab-case name (e.g. `interview-skills.json`).

```json
{
  "title": "Interview Skills",
  "description": "Master the art of job interviews.",
  "isPublished": true,
  "modules": [ ]
}
```

| Field | Required | Notes |
|---|---|---|
| `title` | ✅ | Display name shown to students and admins. |
| `description` | ❌ | Shown on the course card in the catalog. |
| `isPublished` | ❌ | `true` = visible to students immediately. Default `false`. |
| `modules` | ✅ | Array of module objects (see below). |

### 2. Add modules

Each module groups related lessons within the course.

```json
{
  "title": "Preparation",
  "description": "Everything you need before the interview.",
  "sortOrder": 1,
  "lessons": [ ]
}
```

| Field | Required | Notes |
|---|---|---|
| `title` | ✅ | Module heading displayed in the course outline. |
| `description` | ❌ | Short summary of the module. |
| `sortOrder` | ✅ | Display order within the course (1, 2, 3 …). |
| `lessons` | ✅ | Array of lesson objects (see below). |

### 3. Add lessons

Each lesson is a single learning unit. A lesson can have **text/markdown content**, a **video**, a **PDF**, or any combination.

#### Text / Markdown lesson

```json
{
  "title": "Research the Company",
  "content": "# Research the Company\n\nBefore any interview, you should:\n\n- Visit the company website\n- Read recent news articles\n- Understand their products and values",
  "durationMinutes": 10,
  "sortOrder": 1
}
```

#### Video lesson

```json
{
  "title": "Body Language Tips",
  "videoPath": "https://www.youtube.com/embed/VIDEO_ID",
  "durationMinutes": 8,
  "sortOrder": 2
}
```

#### PDF lesson

```json
{
  "title": "Interview Checklist",
  "pdfPath": "/assets/lessons/interview-checklist.pdf",
  "durationMinutes": 5,
  "sortOrder": 3
}
```

#### Combined lesson (video + text)

```json
{
  "title": "Common Questions",
  "videoPath": "https://www.youtube.com/embed/VIDEO_ID",
  "content": "## Key Takeaways\n\n- Always prepare examples using the STAR method\n- Keep answers concise (1–2 minutes)",
  "durationMinutes": 15,
  "sortOrder": 4
}
```

### 4. Lesson field reference

| Field | Required | Type | Notes |
|---|---|---|---|
| `title` | ✅ | `string` | Lesson heading. |
| `content` | ❌ | `string` | Text or Markdown content. Use `\n` for newlines. |
| `videoPath` | ❌ | `string` | Embeddable video URL (YouTube embed format recommended). |
| `pdfPath` | ❌ | `string` | Path to a PDF file served from the API. |
| `durationMinutes` | ❌ | `int` | Estimated reading/watching time. Used for progress display. |
| `sortOrder` | ✅ | `int` | Display order within the module (1, 2, 3 …). |

> At least one of `content`, `videoPath`, or `pdfPath` should be provided so the lesson has something to display.

### 5. Full working example

**File:** `src/ResetYourFuture.Shared/JSON/Courses/interview-skills.json`

```json
{
  "title": "Interview Skills",
  "description": "Master the art of job interviews — from preparation to follow-up.",
  "isPublished": true,
  "modules": [
    {
      "title": "Before the Interview",
      "description": "Preparation is everything.",
      "sortOrder": 1,
      "lessons": [
        {
          "title": "Research the Company",
          "content": "# Research the Company\n\nBefore any interview:\n\n- Visit the company website and read the About page\n- Check recent news and press releases\n- Understand their products, services, and competitors\n- Look up the interviewer on LinkedIn",
          "durationMinutes": 10,
          "sortOrder": 1
        },
        {
          "title": "Prepare Your Answers",
          "videoPath": "https://www.youtube.com/embed/dQw4w9WgXcQ",
          "content": "## The STAR Method\n\n- **S**ituation — set the scene\n- **T**ask — describe the challenge\n- **A**ction — explain what you did\n- **R**esult — share the outcome",
          "durationMinutes": 12,
          "sortOrder": 2
        }
      ]
    },
    {
      "title": "During the Interview",
      "description": "Making a great impression.",
      "sortOrder": 2,
      "lessons": [
        {
          "title": "Body Language",
          "videoPath": "https://www.youtube.com/embed/dQw4w9WgXcQ",
          "durationMinutes": 8,
          "sortOrder": 1
        },
        {
          "title": "Asking Good Questions",
          "content": "# Questions to Ask the Interviewer\n\n1. What does a typical day look like in this role?\n2. How do you measure success for this position?\n3. What are the biggest challenges the team faces?\n4. What opportunities are there for growth?",
          "durationMinutes": 5,
          "sortOrder": 2
        }
      ]
    }
  ]
}
```

### 6. Markdown tips for `content`

Since JSON doesn't support multi-line strings, use `\n` for line breaks:

| Markdown | JSON string |
|---|---|
| Heading | `"# My Heading"` |
| Subheading | `"## Subheading"` |
| Bullet list | `"- Item one\n- Item two"` |
| Bold | `"**bold text**"` |
| Numbered list | `"1. First\n2. Second"` |
| Paragraph break | `"\n\n"` (double newline) |

### 7. Verify

1. Place the `.json` file in `src/ResetYourFuture.Shared/JSON/Courses/`.
2. Clear existing courses (drop DB or delete rows) and restart the API.
3. Check the API logs — the seeder logs each loaded course.
4. **Admin** → `/admin/courses` — confirm it appears and can be published/unpublished.
5. **Student** → `/courses` — enroll and view lessons.