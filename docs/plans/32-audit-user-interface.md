# Audit: User Interface

| | |
|---|---|
| Finding prefix | UI |
| Created | 2026-07-11 |
| Scope | Component-level visual correctness, consistency, and accessibility across the 32 routable pages, 30 shared components, layout components, `wwwroot/css/app.css`, `wwwroot/css/shared-components.css`, and 36 scoped `.razor.css` files |
| Delegated | Flow-level UX (navigation, feedback, empty/error states, bilingual experience) → 33 (UX). Hardcoded/missing localized *strings* → 33 (UX); localization *infrastructure* → 39 (CFG). Render-tree/CSS performance → 34 (PERF). Motion, tokens, skeletons, focus-ring rollout already planned → [13-plan-visual-polish.md](13-plan-visual-polish.md) (referenced, not re-reported). Table sorting gaps → [10-plan-sorting-rollout.md](10-plan-sorting-rollout.md). |

## 1. Methodology

Static analysis only — the app was not launched. Examined:

- All 32 routable pages in `src/ResetYourFuture.Web/Pages/` (markup read in full for AdminUsers, AdminCourses, AdminBlog, AdminCategories, AdminAssessmentSubmissions, AdminCourseEdit (partial), Billing, Courses, Assessments, AssessmentForm, AssessmentHistory, CourseDetail, LessonViewer, MyCertificates, VerifyCertificate, Pricing, Home, Login, Register, ForgotPassword, Profile, Chat, Disabled, SubscriptionSuccess).
- Shared components: ScrollableTable, SortableColumnHeader, AdminPaginationToolbar, PaginationNav, ConfirmModal, FormModal, DismissibleAlert, StatusBadge, LoadingSpinner, PageStateContainer, CategoryFilterBar, UpgradePrompt, PresenceIndicator, MessagePane, AssistantWidget, IncomingCallToast.
- Layout: MainLayout, NavMenu (+ scoped CSS), AvatarDropdown, CultureSelector, ImpersonationBanner; `App.razor`, `Routes.razor`.
- CSS: `app.css` (309 lines), `shared-components.css` (653 lines), targeted reads/greps of scoped `.razor.css` files; repo-wide sweeps for `style="`, `data-label`, `aria-*`, `role=`, `<caption`, `.sort-arrow`, `tier-badge`.
- resx files were checked only to verify values that CSS/markup couples to (e.g. `ColActions`).

NOT examined: runtime rendering, real contrast measurements in a browser, JS interop behavior, Bootstrap vendored sources (except to confirm `bg-light` semantics) — static audit per scope. Visual-polish work items (tokens, `:focus-visible` rollout, micro-interactions, skeletons, reduced-motion) are planned in 13-plan-visual-polish.md and are not repeated as findings here.

## 2. Summary Scorecard

| Severity | Count |
|----------|-------|
| Critical | 0 |
| High | 0 |
| Medium | 7 |
| Low | 4 |
| Info | 1 |

> **Fixed since audit:** UI-3 (High — clickable course cards were `role="button"` with a second nested `role="button"` upgrade badge inside, violating the ARIA content model) — `Courses.razor` now uses the card-link pattern: the `<h3>` holds a single real `<a>` (to `/courses/{id}`, or `/pricing` for a locked course) whose `::after` stretches over the whole card, and the upgrade badge is a plain visual `<span>` sibling (the card already links to `/pricing`). No nested interactive elements remain; the card keeps a focus ring via `:has(a:focus-visible)`. `CourseDetail.razor`'s lesson rows (same whole-row `role="button"` shape but no nesting) got an explicit `aria-label` so the accessible name is just the lesson title. Verified live with Playwright: 10 cards, none `role="button"`/tabbable, the stretched link covers the card, the badge is non-interactive, and the link is keyboard-focusable.

> **Fixed since audit:** UI-1/UI-2 (both High — `bg-light` submission panel and unstyled `#blazor-error-ui` were both ~1:1-2:1 contrast) — fixed as part of `13-plan-visual-polish.md`'s WI-6 pass: `AdminAssessmentSubmissions.razor`'s answers panel dropped `bg-light`, and `#blazor-error-ui`/its `.reload`/`.dismiss` links now use `color: var(--bg-primary)` against the yellow background.

The UI layer is in better shape than most student projects: a coherent dark-theme token set in `app.css`, a documented CSS consolidation rule that is mostly followed, a genuinely good mobile card-table transformation with 100% `data-label` coverage on all 12 data tables, a skip link, `aria-sort` on sortable headers, `aria-pressed` on the culture selector, visually-hidden text for icon-only cells, `role="log"`/`aria-live` on the chat pane, and a global `prefers-reduced-motion` kill-switch already in place. The remaining problems are at the edges: interactive-element nesting inside clickable cards, keyboard operability applied on one page but forgotten on its admin twin, modals without a focus trap, and a set of consistency drifts (duplicated `.tier-badge`, unstyled sort-indicator classes, bespoke Billing table) that the repo's own conventions already prohibit.

