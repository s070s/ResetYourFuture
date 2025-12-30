# Reset Your Future — Local Run & Dev Notes

This repository contains a minimal Blazor WebAssembly client, a minimal ASP.NET Core API, and a shared models project used for local end-to-end development.

Prerequisites
- .NET SDK 9.x installed (the solution uses `net9.0`).

Quick build

```powershell
dotnet build ResetYourFuture.sln
```

Run (two options)

- Run both projects manually (recommended for simple evaluation):

```powershell
dotnet run --project src/ResetYourFuture.Api
dotnet run --project src/ResetYourFuture.Client
```

- Or use the provided VS Code task `Run Both (Client hot-reload, API normal)` (Tasks → Run Task).

URLs
- Client (Blazor WASM): https://localhost:7083
- API: https://localhost:7003
- Example endpoint: GET /api/students → https://localhost:7003/api/students

Development — File-based logging

The API is configured to write simple file logs using built-in .NET logging (no third-party packages).

- Log files location: `src/ResetYourFuture.Api/Logs`
- File pattern: `log-YYYY-MM-DD.txt` (daily files)
- Example log entry:

```
[2025-12-30 14:22:13.123] [INFORMATION] [ResetYourFuture.Api.Program] Application started. Logs directory: C:\...\ResetYourFuture\src\ResetYourFuture.Api\Logs
```

Tail logs in PowerShell while running the API:

```powershell
Get-Content -Path .\src\ResetYourFuture.Api\Logs\log-$(Get-Date -Format yyyy-MM-dd).txt -Wait -Tail 200
```

Using `ILogger` in endpoints

Use `ILogger<T>` via dependency injection in minimal API endpoints or in services. The file logger provider is registered in `Program.cs` and writes messages automatically to `src/ResetYourFuture.Api/Logs`.

Example (add this to `Program.cs`):

```csharp
app.MapGet("/api/students", (ILogger<Program> log) =>
{
	log.LogInformation("GET /api/students called");
	var students = new[] { new { Id = 1, FirstName = "George", LastName = "Kokkalis" } };
	return Results.Ok(students);
});
```

Notes:
- Inject `ILogger<MyService>` into constructors to log from services.
- To change verbosity, edit `FileLogger.IsEnabled` in `src/ResetYourFuture.Api/Logging/FileLogger.cs`.

Notes & troubleshooting

- If the client build fails with a static web assets compression error (this can happen on machines with non-ASCII user paths and some .NET 9 SDK versions), a safe development workaround is already applied in `src/ResetYourFuture.Client/ResetYourFuture.Client.csproj`:

```xml
<PropertyGroup>
	<CompressionEnabled>false</CompressionEnabled>
</PropertyGroup>
```

If you change that file or encounter build cache issues, remove the `obj` folder for the client and rebuild:

```powershell
Remove-Item -Recurse -Force src/ResetYourFuture.Client\obj
dotnet build ResetYourFuture.sln
```

Optional (for evaluators)

- The solution includes three projects: `ResetYourFuture.Api`, `ResetYourFuture.Client`, and `ResetYourFuture.Shared`. No external services or secrets are required to run locally.

If you want me to add a short example request script (curl/PowerShell) or enable more log detail, tell me which one and I will add it.