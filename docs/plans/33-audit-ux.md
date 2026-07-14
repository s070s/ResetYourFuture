# Audit: UX (User Experience)

| | |
|---|---|
| Finding prefix | UX |
| Created | 2026-07-11 |
| Scope | Flow-level experience: navigation structure and discoverability, feedback after actions, error/empty/loading states, form validation experience, confirmation patterns, bilingual (EN/EL) experience across the 32 routable pages |
| Delegated | Component-level visual correctness/a11y → 32 (UI). Localization *infrastructure* (culture cookie, resx pipeline) → 39 (CFG) — missing/hardcoded *strings* are owned here. Table sorting → implemented across every admin/student table (former plan 10). Visual polish (motion, skeletons, tokens) → implemented (see the plan-13 note in [00-INDEX.md](00-INDEX.md)). Circuit crash/reconnect behavior → 26 (REL). |

## 1. Methodology

Static analysis only — no app launch or browser. Examined:

- All page code-behinds (`Pages/*.razor.cs`) for message handling, error catching, and post-action feedback; repo-wide sweep for hardcoded English string assignments (`=\s*\$?"[A-Z]…"` over `*.razor.cs` → 103 matches in 21 files, individually reviewed in AdminUsers, AdminCourses, AdminCourseEdit, AdminBlog(-Editor), AdminAssessments(-Edit), AdminCategories, AdminTestimonials(-Editor), AdminLessonEdit, Billing, Courses, CourseDetail, ForgotPassword, Login, Pricing, AssessmentForm, AdminAnalytics).
- Navigation surface: `Routes.razor`, `App.razor`, `Layout/NavMenu.razor`, `Layout/MainLayout.razor`, full `@page` route inventory (36 routes).
- Feedback components and every usage site: `DismissibleAlert` (11 usages), `ConfirmModal` (7 usages), inline confirm patterns (3 pages), `PageStateContainer` (0 usages — dead), `LoadingSpinner`.
- Forms: Login, Register (+ `RegisterRequestDto`/`LoginRequestDto` DataAnnotations), ForgotPassword, Profile, AssessmentForm, AdminCategories/AdminUsers modal forms, AdminCourseEdit.
- Bilingual posture: all 18 resx pairs in `src/ResetYourFuture.Shared/Resources/` (EN/EL key counts compared — 100% parity), date/time format strings across pages.
- NOT examined: actual Greek translation quality (native review), live SignalR/chat flows, payment redirects — not verifiable statically.

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 9 |
| Low | 2 |
| Info | 1 |

The flow fundamentals are strong where they were done deliberately: the localization system is exemplary (18 EN/EL resx pairs with perfect key parity, localized relative-time formatting, culture-aware `PaginationShowingFormat`), most list pages have real empty states with calls to action, several consumer pages implement the full loading → error → retry chain (`Courses`, `Billing`, `CourseDetail`, `Profile`, `LessonViewer`, and now `AssessmentForm` all offer a localized error state), and destructive actions are gated by the shared `ConfirmModal` (now including Pricing), and action feedback is now localized end-to-end (UX-1, fixed — the ~100 hardcoded strings were swept into the resx pattern). The remaining Medium items are about feedback *presentation* (severity/placement, load-failure states) rather than its language.

## 3. Findings

### UX-5: Action feedback is a single message slot styled "info", placed at the bottom of the page, with inconsistent dismissibility  [Medium] [Effort: M]
- **Evidence:** The admin pages funnel every outcome — success ("User deleted") and failure ("Error deleting user") — into one `message` string rendered by `DismissibleAlert` with the default `AlertType="info"` (`DismissibleAlert.razor:16`): 9 of 11 usages never set a type (`AdminUsers.razor:117`, `AdminCourses.razor:85`, `AdminBlog.razor:89`, `AdminCategories.razor:62`, `AdminAssessments.razor:82`, `AdminAssessmentEdit.razor:186`, `AdminCourseEdit.razor:258`, `AdminLessonEdit.razor:137`, `AdminTestimonials.razor:107`); only `AdminAssessmentSubmissions.razor:89` and `MyCertificates.razor:64` pass `AlertType="danger"`. The alert is rendered **after** the table + pagination toolbar, i.e. below the fold on long pages. Dismissibility is arbitrary: `AdminUsers.razor:117`, `AdminBlog.razor:89`, `AdminTestimonials.razor:107` set `Dismissible="false"` (while still wiring a never-reachable `OnDismiss`), others are dismissible; non-dismissible messages persist until the next action replaces them.
- **Impact:** Errors look like neutral notices; success and failure are visually identical; the feedback often appears offscreen where the user isn't looking (they clicked a button at the top/middle of the table); stale "User deleted" banners linger indefinitely on some pages.
- **Recommendation:** Split `message` into `(text, severity)` — `DismissibleAlert` already accepts `AlertType` — success → `success`, failure → `danger`; render the alert directly under the page `<h1>` (consistent, visible position); pick one dismissibility policy (dismissible everywhere, since `role="alert"` already announces).

