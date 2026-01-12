PHASE 3 — VALUE DIFFERENTIATOR (Week 5–6)

Goal: show why this is not just another course site.

5. Psychosocial Assessments (MVP)

Rule of thumb: collect data now, analyze later.

Implement:
• 1 assessment only:

Career clarity / emotional state
• Store answers as JSON
• Show basic summary

Important:
• No complex scoring yet
• This keeps scientific credibility without complexity

Deliverable:
• Assessment → stored → visible to user

6. Admin Content Management

Rule of thumb: avoid hard-coding content.

Implement:
• Admin dashboard:

Create courses

Add lessons

Publish/unpublish

No WYSIWYG yet:
• Plain text + video URL

Deliverable:
• You can manage content without redeploying

PHASE 4 — MONETIZATION SKELETON (Week 7)

Goal: show business viability, not revenue optimization.

7. Subscription Plans (Stub)

Rule of thumb: UI + data first, real payments later.

Implement:
• Plans:

Free

Plus

Pro
• Feature flags per plan
• Mock Stripe integration OR test mode only

Deliverable:
• App behaves as paid platform

PHASE 5 — COMMUNITY & ENGAGEMENT (Week 8)

Goal: support without building social media.

8. Community Integration

Rule of thumb: integrate, do not rebuild.

Implement:
• Discord invite link per plan
• Visible inside dashboard
• Community rules page

Deliverable:
• Community exists without code debt

PHASE 6 — POLISH & DEPLOY (Week 9)

Goal: prototype that judges, users, or partners can use.

9. UX Pass

Rule of thumb: clarity beats beauty.

Fix:
• Navigation
• Empty states
• Copy (aligned with slides)

10. Deployment

Rule of thumb: public URL matters.

Deploy:
• Azure App Service OR cheap VPS
• SQL hosted
• HTTPS
• Environment secrets

Deliverable:
• Live prototype

WHAT IS INTENTIONALLY NOT BUILT (YET)

Explicitly postponed to protect April deadline:

• In-app chat
• Automated psychometric scoring
• B2B portals
• Advanced analytics
• Mobile app
• Full payment enforcement
• Link for confirmation after registration using email or by pressing a button for testing purposes
• Students cant see course contents if they are not enrolled
• Admin instead of Courses have a Course Analytics page
• Admin analytics for logged in users
• Courses,Lectures Stored in json files
• Admin Course Creator feature
• AI features using slm like ollama