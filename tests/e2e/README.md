# End-to-end smoke suite (Playwright)

Five browser-level scenarios covering what no `WebApplicationFactory` test can reach
(the Blazor circuit, the auth redirect chain, the blank-render-on-API-failure mode,
and WebRTC):

| Spec | Scenarios |
|------|-----------|
| `auth.spec.mjs` | Login lands authenticated; register page links Privacy/Terms from the consent block |
| `pages.spec.mjs` | Courses page renders seeded course cards (anti-blank-render); Greek culture switch renders Greek |
| `call.spec.mjs` | Two isolated browser contexts (two users) place and connect a real video call with fake media, assert `RTCPeerConnection.connectionState === 'connected'` and remote frames flowing, then hang up |

## Prerequisites

- Node.js 18+ and the .NET 10 SDK
- SQL Server LocalDB with the Development database seeded (any prior `dotnet run` of
  `ResetYourFuture.Web` does this; `SeedData:Enabled=true` so the JSON students exist)

## Running

```bash
cd tests/e2e
npm install
npx playwright install chromium   # first time only
npx playwright test
```

Playwright starts the dev server itself (`dotnet run --project ../../src/ResetYourFuture.Web`)
and reuses it if already running on `https://localhost:7090`.

## Notes

- **Seeded passwords:** global setup resets the passwords of the two seeded students
  (`alice.johnson@` / `bob.smith@resetyourfuture.local`) to a known test value through the
  Development-only `/api/auth/dev/reset-password` endpoint. This only touches regenerable
  local seed data and keeps real credential values out of the suite.
- **Not in CI:** the `test` job is a Linux `dotnet test` job with no database. CI does now have a
  SQL Server container, but only in the separate `migrations` job, which applies the migration
  chain and never boots or seeds the app. The suite depends on the seeded Development database and
  dev-only endpoints, so it stays a local, on-demand suite by design.
- **Serial by design:** one worker — the call spec drives two logged-in users against shared
  server state (presence/busy checks), so specs must not interleave.
