# Operations runbook

Operator-facing procedures for running, updating, and recovering the app. Deployment *config*
(secrets, SelfBaseUrl, key ring, email transport) lives in [DEPLOYMENT.md](DEPLOYMENT.md); this
page is the "what do I actually do" companion. All of it targets the single-host deployment this
project is built for.

The app has exactly **two stores of irreplaceable state**: the SQL Server database (users,
enrollments, certificates, chat history, DataProtection keys) and the uploaded files (avatars,
lesson videos, certificate PDFs). Everything else — the binaries, logs, the AI index — is
rebuildable.

---

## Backup & restore (OPS-2)

Back up **both stores together** — a database restored to a different point than the uploads it
references leaves dangling avatar/certificate paths.

**Database** (SQL Server):

```bash
sqlcmd -S <server> -Q "BACKUP DATABASE [ResetYourFutureDb] TO DISK = N'D:\backups\ryf-$(date +%F).bak' WITH INIT, COMPRESSION"
# or, for a portable schema+data package:  sqlpackage /a:Export /scs:"<connection>" /tf:"D:\backups\ryf-$(date +%F).bacpac"
```

**Uploads** (the directory pointed at by `Storage:UploadsPath`, default `<ContentRoot>/App_Data/Uploads`):

```bash
robocopy "<uploads-path>" "D:\backups\uploads-$(date +%F)" /MIR
```

- Schedule both as one Windows Scheduled Task on the demo/host machine if the data matters.
- **Rehearse a restore at least once** — an untested backup is of unknown value. Restore into a
  throwaway database + a scratch uploads folder, point a spare config at them, and confirm the app
  boots and a certificate downloads.

Restore is the reverse: `RESTORE DATABASE ... WITH REPLACE` (or `sqlpackage /a:Import`) and copy the
uploads back, then restart the app.

---

## Update / migration procedure (OPS-3)

Migrations run automatically at startup (`MigrateAsync`, with bounded retry-with-backoff). That is
fine at this scale — but a failed or data-mangling migration can brick startup, and there is no
automatic undo. Always follow this order:

1. **Back up first** (both stores — see above). This is the only rollback for a bad migration.
2. **Review risky migrations before they auto-run.** Generate a reviewable, idempotent SQL script
   with the pinned EF tool and read/apply it manually if a migration touches a lot of data or
   changes column types/indexes:
   ```bash
   dotnet tool restore
   dotnet ef migrations script --idempotent --project src/ResetYourFuture.Infrastructure --startup-project src/ResetYourFuture.Web -o migrate.sql
   ```
   CI already applies the full migration chain against a real SQL Server container (BUILD-2), so a
   SQL-Server-only break is usually caught before you get here.
3. **Deploy the new binaries**, then start the app — migrations apply on boot.
4. **Verify:** `/health/ready` returns healthy, a page that reads data renders, and the startup log
   shows no errors (see the daily digest below).

**Rollback:** there is no `ef database update <previous>` safety net for data changes — restore the
pre-update database backup and redeploy the previous binaries. Keep the previous published output.

> Deploying by "delete the folder and re-copy" is destructive if uploads/logs live inside it. Point
> `Storage:UploadsPath` and `Logging:File:Directory` outside the deploy folder (CLOUD-1) first.

---

## Incident scenarios (OPS-4)

For each: the symptom, then the first diagnostic steps. Most root causes are already documented in
code comments and [DEPLOYMENT.md](DEPLOYMENT.md).

### App won't start
- **Symptom:** process exits immediately; an `InvalidOperationException` lists missing config.
- **Check:** the aggregated startup message (`Startup/StartupConfigValidation.cs`) names every
  missing/invalid key at once — `Jwt:Key` (≥ 32 bytes), `AdminUser:Password`, `Email:Smtp:Host`
  (non-Development), `Sitemap:BaseUrl`, `SeedData:StudentPassword` (dev + seeding). Also validated on
  start: `Assistant`/`WebRtc`/`Email` options (CFG-4).
- **Fix:** set the named keys in `.env` / environment and restart.

### Every page renders empty (no error)
- **Symptom:** pages load but show no data; no visible exception.
- **Cause:** the loopback API call. `SelfBaseUrl` is unset/localhost outside Development, **or** the
  loopback HTTPS cert isn't trusted by the in-process HttpClient (not validated at startup — see
  DEPLOYMENT.md → "SelfBaseUrl and the loopback API call").
- **Check:** `SelfBaseUrl` matches the real bound address; the cert chain is trusted on the host.

### Logins fail / users randomly signed out
- **Symptom:** valid credentials rejected, or sessions dropping.
- **Check:** the DataProtection key ring is in SQL Server (survives redeploy); behind a reverse
  proxy, forwarded headers must be on or the Secure cookie is refused (CLOUD-2 — see the README
  production checklist); a password reset / disable rotates the security stamp and ends existing
  sessions by design.

### AI assistant unavailable
- **Symptom:** the assistant widget reports unavailable; `/health/ready` shows `assistant` degraded.
- **Check:** `Assistant:Enabled` is true; the Ollama service is running and reachable at
  `Assistant:BaseUrl` (keep it loopback); models are pulled. The assistant degrades gracefully —
  the rest of the app is unaffected.

### Email not arriving
- **Symptom:** confirmation/reset emails never delivered.
- **Check:** transport selection (`Startup/ServiceRegistrationExtensions.cs`) — with no
  `Email:Smtp:Host`, Development uses a **stub that only logs** the email (search `STUB EMAIL` in the
  logs for the link); production **fails fast at startup** instead. Verify SMTP egress from the host
  (many VMs block 587 by default).

### Errors are invisible
- **Where to look:** the file logs under `Logging:File:Directory` (default `<ContentRoot>/Logs`,
  `log-YYYY-MM-DD.txt`). A daily **WARN digest** line reports the prior day's error count (LOG-1), and
  each error carries the same `traceId` shown to users on the error page (OBS-3), so a reported
  traceId can be grepped straight to the exception.

### Admin lockout / recovery (see also OPS-6)
- The `.env` admin password is **initial-only** — the seeder creates the account once and never
  updates it. Rotate the admin password in-app, then blank the env value.
- **Break-glass:** if the only admin account is disabled/deleted or its password is lost, set a new
  `AdminUser__Email` (+ `AdminUser__Password`) in the environment and restart — the seeder creates a
  fresh admin for the new email (existing code, no surgery needed).

> **Restarting affects everyone:** this is Blazor Server, so a restart drops every active circuit
> (chat, calls). The graceful-shutdown notice (AVAIL-5) tells connected clients, but active work is
> interrupted — restart during a lull, not mid-demo.
