// Runs once before the suite: gives both seeded students a known password via
// the dev-only, Development-gated /api/auth/dev/reset-password endpoint. This
// removes any drift between the seeded password (SeedData:StudentPassword at
// first-seed time) and the current database, and keeps real credential values
// out of the suite entirely.
import { request } from '@playwright/test';
import { STUDENTS, TEST_PASSWORD } from './helpers.mjs';

export default async function globalSetup() {
    const api = await request.newContext( {
        baseURL: 'https://localhost:7090',
        ignoreHTTPSErrors: true,
    } );

    try {
        for ( const email of Object.values( STUDENTS ) ) {
            const response = await api.post( '/api/auth/dev/reset-password', {
                data: { Email: email, NewPassword: TEST_PASSWORD },
            } );

            if ( !response.ok() ) {
                throw new Error(
                    `dev password reset failed for ${email}: HTTP ${response.status()}. ` +
                    'The endpoint is Development-only — make sure the server runs with ' +
                    'ASPNETCORE_ENVIRONMENT=Development and the student seed has run.' );
            }
        }
    } finally {
        await api.dispose();
    }
}
