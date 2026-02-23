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



FEATURE 1
Community - Facebook Timeline like component
Separate From Chat Component

FEATURE 2
F.A.Q Page

FEATURE 3
Courses-Transcript PDF Scrolling Along the Video

FIX
change the Courses Section to include Course Items as Cards
use the Cards.md as reference

FIX
Assesments are not a seperate Feature on a separate Tab
They can be attached to a course at the end of a Course

FEATURE
Integration
Calendly/Google Calendar
Mailchimp
Paypal
Stripe
Provider for Emails



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
• https://localhost:7083/profile
profile
• Keep only Disable in Content and Users of Admin Pages and have Delete inside the detailed view of each item

LOCALIZED FULLY
Register.razor
Login.razor
Navmenu.razor




Edge case Tests to be done





WHAT IS INTENTIONALLY NOT BUILT (YET)

Explicitly postponed to protect April deadline:

• Automated psychometric scoring
• B2B portals
• Advanced analytics
• Mobile app
• Full payment enforcement
• Admin Course Creator feature,Courses,Lectures Stored in json files
• AI features using slm like ollama