## 3. Findings

### UI-4: Keyboard operability of collapsible headers is inconsistent — admin module headers are mouse-only  [Medium] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/Pages/CourseDetail.razor:78-83` does it right: `role="button" tabindex="0" aria-expanded @onclick @onkeydown(IsActivationKey)`. Its admin twin `src/ResetYourFuture.Web/Pages/AdminCourseEdit.razor:117-120` has `role="button"` and `aria-expanded` but **no `tabindex` and no `@onkeydown`**. The expandable answers toggle in `AdminAssessmentSubmissions.razor:37-40` is a real `<button>` (good) but lacks `aria-expanded` for the row it controls.
- **Impact:** Admin module/lesson management cannot be expanded from the keyboard at all; the announced "button" role is a lie for keyboard users. The `.collapsible-header:focus-visible` style in `shared-components.css:104-107` never fires because the element is unfocusable.
- **Recommendation:** Copy the CourseDetail attributes verbatim onto the AdminCourseEdit header (the `IsActivationKey()` extension is already shared), and add `aria-expanded` to the ViewAnswers/HideAnswers button.

### UI-5: Modals move focus in but have no focus trap and no focus restore  [Medium] [Effort: M]
- **Evidence:** `src/ResetYourFuture.Web/Shared/Components/Data/ConfirmModal.razor:59-78` and `FormModal.razor:46-64` — on open they `FocusAsync()` the dialog and handle Escape (good), but nothing constrains Tab within the dialog, and on close focus is dropped (not returned to the triggering button).
- **Impact:** Keyboard users can Tab out of an open modal into the inert, backdrop-covered page behind it (WCAG 2.4.3); after closing, focus resets to `<body>`, losing the user's place in long admin tables — every delete/rename/reset-password flow is affected because all 10+ modal usages go through these two components.
- **Recommendation:** Because both components are the single chokepoint, fix once: keep a captured `ElementReference` of the trigger (or use a small JS interop with `focus()`/`focusin` sentinel elements before/after the dialog) to cycle Tab and restore focus on close.

### UI-6: Billing renders a bespoke inline table + hand-built pagination toolbar instead of the shared components  [Medium] [Effort: M]
- **Evidence:** `src/ResetYourFuture.Web/Pages/Billing.razor:114-181` — a raw `<table class="transactions-table">` with inline `style="max-height:60vh; overflow-y:auto"` and `style="position:sticky; top:0; z-index:1"` duplicating exactly what `ScrollableTable.razor:6-8` renders, plus a copy-pasted `.pagination-toolbar-admin` block duplicating `AdminPaginationToolbar.razor:3-23` (the shared CSS even documents the exception: `shared-components.css:582-584` "used in AdminPaginationToolbar component and inline on Billing page").
- **Impact:** Every fix to the shared table/toolbar (touch targets, aria, sticky-header tweaks) silently misses Billing; it is also the page furthest from the admin-table conventions (no sortable headers, custom `<th>` set).
- **Recommendation:** Migrate to `ScrollableTable` + `AdminPaginationToolbar` — this is already specified as WI-9 of [10-plan-sorting-rollout.md](10-plan-sorting-rollout.md); do it there rather than as separate work.

### UI-7: `.tier-badge` styles duplicated (and already diverged) across two scoped CSS files  [Medium] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/Pages/Billing.razor.css:43-63` and `src/ResetYourFuture.Web/Pages/Courses.razor.css:57-74` both define `.tier-badge` + tier variants; Billing has a `.tier-free` variant that Courses lacks. The repo's own convention (header comment of `wwwroot/css/shared-components.css:1-12`) says cross-cutting styles belong in shared-components.css, and `.category-chip` (`shared-components.css:638-652`) — explicitly "Modeled on .tier-badge" — was already consolidated there.
- **Impact:** Same badge renders subtly differently per page and drifts further with every edit; a third consumer (e.g. Pricing) would need a third copy.
- **Recommendation:** Move `.tier-badge` and its tier variants next to `.category-chip` in shared-components.css and delete both scoped copies.

