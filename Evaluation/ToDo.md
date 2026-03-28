PRIORITY FIX

-Section in the Home,Landing Page for
Blog Type Articles editable from the Admin Panel

-Testimonials on landing page editable from the Admin Panel

-For Logged Users Timeline like Facebook 
where people can post their progress,post text images and videos,share their thoughts,ask questions and interact with each other,with comments
Include a like/reaction system for posts and comments to increase engagement

-Include a F.A.Q page with common questions and answers about the platform, courses, assessments, and account management editable from the admin panel

-Like/reaction system for chat messages

-Sending Videos,Screenshots and Images in the Chat

-Admin Moderation Features for the Community Timeline and Chat (Delete/Disable Posts,Comments,Messages)

-Admin controllable pop up for
Announcements and News that can be shown to all users or specific groups of users

-Assessments can be attached to courses as Lessons and can be taken at the end of the course

- Popup for feedback collection from courses,assessments.Results visible on the Admin Analytics Page

- Admin controllable profanity filter for the Community Timeline and Chat

- SEO Optimization

-Better Searching for First Last Names emails in one bar (if @ is present search prefix and suffix as email)

-Sorting for Table Components both in  Backend and in FrontEnd

-Change to .NET 10

-Change to Blazor Server for better SEO and performance

-Suggestions for collapsable UI Elements

-Make the Profile Page similar to Instagram



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
• Certificates for Users at users/certificates
• Localization of all Razor Pages and Client API Messages,Error Messages
• Localization of Content either by adding a second version of the content in another language or by automatically localizing the content using a translation API
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
AdminAnalytics.razor
AdminAssessmentEdit.razor
AdminAssessments.razor
AdminAssessmentSubmissions.razor
AdminCourseEdit.razor
AdminCourses.razor
AdminLessonEdit.razor
AdminUsers.razor
AssessmentForm.razor
AssessmentHistory.razor
Assessments.razor
Billing.razor
Chat.razor
CourseDetail.razor
Courses.razor
Disabled.razor
ForgotPassword.razor
Home.razor
AvatarDropdown.razor



WHAT IS INTENTIONALLY NOT BUILT (YET)

Explicitly postponed to protect April deadline:

• Automated psychometric scoring
• B2B portals
• Advanced analytics
• Mobile app
• Full payment enforcement
• AI features using slm like ollama

