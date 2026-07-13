// @ts-check
import { defineConfig } from '@playwright/test';

/**
 * Local-only e2e smoke suite (TEST-2). Runs against the real dev server on
 * https://localhost:7090 — Playwright starts it via `dotnet run` if it isn't
 * already running (reuseExistingServer). Deliberately NOT wired into CI: the
 * GitHub workflow is a Linux `dotnet test` job with no SQL Server/LocalDB, and
 * the suite depends on the Development-seeded database and dev-only endpoints.
 */
export default defineConfig({
    testDir: '.',
    // One worker: the call spec drives two logged-in users against shared
    // server state (presence, busy checks), so specs must not interleave.
    workers: 1,
    fullyParallel: false,
    // Blazor Server circuits are timing-sensitive; one retry absorbs a slow boot.
    retries: 1,
    timeout: 90_000,
    reporter: [['list']],
    globalSetup: './global-setup.mjs',
    use: {
        baseURL: 'https://localhost:7090',
        ignoreHTTPSErrors: true,
        // Fake camera/mic so the two-context call test can exchange real media
        // frames without touching (or fighting over) physical devices.
        launchOptions: {
            args: [
                '--use-fake-device-for-media-stream',
                '--use-fake-ui-for-media-stream',
            ],
        },
        trace: 'retain-on-failure',
    },
    webServer: {
        command: 'dotnet run --project ../../src/ResetYourFuture.Web',
        url: 'https://localhost:7090',
        reuseExistingServer: true,
        ignoreHTTPSErrors: true,
        // First boot migrates and seeds the database.
        timeout: 180_000,
    },
});