### UI-8: Sort-indicator classes referenced by SortableColumnHeader have no CSS anywhere  [Medium] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/Shared/Components/Data/SortableColumnHeader.razor:4,15,19` emits `sort-active`, `sort-arrow`, and `sort-arrow--inactive` classes; a repo-wide grep over all `.css` files (shared + scoped) finds zero rules for any of them.
- **Impact:** The inactive `⇅` hint renders at the same weight/color as the active `▲/▼`, and the active column gets no header highlight — sortable vs. sorted state is conveyed only by which arrow glyph is present, which is easy to miss and clearly not the intended design (the classes exist to be styled). This will get 7 more consumers when 10-plan-sorting-rollout lands.
- **Recommendation:** Add the three rules to `shared-components.css` (the component is cross-page): dim `--text-light-muted` for `.sort-arrow--inactive`, `--bg-table-header-active` background for `th.sort-active`.

### UI-9: Page-level loading indicator inconsistency — Chat uses a raw Bootstrap spinner with no label  [Medium] [Effort: S]
- **Evidence:** Every other page uses the themed `LoadingSpinner` component with a localized `Label` (e.g. `Courses.razor:10`, `Billing.razor:11`, `AdminUsers.razor:19`). `src/ResetYourFuture.Web/Pages/Chat.razor:7-12` renders `<div class="spinner-border" role="status">` with no accessible text and no compass theming; `MessagePane.razor:58-63` repeats this for message loads.
- **Impact:** Visual inconsistency on a flagship feature page, plus `role="status"` with an empty accessible name announces nothing to screen readers.
- **Recommendation:** Use `LoadingSpinner Label="@ChatRes…"` for the page-level state. (Small inline button spinners with adjacent text are fine and out of scope; skeleton replacements for table pages are WI-4 of 13-plan-visual-polish.md.)

### UI-10: Search inputs and filter chips lack programmatic state/labels  [Medium] [Effort: S]
- **Evidence:** Placeholder-only search fields: `AdminUsers.razor:10-13`, `AdminBlog.razor:10-14`, `Shared/Components/Data/CategoryFilterBar.razor:20-24` (no `aria-label`/`<label>`; placeholder disappears on input and is not a reliable accessible name). Category filter chips (`CategoryFilterBar.razor:6-18`) convey the selected filter only via the `selected` CSS class — no `aria-pressed`, even though the sibling `CultureSelector.razor:2-7` does this correctly.
- **Impact:** Screen-reader users get unnamed textboxes on the three search surfaces and cannot tell which category filter is active.
- **Recommendation:** Add `aria-label="@…SearchPlaceholder"` (or a visually-hidden label) to the three inputs and `aria-pressed="@(SelectedCategoryId == category.Id)"` to the chips, mirroring CultureSelector.

### UI-11: Static inline styles scattered through markup instead of scoped CSS  [Low] [Effort: S]
- **Evidence:** ~34 `style="` occurrences in `.razor` files. Legitimately dynamic ones aside (progress width `CourseDetail.razor:40`, hero background `Home.razor:19`, parameterized `ScrollableTable.razor:6`), the static offenders include: `AdminTestimonialEditor.razor:66` (avatar preview with hardcoded `border:2px solid #444` — a hex not in the token set), `BlogArticle.razor:5,15` (container padding), `VerifyCertificate.razor:5` (max-width), `ConversationSidebar.razor:32` (max-height/overflow), `SortableColumnHeader.razor:11` (cursor/user-select/white-space), `MessagePane.razor:7` (font-size), `AdminCourseEdit.razor:162-167` (five column widths).
- **Impact:** Bypasses the documented consolidation rule and the token system, so theme adjustments (13-plan-visual-polish WI-1 sweeps hardcoded values) miss these; `#444` is invisible against the dark card in practice.
- **Recommendation:** Fold each static style into the owning component's `.razor.css`; do it alongside the WI-1 token sweep of 13-plan-visual-polish.md to avoid touching the same lines twice.

### UI-12: Data tables have no caption or accessible name  [Low] [Effort: S]
- **Evidence:** `ScrollableTable.razor:6-18` renders a bare `<table>` with no `<caption>`, `aria-label`, or `aria-labelledby` parameter; no `<caption>` exists anywhere in the repo (grep). All 8 ScrollableTable consumers plus the inline tables (Billing, AdminAnalytics) are affected.
- **Impact:** Screen-reader table navigation announces an anonymous table; on pages with two tables (AdminAnalytics courses + assessments, `AdminAnalytics.razor:58-111`) they are indistinguishable in the rotor.
- **Recommendation:** Add an optional `AriaLabel`/`Caption` parameter to ScrollableTable (visually-hidden `<caption>`) and pass the page heading string at each call site.

