# Copilot instructions — ResetYourFuture (Blazor WASM, .NET 9)

Purpose
- Produce suggestions and code edits that prioritize: Simplicity, Speed/Optimization, Effectiveness, Best Architecture, and Separation of Concerns.
- Prefer small, safe changes (single-responsibility, minimal surface area) and provide migration steps for larger refactors.

Workspace assumptions
- Target: .NET 9
- Project type: Blazor WebAssembly + hosted/api patterns
- Prefer Blazor idioms over Razor Pages or MVC

General rules for generated code
- Match existing code style and naming. Prefer C# by default.
- Enable and respect nullable reference types.
- Use `async`/`await` and `CancellationToken` for all I/O; avoid blocking calls.
- For frequently awaited trivial operations consider `ValueTask` only when measurable benefit exists.
- Use `ConfigureAwait(false)` in library/infra code; avoid it in UI-bound Blazor component code.
- Favor dependency injection: typed `HttpClient`, interfaces (e.g., `IUserService`), and minimal public surface.
- Keep components small and focused. Prefer composition over inheritance.
- Keep logic out of `.razor` markup: place business logic in partial class (`.razor.cs`) or services.
- Use `EventCallback<T>` for parent-child communication; prefer parameters over service-locals for ephemeral state.
- Use `IAsyncDisposable` for async cleanup; call `DisposeAsync` when needed.
- Use `@key` on repeating elements to stabilize UI diffing.
- For long lists use `<Virtualize>` or incremental loading.

Architecture and separation of concerns
- Follow layered structure:
  - `ResetYourFuture.Client` — UI (Blazor components, page routing, minimal view-models)
  - `ResetYourFuture.Shared` — DTOs, contracts
  - `ResetYourFuture.Application` — interfaces, use-cases, validation
  - `ResetYourFuture.Domain` — domain entities, business rules
  - `ResetYourFuture.Infrastructure` — HTTP clients, EF Core, external adapters
- Components call services (application layer) which map DTOs to domain models. No direct HTTP calls from components.
- Keep mapping and transformation explicit; prefer small mappers (Mapster/AutoMapper at boundaries).

Performance and optimization
- Avoid large render trees and unnecessary re-renders: minimize state that triggers renders; use `ShouldRender()` when needed.
- Batch state changes with `InvokeAsync(StateHasChanged)` and avoid calling `StateHasChanged` in tight loops.
- Dispose event handlers and subscriptions to avoid leaks.
- Reduce allocations: reuse arrays/collections where safe, use pooling for heavy throughput paths.
- Minimize JSInterop round-trips; batch DOM operations or do them in a single call.
- For Blazor WebAssembly, reduce startup size: prefer trimmed libraries, static linking, and avoid unused packages.


Security and resilience
- Validate inputs on server and client boundaries.
- Use typed HTTP clients with proper timeouts and retry/backoff for transient failures.
- Never store secrets in client; use secure token flows for auth.
- Sanitize and encode any user-supplied content before rendering.

PRs and commits
- Keep commits small and focused. One logical change per PR.
- Include a short description and testing notes in PRs.
- When suggesting code, include a minimal diff and list of changed files.

How Copilot should respond in this repo
- Prefer minimal, copy-paste-ready code. Use existing project structure and types when present.
- When modifying or adding files, always include the file path in the generated code block.
- When uncertain or missing context, read the active file first and ask a focused question rather than guessing. Use available workspace artifacts.
- Suggest measurable performance or complexity trade-offs when recommending alternatives.

Visual Studio tips (use when relevant)
- Run analyzers via __Analyze > Run Code Analysis__.
- Apply formatting via __Edit > Advanced > Format Document__ or __Code Cleanup__.
- Use the Test Explorer and the __Run All Tests__ action in CI.

Remember
- Keep UX simple, secure, and performant.
- Prefer clear, maintainable code over clever micro-optimizations unless profiling proves otherwise.
- When asked to remember or adopt a team preference, persist it as a memory.
