PHASE 3 — VALUE DIFFERENTIATOR (Week 5–6)


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
• Localization of all Razor Pages and Client API Messages,Error Messages
• If credentials are invalid in the login page do not pop up browser dialog for saving the credentials
• When completing courses remove the 🎉 Course Completed! from the top and make a beautiful animation in the bottom with a big card
• Admins have a Course Analytics route in the nav menu that shows Admin analytics for logged in users,
subscribed users,completion analytics of users


• https://localhost:7083/admin/courses Edit,Publish do not Work
• Courses should be updated on each startup from the json files
• WYSIWYG Editor for Admin Course Creator feature that lets you add Modules Lessons and Courses with or without Videos Text and PDF formatted(markup) or not
• https://localhost:7083/profile
profile
"The FirstName field is required."
"The LastName field is required."
• https://localhost:7083/profile add an avatar photo and the Display Name
• Profile Avatar with image,when clicked a small drop down is shown with User Profile that routes to Profile page and Log Out that Logs the User Out
• Move Log out Button from Profile to a User Menu(Profile Pic avatar)
• Different Password and Confirm Password pass through Registration (Password is passed in)
• Keep only Disable in Content and Users of Admin Pages and have Delete inside the detailed view of each item
• Feature in Admin Users screen to Login as specific Students in a separate browser window


LOCALIZED FULLY
Register.razor
Login.razor
Navmenu.razor








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