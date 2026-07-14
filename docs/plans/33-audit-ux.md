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
| Medium | 0 |
| Low | 2 |
| Info | 1 |

The flow fundamentals are strong where they were done deliberately: the localization system is exemplary (18 EN/EL resx pairs with perfect key parity, localized relative-time formatting, culture-aware `PaginationShowingFormat`), most list pages have real empty states with calls to action, several consumer pages implement the full loading → error → retry chain (`Courses`, `Billing`, `CourseDetail`, `Profile`, `LessonViewer`, and now `AssessmentForm` all offer a localized error state), and destructive actions are gated by the shared `ConfirmModal` (now including Pricing), and action feedback is now localized end-to-end (UX-1, fixed — the ~100 hardcoded strings were swept into the resx pattern).

All nine Medium findings have been resolved: action feedback is now typed and repositioned (UX-5), load-failure states offer error+retry (UX-6), the authenticated home is a real dashboard with a public `/blog` index and a global footer (UX-7), role-denied messaging distinguishes needs-login from lacks-role (UX-8), admin editors guard unsaved work on navigation (UX-9), deletes converge on the shared `ConfirmModal` (UX-10), auth/profile validation is localized and aligned with the Identity policy (UX-11), dates are culture-aware with uniform `ToLocalTime()` (UX-12), and the two largest admin tables gained debounced search (UX-13). Only two Low items and one Info item remain (below).

## 3. Findings

> The nine Medium findings (UX-5 through UX-13) are resolved and have been removed from this list; see §2 for the summary and the git history (`Fix UX-5` … `Fix UX-13`) for the changes. The remaining open items are two Low and one Info.

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

All nine Medium items (UX-5 through UX-13) are resolved. The remaining backlog:

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
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