### UX-6: Load-failure states are inconsistent — several pages spin forever; the purpose-built state component is used by zero pages  [Medium] [Effort: M]
- **Evidence:** Good pattern (loading → error + localized "Try again" → empty → content): `Courses.razor:8-20`, `Billing.razor:9-17`, `CourseDetail.razor:6-20`, `Profile.razor:11-22`, `LessonViewer.razor:6-19`. Broken pattern: `AdminAnalytics.razor.cs:14-24` catches the load exception, logs, leaves `stats == null` → `AdminAnalytics.razor:8-11` shows `LoadingSpinner` forever with no error or retry. `AdminUsers.razor.cs:53-68` handles only the 403 case (leaving spinner *and* "Access denied" alert rendered together, since `pagedResult` stays null → `AdminUsers.razor:17-19`); any other exception is unhandled and takes down the circuit. `Assessments.razor:22-25` and `Pricing.razor:26-29` show the error but offer no retry. Meanwhile `Shared/Components/Layout/PageStateContainer.razor` implements exactly this chain and has **zero usages** (repo-wide grep).
- **Impact:** On admin pages a transient API failure looks like an eternal load; users cannot recover without a manual full refresh; each new page re-invents the chain with a different subset of states.
- **Recommendation:** Standardize on the `_loading/_error/TryAgain` trio (the Courses implementation) — either adopt `PageStateContainer` everywhere or delete it and document the hand-rolled pattern; minimum fix: add error+retry branches to AdminAnalytics and catch-all handling in `AdminUsers.LoadUsers`.

### UX-7: Logged-in users hit a dead-end home page; blog and footer exist only for anonymous visitors  [Medium] [Effort: M]
- **Evidence:** `Pages/Home.razor:9-15` — the entire authenticated home is a welcome heading plus one paragraph; the hero, testimonials, blog preview, CTA, and the site footer (with social links) all live in the anonymous `else` branch (`Home.razor:16-429`). There is no `/blog` index route at all (route inventory: only `/blog/{Slug}`, `BlogArticle.razor:1`), and no nav link to blog for anyone — articles are reachable solely through the anonymous home's preview cards (`Home.razor:315-372`). `MainLayout.razor` renders no footer, so authenticated users never see one anywhere. Anonymous nav offers only Home/Login/Register (`NavMenu.razor:31-47`) even though `/pricing` is public (`Pricing.razor:1-2` — no `[Authorize]`).
- **Impact:** After login, the home page offers no path to courses, progress, or content — students navigate solely via the nav bar; published blog content is invisible to the paying audience and to any anonymous visitor who scrolls past the preview; anonymous visitors can't discover the pricing page that exists for them.
- **Recommendation:** (a) Give the authenticated home continue-learning shortcuts (enrolled courses / latest lesson — data already available via existing consumers); (b) add a public `/blog` index (list endpoint already exists for the home preview) and a nav link; (c) add Pricing to the anonymous nav; (d) render the footer from MainLayout instead of inside Home's anonymous branch.

