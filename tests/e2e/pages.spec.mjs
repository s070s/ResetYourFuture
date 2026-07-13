// Smoke: a consumer-backed page really shows data. ApiClientBase degrades
// failures to the empty state, so a broken loopback pipeline passes every
// integration test and manifests only as a blank page in the browser — this
// spec is the net for exactly that failure mode. Also covers the bilingual
// experience (culture cookie → Greek render).
import { test, expect } from '@playwright/test';
import { login, waitForCircuit, STUDENTS } from './helpers.mjs';

test( 'courses page renders seeded course cards', async ( { page } ) => {
    await login( page, STUDENTS.alice );

    await page.goto( '/courses' );
    await waitForCircuit( page );

    const cards = page.locator( '.course-card' );
    await expect( cards.first() ).toBeVisible( { timeout: 15_000 } );
    expect( await cards.count() ).toBeGreaterThan( 0 );

    // Each card carries the UI-3 card-link — proving real data flowed through
    // the loopback consumer into the projection, not an empty fallback render.
    await expect( cards.first().locator( 'a.course-card-link' ) ).toHaveAttribute( 'href', /.+/ );
} );

test( 'switching to Greek renders the courses page in Greek', async ( { page } ) => {
    await login( page, STUDENTS.alice );

    await page.goto( '/culture/set?culture=el-GR&returnUrl=/courses' );
    await waitForCircuit( page );

    await expect( page.locator( '.course-card' ).first() ).toBeVisible( { timeout: 15_000 } );

    // The chrome (nav/headings) must actually be localized — assert Greek
    // characters are present in the rendered page.
    const bodyText = await page.locator( 'body' ).innerText();
    expect( bodyText ).toMatch( /[Α-ω]/ );
} );