### UI-13: Mobile card-table CSS couples to localized label text values  [Low] [Effort: S]
- **Evidence:** `wwwroot/css/shared-components.css:222-236` targets `td[data-label="Actions"]` and `td[data-label="Ενέργειες"]` to suppress the label and lay action buttons out horizontally. The values must stay in lockstep with `AdminRes.ColActions`, `CategoryRes.ColActions`, `AssessmentRes.Actions` (`AdminRes.resx:257-259`, `AdminRes.el.resx:276-278`, etc. — currently all aligned).
- **Impact:** Renaming the resx value (or adding a third language) silently degrades every admin table's mobile action row into a mislabeled stacked cell — a failure no compiler or test catches.
- **Recommendation:** Switch the hook to a locale-independent class (`<td class="actions-cell" data-label=…>`) and match `.table-responsive td.actions-cell` in the CSS; one mechanical sweep of the 10 action cells.

### UI-14: Bootstrap default palette leaks into the themed UI  [Low] [Effort: S]
- **Evidence:** `AssessmentHistory.razor:22` — `card-header bg-primary text-white` renders Bootstrap's stock blue header on the otherwise pink/purple (`--text-strong-accent`) theme; `AdminCourseEdit.razor:124` — `badge bg-light text-dark`; `ImpersonationBanner.razor:9` — `btn-light` (acceptable on the orange banner, but the only `btn-light` in the app).
- **Impact:** The one blue element in the app reads as foreign; light badges are near-white blobs on dark cards.
- **Recommendation:** Replace `bg-primary text-white` with the themed card-header (already styled globally at `shared-components.css:68-72`) plus a border accent; use `--bg-neutral-subtle` badges as elsewhere.

### UI-15: `role="menu"` dropdown without arrow-key or Escape handling  [Info] [Effort: S]
- **Evidence:** `src/ResetYourFuture.Web/Layout/AvatarDropdown.razor:5-38` — declares `aria-haspopup="menu"`/`role="menu"`/`role="menuitem"` and closes on focus-out, but implements no ArrowUp/ArrowDown/Escape/Home/End keyboard model that the `menu` role promises.
- **Impact:** Minor: only two items, Tab still works because they are real links/buttons; but the announced role sets expectations the widget doesn't meet.
- **Recommendation:** Either downgrade to the disclosure pattern (`aria-expanded` on the button, plain links — no `menu` roles) or add the key handling. The disclosure pattern is the cheaper, correct fix here.

## 4. Prioritized Action List

| ID | Severity | Effort | Action |
|----|----------|--------|--------|
| UI-4 | Medium | S | Add `tabindex="0"` + activation-key handler to AdminCourseEdit collapsible headers; `aria-expanded` on ViewAnswers |
| UI-7 | Medium | S | Consolidate `.tier-badge` into shared-components.css next to `.category-chip` |
| UI-8 | Medium | S | Style `.sort-active` / `.sort-arrow--inactive` in shared-components.css |
| UI-9 | Medium | S | Replace Chat's raw `spinner-border` with labeled `LoadingSpinner` |
| UI-10 | Medium | S | `aria-label` on the 3 search inputs; `aria-pressed` on category chips |
| UI-5 | Medium | M | Add focus trap + focus-restore to ConfirmModal/FormModal (single chokepoint) |
| UI-6 | Medium | M | Migrate Billing table/toolbar to shared components — execute as 10-plan WI-9 |
| UI-11 | Low | S | Fold static inline styles into scoped CSS (with 13-plan WI-1 sweep) |
| UI-12 | Low | S | Add caption/aria-label parameter to ScrollableTable and pass at call sites |
| UI-13 | Low | S | Replace `data-label="Actions"` CSS coupling with a locale-independent class |
| UI-14 | Low | S | Purge `bg-primary text-white` / `bg-light text-dark` leaks |
| UI-15 | Info | S | Downgrade AvatarDropdown to the disclosure pattern (or implement menu keys) |

## 5. Related Findings Elsewhere

- **UX-1 (33)** — the hardcoded English strings that ship inside these components (DismissibleAlert/ConfirmModal/PaginationNav/StatusBadge defaults, `blazor-error-ui` text, `ItemLabel` values) are quantified there; the banner's colors (former UI-2) are fixed.
- **UX-2 / UX-6 (33)** — silent failure and infinite-spinner *flows* behind the loading components discussed in UI-9.
- **UX-5 (33)** — where and how the `DismissibleAlert` messages appear (placement, severity styling, dismissibility) is flow-level and lives there.
- **UX-12 (33)** — inconsistent date/time formats rendered inside the tables audited here.
- **10-plan-sorting-rollout.md** — plain `<th>` headers on all non-AdminUsers tables (and the Billing bespoke table, UI-6) are handled by that plan; not re-reported as findings.
- **13-plan-visual-polish.md** — design tokens (spacing/radii/shadows/motion), `:focus-visible` unification, skeleton loaders, reduced-motion, and contrast spot-checks of existing tokens are that plan's WI-1..WI-7; findings here deliberately exclude them.
- **PERF (34)** — `ScrollableTable` renders all rows without virtualization (by design for paged lists); any render-cost concerns belong there.