### UX-8: Role-denied users are told to "log in" while already logged in  [Medium] [Effort: S]
- **Evidence:** `Routes.razor:4-6` — the single `<NotAuthorized>` branch renders `@GlobalRes.NotAuthorizedMessage <a href="/login">…` as a bare paragraph for **both** unauthenticated visitors and authenticated users lacking the role (e.g. a Student opening `/admin/users`, an Admin opening the Student-only `/assessments`).
- **Impact:** The message is wrong for the second (common) case — following its advice loops the user through a login they already completed; the bare `<p>` also renders without any page framing.
- **Recommendation:** Branch on `context.User.Identity?.IsAuthenticated` inside `NotAuthorized` (the `AuthorizeRouteView` context provides it): unauthenticated → login link with returnUrl; authenticated → localized "you don't have access to this area" + link home. Style it like `Disabled.razor`.

### UX-9: Admin editors silently discard unsaved work on navigation  [Medium] [Effort: M]
- **Evidence:** No `NavigationLock` / `RegisterLocationChangingHandler` / `beforeunload` usage anywhere in app code (repo grep matches only vendored Bootstrap). Editors with substantial form state: `AdminCourseEdit.razor` (titles + two Quill editors + tier/category), `AdminBlogEditor`, `AdminAssessmentEdit`, `AdminTestimonialEditor`, `Profile`. Each has an always-armed "Back"/Cancel button (e.g. `AdminCourseEdit.razor:10`) and nav links remain live.
- **Impact:** One click on any nav item wipes a half-written blog article or course description with no warning — the costliest data-loss path in the admin experience, and Quill content is not recoverable.
- **Recommendation:** Add `<NavigationLock ConfirmExternalNavigation OnBeforeInternalNavigation=…/>` gated on a dirty flag in the four admin editors (dirty = any bound field differs from loaded snapshot; Quill exposes content via the existing interop). Localize the confirm prompt.

### UX-10: Two competing delete-confirmation patterns split the admin experience  [Medium] [Effort: M]
- **Evidence:** Shared `ConfirmModal` (with consequence text, busy state, Escape handling): `AdminCourses.razor:77-83`, `AdminAssessments.razor:74-80`, `AdminCategories.razor:94-100` (even interpolates affected course/assessment counts), `AdminCourseEdit.razor:250-256`, `AdminLessonEdit.razor:129-135`, `Chat.razor:47-58`, `Billing.razor:85-100`. Inline button-swap (Delete → Confirm/Cancel pair in the row): `AdminUsers.razor:89-98`, `AdminBlog.razor:62-70`, `AdminTestimonials.razor:80-88`.
- **Impact:** The same destructive verb behaves differently page to page; the inline variant states no consequences (user deletion is the most destructive of all — cascades to enrollments/submissions), never times out, and shifts row-button layout (misclick risk on the reflowed buttons).
- **Recommendation:** Converge on `ConfirmModal` (majority pattern, already localized on its consumers) for AdminUsers/AdminBlog/AdminTestimonials, with a consequences line as in AdminCategories.

### UX-11: Form-validation experience is split-brain: unlocalized DataAnnotations on auth forms, ad-hoc English checks in modals, none on Profile  [Medium] [Effort: M]
- **Evidence:** Login/Register use `DataAnnotationsValidator` + `ValidationMessage` (`Register.razor:46-105`) but the DTO attributes carry no localized messages — `Application/DTOs/Auth/RegisterRequestDto.cs:9-43` yields stock English ("The Email field is required.") and one hardcoded `ErrorMessage = "You must consent to data processing."`; the DTO's own doc comment says the password needs "at least one uppercase letter and one digit" while the attribute checks only `MinLength(8)`, so compliant-looking input fails server-side after submit. Modal/inline forms skip the framework entirely with hardcoded English checks: `AdminUsers.razor.cs:155-159`, `AdminTestimonialEditor.razor.cs:110-118`, `AdminLessonEdit.razor.cs:123-126`, `AdminCategories` (save disabled until NameEn non-empty, no message). Profile's change-password form (`Profile.razor:66-80`) has no client validation or rules hint at all — mismatch/short passwords round-trip to the API. Modal form labels also lack `for`/`id` association (`AdminCategories.razor:73-80`, `AdminUsers.razor:135-142`), unlike the auth pages.
- **Impact:** In Greek, every validation message on the two most-used public forms appears in English; password rules surprise users post-submit; each admin form invents its own validation timing and wording.
- **Recommendation:** Add `ErrorMessageResourceType/ErrorMessageResourceName` (pointing at `ErrorMessagesRes`) to the auth DTO attributes and align the password attribute with the real Identity policy (regex or custom attribute); reuse `GlobalRes.PasswordRulesHint` (already shown on Register, `Register.razor:83`) on Profile and the admin reset-password modal; associate modal labels with inputs.

