// Smoke: the login → DataProtection ticket → /auth/complete → cookie → circuit
// re-auth chain end-to-end (only its server halves are covered by
// MinimalEndpointsTests), plus the registration consent links (COMP-1).
import { test, expect } from '@playwright/test';
import { login, STUDENTS } from './helpers.mjs';

test( 'login completes and lands authenticated', async ( { page } ) => {
    await login( page, STUDENTS.alice );

    // Off /login and actually authenticated: the authenticated layout renders
    // the avatar dropdown and the main nav; an anonymous circuit would bounce
    // back to /login when hitting an [Authorize] page.
    expect( page.url() ).not.toContain( '/login' );
    await page.goto( '/courses' );
    await expect( page ).not.toHaveURL( /login/ );
    await expect( page.locator( 'main' ) ).toBeVisible();
} );

test( 'register page links the privacy policy and terms from the consent block', async ( { page } ) => {
    await page.goto( '/register' );

    const consentHelp = page.locator( '.form-check .form-text' );
    await expect( consentHelp.locator( 'a[href="/privacy"]' ) ).toBeVisible();
    await expect( consentHelp.locator( 'a[href="/terms"]' ) ).toBeVisible();

    // Both pages actually serve content (not a blank render or 404 re-execute).
    await page.goto( '/privacy' );
    await expect( page.locator( '.legal-content h1' ) ).toBeVisible();
    await page.goto( '/terms' );
    await expect( page.locator( '.legal-content h1' ) ).toBeVisible();
} );
