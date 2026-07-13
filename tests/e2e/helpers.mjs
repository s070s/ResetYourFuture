// Shared constants and Blazor-aware helpers for the e2e smoke suite.

/**
 * Known test password, set for both seeded students by global-setup via the
 * dev-only /api/auth/dev/reset-password endpoint. Not a secret — it only ever
 * applies to regenerable Development seed users on a local machine.
 */
export const TEST_PASSWORD = 'E2e-Smoke-Pass-1!';

/** Deterministic students from ResetYourFuture.Shared/JSON/Students/students.json. */
export const STUDENTS = {
    alice: 'alice.johnson@resetyourfuture.local',
    bob: 'bob.smith@resetyourfuture.local',
};

/**
 * Blazor Server (global InteractiveServer) needs the circuit connected before
 * interactive forms work — window.Blazor appearing is necessary but not
 * sufficient, so a short settle wait follows.
 */
export async function waitForCircuit( page ) {
    await page.waitForFunction( () => window.Blazor !== undefined, { timeout: 20_000 } )
        .catch( () => { } );
    await page.waitForTimeout( 2_500 );
}

/**
 * Logs in through the real /login page. Blazor's InputText binds on `change`,
 * not `input`, so each fill is committed with a Tab blur before submitting.
 * Retries: if the circuit wasn't ready the first submit is silently lost.
 */
export async function login( page, email, password = TEST_PASSWORD ) {
    await page.goto( '/login' );
    await waitForCircuit( page );

    for ( let attempt = 0; attempt < 3; attempt++ ) {
        await page.locator( '#email' ).fill( email );
        await page.keyboard.press( 'Tab' );
        await page.locator( '#password' ).fill( password );
        await page.keyboard.press( 'Tab' );
        await page.waitForTimeout( 500 );
        await page.locator( 'button[type=submit]' ).click();

        try {
            await page.waitForURL( url => !url.pathname.includes( '/login' ), { timeout: 8_000 } );
            return;
        } catch {
            // Submit didn't take (circuit still connecting, or a lost click) — retry.
        }
    }

    throw new Error( `login failed for ${email} after 3 attempts` );
}
