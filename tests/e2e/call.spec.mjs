// Smoke: a two-user video call really connects. Two ISOLATED browser contexts
// (separate cookie jars = two logged-in users) in one Chromium with fake media
// devices — two tabs would share a session and two real browsers would fight
// over the physical webcam. Asserts a real WebRTC connection (peer connection
// state + remote frames flowing), not just overlay visibility.
import { test, expect } from '@playwright/test';
import { login, waitForCircuit, STUDENTS } from './helpers.mjs';

test( 'two-context video call connects with fake media', async ( { browser } ) => {
    const contextA = await browser.newContext( { ignoreHTTPSErrors: true } );
    const contextB = await browser.newContext( { ignoreHTTPSErrors: true } );
    const alice = await contextA.newPage();
    const bob = await contextB.newPage();

    try {
        await login( alice, STUDENTS.alice );
        await login( bob, STUDENTS.bob );

        // Bob just needs a connected CallService hub (CallOverlayHost mounts on
        // every authenticated page) to be reachable and receive the ring.
        await bob.goto( '/chat' );
        await waitForCircuit( bob );

        await alice.goto( '/chat' );
        await waitForCircuit( alice );

        // Open (or create) the conversation with Bob.
        const existing = alice.locator( '.conversation-item', { hasText: 'Bob' } ).first();
        if ( await existing.count() > 0 ) {
            await existing.click();
        } else {
            await alice.locator( '.sidebar-header-actions .btn-primary' ).click();
            const search = alice.locator( '.modal-body input.form-control' );
            await search.fill( 'Bob' );
            const bobItem = alice.locator( '.user-picker-item', { hasText: 'Bob Smith' } ).first();
            await expect( bobItem ).toBeVisible( { timeout: 10_000 } );
            await bobItem.click();
        }

        const callButton = alice.locator( '.btn-video-call' );
        await expect( callButton ).toBeVisible( { timeout: 15_000 } );

        // The call button silently no-ops until CallService's own hub client is
        // connected — retry-click until Bob's toast appears.
        await expect
            .poll( async () => {
                await callButton.click().catch( () => { } );
                await bob.waitForTimeout( 1_500 );
                return bob.locator( '.incoming-call-toast' ).isVisible();
            }, { timeout: 30_000 } )
            .toBe( true );

        await bob.locator( '.incoming-call-toast .btn-success' ).click();

        await expect( alice.locator( '.active-call-overlay' ) ).toBeVisible( { timeout: 20_000 } );
        await expect( bob.locator( '.active-call-overlay' ) ).toBeVisible( { timeout: 20_000 } );

        // Real connection on both sides: every RTCPeerConnection reports
        // 'connected', and the REMOTE tile has frames flowing (videoWidth > 0)
        // — fake devices produce a moving test pattern, so frames prove media
        // is actually traversing the peer connection.
        for ( const page of [ alice, bob ] ) {
            await expect
                .poll( () => page.evaluate( () => {
                    const peers = window.webrtcInterop?._peers ?? {};
                    const keys = Object.keys( peers );
                    return keys.length > 0 &&
                        keys.every( k => peers[ k ].pc.connectionState === 'connected' );
                } ), { timeout: 30_000 } )
                .toBe( true );

            await expect
                .poll( () => page.evaluate( () => {
                    const remoteVideos = [ ...document.querySelectorAll(
                        '.participant-tile:not(.local-tile) video' ) ];
                    return remoteVideos.length > 0 &&
                        remoteVideos.every( v => v.videoWidth > 0 );
                } ), { timeout: 30_000 } )
                .toBe( true );
        }

        // Hang up cleanly; both overlays close (Bob's via the CallEnded broadcast).
        await alice.locator( '.control-btn.hang-up' ).click();
        await expect( alice.locator( '.active-call-overlay' ) ).toBeHidden( { timeout: 15_000 } );
        await expect( bob.locator( '.active-call-overlay' ) ).toBeHidden( { timeout: 15_000 } );
    } finally {
        await contextA.close();
        await contextB.close();
    }
} );
