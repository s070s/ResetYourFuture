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




OtherTODO
• Using this prompt add comments throughout the solution...
You are a principal .NET Engineer and the author of this solution
You are free to search the solution for context
add comments for a junior .NET to understand this file
one line per comment
• Students cant see course contents if they are not enrolled
• Admin do not have a Courses Route in the navMenu, Admins cant enroll in courses
• If credentials are invalid in the login page do not pop up browser dialog for saving the credentials
• Store the courses,Lectures in json files instead of seeding them
• When completing courses remove the 🎉 Course Completed! from the top and make a beautiful animation in the bottom with a big card
• In Login Email after input and losing focus the input field turns white (css class form-control modified valid)
<input id="email" class="form-control modified valid" _bl_2="">
• See if this is implemented already.. if not do implement it yourself If email already exists on the Database return in Registration that the user already exists
• After Login Remember me checkbox and functionality
• In Login and Registration make sure to trim space,tab at the front and at the end in input data
(username,password especially) after clicking submit and before sending the http
• Link for confirmation after registration using email or by pressing a button for testing purposes
• Admins have a Course Analytics route in the nav menu that shows Admin analytics for logged in users,
subscribed users,completion analytics of users
• Admin route in navigation menu provides a table where the Admin can change user credentials,remove users 
completely








WHAT IS INTENTIONALLY NOT BUILT (YET)

Explicitly postponed to protect April deadline:

• In-app chat
• Automated psychometric scoring
• B2B portals
• Advanced analytics
• Mobile app
• Full payment enforcement
• Admin Course Creator feature,Courses,Lectures Stored in json files
• AI features using slm like ollama