### UX-12: Date/time presentation is inconsistent and partly culture-blind  [Medium] [Effort: S]
- **Evidence:** Five competing styles: US 12-hour `"MMM dd, yyyy h:mm tt"` (`AssessmentHistory.razor:27,47,63`), `"dd MMM yyyy HH:mm"` (`Billing.razor:129`), `"MMMM dd, yyyy"` (`MyCertificates.razor:30`, `VerifyCertificate.razor:33`), `"dd/MM/yyyy"` (`Profile.razor:55`), culture-aware `"d"`/`"g"` (admin tables, `AdminAssessmentSubmissions.razor:35`). In Greek culture the hardcoded US patterns produce "Ιουλίου 10, 2026 3:05 μμ" — wrong word order and a 12-hour clock Greeks don't use. Also `AdminBlog.razor:51` formats `PublishedAt` **without** `ToLocalTime()` while `CreatedAt` on the same row gets it (`:50`) — the two columns can show different dates for the same moment near midnight; `AssessmentHistory.razor:63` (modal) likewise omits `ToLocalTime()` while line 47 (table) applies it.
- **Impact:** The same timestamp renders four different ways across a single user journey (submit assessment → history → certificate); Greek users get anglicized dates on exactly the student-facing pages.
- **Recommendation:** Standardize on culture-aware standard formats (`"d"`, `"g"`, `"f"`) everywhere user-facing, and apply `ToLocalTime()` uniformly before formatting (one sweep, ~12 call sites).

### UX-13: List-management affordances are unevenly distributed across admin tables  [Medium] [Effort: M]
- **Evidence:** Search exists only on AdminUsers (`AdminUsers.razor:10-13`, debounced 300 ms in `AdminUsers.razor.cs:77-93`) and AdminBlog (`AdminBlog.razor:10-14`); AdminCourses, AdminAssessments, AdminCategories, AdminTestimonials offer none — finding one course among pages of rows means paging manually. (Column sorting, by contrast, is now rolled out across all these tables — former plan 10 — so search is the remaining gap.) Student-side, `Courses`/`Assessments` get category chips + search via `CategoryFilterBar` while `MyCertificates` and `AssessmentHistory` have no filter at all (acceptable at small counts, noted for symmetry).
- **Impact:** Admins working the two largest content tables (courses, assessments) lack the retrieval tools the users table already proves out; the asymmetry also makes the UI feel unfinished.
- **Recommendation:** Clone the AdminUsers debounced-search chain (input → consumer `search` param → service `ApplySearch`) onto AdminCourses and AdminAssessments first; AdminBlog already threads `search` through its whole stack as the reference for the API side.

### UX-14: Two student pages have no browser tab title  [Low] [Effort: S]
- **Evidence:** `<PageTitle>` exists on 30 of 32 pages; missing on `AssessmentHistory.razor` and `AssessmentForm.razor` (grep). `AssessmentForm.razor:4` also renders its `<h1>` as empty while the assessment loads (`@assessment?.Title`).
- **Impact:** The tab keeps the previous page's title during and after navigation — confusing with multiple tabs and hurting history/bookmarks; the empty `<h1>` gives `FocusOnNavigate` (Routes.razor:8) a blank announcement target.
- **Recommendation:** Add `<PageTitle>@AssessmentRes.HistoryTitle` / assessment title with localized fallback (`LessonViewer.razor:4` shows the fallback pattern).

### UX-15: Auth/profile/chat pages have no `h1`, so post-navigation focus is never moved there  [Low] [Effort: S]
- **Evidence:** `Routes.razor:8` — `<FocusOnNavigate Selector="h1"/>`. Login (`Login.razor:6`), Register (`Register.razor:7`), ForgotPassword (`ForgotPassword.razor:6`), Profile (`Profile.razor:7`) use `<h2>` as their top heading; Chat (`Chat.razor`) has no heading element at all. All other 27 pages have an `h1`.
- **Impact:** Keyboard/screen-reader users navigating to these five pages get no focus move and no announced page context (the mechanism works everywhere else); document outline starts at h2.
- **Recommendation:** Promote the top heading to `<h1 class="h2">` (the admin pages' exact idiom, e.g. `AdminUsers.razor:8`) and give Chat a visually-hidden `h1` with `ChatRes.PageTitle`.

### UX-16: Mock-checkout success on Pricing gives no confirmation feedback  [Info] [Effort: S]
- **Evidence:** `Pricing.razor.cs:52-56` — when checkout returns no redirect URL (the mock/dev path), the code silently reloads `_currentStatus`; the only visible change is the button flipping to "Current plan". The real-checkout path lands on `SubscriptionSuccess` with a full celebration screen (`SubscriptionSuccess.razor:13-31`).
- **Impact:** Dev/demo users (the project's actual audience per 00-INDEX) upgrade a plan and get no explicit "you're now on X" acknowledgment.
- **Recommendation:** Set a localized success message (the `_cancelMessage` slot at `Pricing.razor:119-124` already renders both severities) or navigate to `/subscription/success`.

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| UX-8 | Medium | S | Split NotAuthorized messaging: needs-login vs. lacks-role |
| UX-12 | Medium | S | Standardize on culture-aware date formats + uniform `ToLocalTime()` |
| UX-5 | Medium | M | Type + reposition action feedback (success/danger under the h1); one dismissibility policy |
| UX-6 | Medium | M | Error+retry states on AdminAnalytics/AdminUsers; adopt or delete PageStateContainer |
| UX-7 | Medium | M | Authenticated home dashboard; public /blog index + nav links; footer in MainLayout |
| UX-9 | Medium | M | NavigationLock dirty-guard on the four admin editors |
| UX-10 | Medium | M | Converge AdminUsers/AdminBlog/AdminTestimonials deletes on ConfirmModal |
| UX-11 | Medium | M | Localized DataAnnotations on auth DTOs; align password rule; validate Profile password form |
| UX-13 | Medium | M | Roll AdminUsers-style search onto AdminCourses/AdminAssessments |
| UX-14 | Low | S | Add PageTitle to AssessmentHistory and AssessmentForm |
| UX-15 | Low | S | Promote h2→h1 on auth/profile pages; hidden h1 on Chat |
| UX-16 | Info | S | Acknowledge mock-checkout success on Pricing |

## 5. Related Findings Elsewhere

- **UI-2 (32)** — the `#blazor-error-ui` contrast defect; its hardcoded English text is counted under UX-1 here.
- **UI-5 (32, fixed)** — modal focus trap/restore; the *flows* those modals guard include UX-10 here (Pricing's downgrade flow, former UX-4, now also uses this modal).
- **UI-9 (32, fixed)** — Chat's unlabeled raw spinner (component-level face of the loading-state inconsistency in UX-6).
- **UI-10 (32, fixed)** — unlabeled search inputs belonging to the search flows in UX-13.
- **Table sorting (former plan 10, implemented)** — sortable headers landed on every admin/student table (incl. AssessmentHistory and the migrated Billing table); deliberately not re-reported in UX-13.
- **Deferred visual polish (former plan 13; see the plan-13 note in [00-INDEX.md](00-INDEX.md))** — the remaining skeleton-loader swaps will change the *look* of the loading states audited in UX-6; the missing error/retry branches remain this report's items either way.
- **SEC (25)** — `(MarkupString)` rendering of course/assessment/blog descriptions and `ex.Message` exposure have security dimensions owned there; UX-1 covers only the language/feedback aspect.
- **REL (26)** — unhandled exceptions in `OnInitializedAsync` (e.g. `AdminUsers.LoadUsers` non-403 failures) crashing the circuit; UX-6 covers only what the user should see instead.
- **CFG (39)** — the localization pipeline itself (culture cookie, resx build, hand-edited Designer.cs) is that report's territory; this report established the pipeline works (18/18 EN-EL key parity) and the gaps are content-level.
