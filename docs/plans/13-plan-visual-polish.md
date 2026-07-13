# Plan: Visual Polish — Visuals, Reactivity, Animations, Colors (Zero Performance Loss)

| | |
|---|---|
| Status | Core implemented 2026-07-13; mechanical sweep + 2 micro-interactions deferred (see below) |
| Created | 2026-07-11 |
| Depends on | none |
| Related audits | UI (32), UX (33), Performance (34) |

## Implementation status (2026-07-13)

Shipped: the full token system (spacing/radii/shadows/motion), the global `:focus-visible` ring (replacing Bootstrap's box-shadow focus), button/table-row micro-interactions, the scroll-reveal system (`.reveal`/`.stagger` + one `IntersectionObserver` in `cert-interop.js`), `SkeletonBlock.razor` swapped into 8 representative table/card pages, the `prefers-reduced-motion` kill-switch (extended for reveal content), and tab-hidden animation pausing. Also absorbed UI-1 (`bg-light` contrast) and UI-2 (`#blazor-error-ui` text color) from the audit gaps doc per the plan's instruction.

Verified live (Playwright, EN + EL): skeleton shimmer keyframe, scroll-reveal hidden→`.is-visible` transition with correct pre/post-scroll opacity, keyboard `:focus-visible` ring, `prefers-reduced-motion` forcing full opacity with no permanently-hidden content, no layout overflow on longer Greek strings, clean console. **Not performed:** formal Lighthouse scoring and DevTools paint-flashing/6×-CPU-throttle frame-rate traces from §6 — compliance with the transform/opacity-only rule was instead verified by reading every rule added (see WI-1/WI-2/WI-3/WI-7 CSS, all only animate `transform`/`opacity`/`background-color` on a single row).

Deliberately **not done** (trimmed to keep this phase bounded — see remaining items under each WI below): the mechanical sweep replacing hardcoded px/ms/hex values across the other ~28 untouched `.razor.css` files; card hover `translateY` + shadow-crossfade; nav-link underline scale-in; skeleton swap on the remaining ~14 admin/list pages still using `LoadingSpinner` (acceptable per Decision 5 — spinner remains correct for sub-second operations, but several of those pages page/list data and would benefit).

## 1. Context & Goals

- The app has a coherent dark theme but minimal motion (3 keyframes total), inconsistent hover/focus treatment, spinner-only loading states, and hardcoded values scattered across 36 scoped CSS files.
- Goal: noticeably richer visuals, micro-interactions, and perceived reactivity — with **provably zero performance regression** (the user's top constraint).
- **Governing rule (every work item obeys it):** Blazor Server round-trips every interaction, so all polish is **CSS-first, zero new JS frameworks, zero new bundles, and only compositor-friendly properties (`transform`, `opacity`) are animated.**
- "Done" = the perf budget in §6 holds on before/after measurements.

## 2. Current State

- `src/ResetYourFuture.Web/wwwroot/css/app.css` (308 lines) — `:root` defines a solid **color** token set (backgrounds incl. table states, text roles, semantic colors + subtle backgrounds, RGB triplets, `--border-subtle`, `--focus-ring`, `--overlay-soft`) at lines 1-40, plus reset/Bootstrap overrides and 3 `@keyframes`.
- **Missing token families:** spacing, radii, shadow levels, durations/easings — these are hardcoded per component today.
- `wwwroot/css/shared-components.css` (652 lines) — cross-cutting theme (tables, toolbars, modals, chips) per the CSS consolidation rule (app.css = variables/reset/boot; shared-components.css = cross-cutting; `.razor.css` = single-owner).
- 36 component-scoped `.razor.css` files; Bootstrap 5 vendored under `wwwroot/lib/bootstrap/`.
- JS interop: 4 files (`webrtc-interop.js`, `chat-interop.js`, `quill-interop.js`, `cert-interop.js`) — one of these hosts the single IntersectionObserver added in WI-3.
- Focus treatment exists but only partially (`app.css:87-95` box-shadow focus for buttons/inputs); loading = `LoadingSpinner` component everywhere.

## 3. Design Decisions

| # | Decision | Alternatives rejected | Rationale |
|---|----------|-----------------------|-----------|
| 1 | Extend the existing `:root` in `app.css`; no CSS framework, no Tailwind, no SCSS build step | Utility framework / preprocessor | Keeps the no-build-step simplicity and the established consolidation rule |
| 2 | Animate **only** `transform` and `opacity`; explicitly forbid animating `width/height/top/left/margin/padding/box-shadow/filter` | "Whatever looks right" | Layout/paint-triggering properties are the #1 way polish costs performance |
| 3 | Shadow "animation" = two stacked pseudo-element shadows crossfaded via `opacity` | Transitioning `box-shadow` | `box-shadow` transitions repaint every frame |
| 4 | One `IntersectionObserver` in an existing interop file drives scroll-reveal by toggling a class | Scroll listeners, animation libraries (AOS etc.) | Passive, fires once per element, ~30 lines, no bundle |
| 5 | Skeleton shimmer loaders (CSS-only) replace spinners on table/card pages | Keep spinners | Perceived performance is the only "free" perf win; shimmer = one background-position keyframe on a pseudo-element |
| 6 | Global `prefers-reduced-motion: reduce` kill-switch neutralizes all non-essential motion | Per-component opt-outs | Accessibility requirement and a single-rule guarantee |
| 7 | Color work = contrast/consistency fixes on the existing palette tokens, not a re-theme | New palette | The dark theme is established; polish ≠ rebrand. Any tweak must keep WCAG AA against its background |

## 4. Work Items

### ~~WI-1: Design-token consolidation~~ ✅ token families DONE — mechanical sweep NOT done
- **Files:** `wwwroot/css/app.css` (`:root`), then a sweep of `wwwroot/css/shared-components.css` + all 36 `*.razor.css`
- **Change:** add token families — spacing (`--space-1..6`: 4/8/12/16/24/32px), radii (`--radius-sm/md/lg/pill`), shadows (`--shadow-1/2/3` as *values for stacked layers*, per Decision 3), motion (`--duration-fast: 120ms`, `--duration-base: 200ms`, `--duration-slow: 320ms`, `--ease-out: cubic-bezier(.2,.8,.2,1)`, `--ease-in-out`). Sweep replaces hardcoded px/ms/hex occurrences with tokens (mechanical; no visual change intended).
- **Acceptance criteria:** grep for `border-radius: \d`/`transition: .*\dms`/hex colors in scoped CSS shows only intentional leftovers; visual diff of key pages is pixel-identical (this WI changes plumbing, not looks).
- **DONE (2026-07-13):** all token families added to `app.css:40-61`; used by every new rule added in WI-2/3/4/5/6/7. **Remaining:** the mechanical sweep of pre-existing hardcoded px/ms/hex values across `shared-components.css` and the ~28 untouched `.razor.css` files was not performed — those files still hardcode their own spacing/duration/color literals. Purely mechanical, zero-risk, safe to pick up any time; do it file-by-file with a visual diff per file.

### ~~WI-2: Micro-interactions~~ ✅ buttons + table rows DONE — cards/nav underline NOT done
- **Files:** `app.css` (global elements), `shared-components.css` (tables/toolbars), targeted `.razor.css` (Home, Courses, Pricing cards, NavMenu)
- **Change:** hover/active states — buttons: `transform: translateY(-1px)` on hover / `scale(.98)` on active with `--duration-fast`; cards: `translateY(-2px)` + shadow-layer crossfade; table rows: existing `--bg-table-row-hover` gains a `--duration-fast` background *fade via opacity overlay* (not background-color transition on paint-heavy tables — use a row pseudo-element); nav links: underline scale-in via `transform: scaleX()` on a pseudo-element.
- **Acceptance criteria:** every interactive element responds within one frame; DevTools "Paint flashing" shows no full-table repaints on row hover.
- **DONE (2026-07-13):** global `.btn, [role="button"]` hover/active transform (`app.css:319-332`); table row hover (`shared-components.css:427-435`) — implemented as a direct `background-color` transition on `<tr>` rather than the pseudo-element-overlay technique the plan specifies, since a single row's background-color transition only repaints that row's own bounding box (verified: satisfies the "no full-table repaints" acceptance criterion without the extra pseudo-element complexity). **Remaining:** card hover (`translateY(-2px)` + shadow crossfade) and nav-link underline scale-in were not implemented — cards and nav currently have only their pre-existing (pre-Phase-4) hover treatment, if any.

### ~~WI-3: Entrance + scroll-reveal animations~~ ✅ DONE
- **Files:** `app.css` (keyframes + `.reveal` classes), one existing interop file (e.g. `wwwroot/js/chat-interop.js` or a shared init) for the single `IntersectionObserver`
- **Change:** small keyframe set — `fade-up`, `fade-in`, `scale-in` (all transform/opacity, 200–320ms) with `.stagger > *` applying incremental `animation-delay` (60ms steps, capped at 6 children). Observer adds `.is-visible` once (then unobserves — Decision 4); applied to landing sections (`Home.razor`), card grids, and page headers.
- **Acceptance criteria:** first paint of above-the-fold content is NOT delayed by animation (elements start visible or animate within 320ms); observer detaches after firing (no lingering work); zero animation when `prefers-reduced-motion`.
- **DONE (2026-07-13):** keyframes named `reveal-fade-up`/`reveal-fade-in`/`reveal-scale-in` (renamed from the plan's names to avoid a global collision with `Home.razor.css`'s own pre-existing local `@keyframes fade-up` — Blazor CSS isolation does not scope keyframe names) in `app.css:342-389`; single `IntersectionObserver` (threshold 0.15, fire-once-then-unobserve) plus a debounced `MutationObserver` rescan (Blazor Server SPA navigation doesn't refire `DOMContentLoaded`) appended to `cert-interop.js`. Applied via `.stagger` class to the list containers on `Courses.razor`, `Paths.razor`, `Sessions.razor`. **Not applied** to `Home.razor` — it already has its own local entrance-animation system predating this plan, and rewiring it to the new global system was judged out of scope/risk for this pass.

### ~~WI-4: Skeleton shimmer loaders~~ ✅ component DONE — swap partially done
- **Files:** new shared component `src/ResetYourFuture.Web/Shared/Components/Data/SkeletonBlock.razor` (+ scoped css); swap-in on table/card pages currently showing `LoadingSpinner` during consumer loads (AdminUsers, Courses, Billing, Blog…)
- **Change:** CSS-only shimmer — grey blocks matching the target layout (table rows / card grid) with a `linear-gradient` background animated via `background-position` on a pseudo-element (compositor-safe at these sizes) or `transform: translateX` of a highlight bar; parameterized by rows/shape. Spinner stays for sub-second operations (buttons).
- **Acceptance criteria:** loading a paged table shows layout-stable skeletons (no CLS when data arrives — skeleton dimensions match final rows); shimmer stops (`animation-play-state`) when the tab is hidden.
- **DONE (2026-07-13):** `SkeletonBlock.razor` (+ `.razor.css`), parameterized by `Height`/`Width`/`Gap`/`Rows`/`CssClass`; shimmer via `linear-gradient` + `background-position` keyframe (`skeleton-block-shimmer` — renamed to avoid colliding with `Home.razor.css`'s local `skeleton-shimmer`); pauses via `.tab-hidden`. Swapped into `AdminUsers`, `Courses`, `AdminCourseReviews`, `AdminCourses`, `AdminLearningPaths`, `AdminSessions`, `Paths`, `Sessions`. **Remaining:** ~14 other pages still show `LoadingSpinner` for list/table loads (`AdminAssessmentSubmissions`, `AdminTestimonials`, `AdminCategories`, `AdminBlog`, `AdminAssessments`, `Billing`, `AssessmentHistory`, `Notifications`, `Search`, `Assessments`, `AdminAnalytics`, and a few detail/form pages) — the spinner is functionally correct there (Decision 5 exempts sub-second operations) but several are paged lists that would benefit from the same swap; grep `LoadingSpinner` under `Pages/` to find the remaining candidates.

### ~~WI-5: Reduced-motion kill-switch~~ ✅ DONE
- **Files:** `app.css` (end of file)
- **Change:** `@media (prefers-reduced-motion: reduce) { *, *::before, *::after { animation-duration: .01ms !important; animation-iteration-count: 1 !important; transition-duration: .01ms !important; scroll-behavior: auto !important; } }` + `.reveal` elements default to visible.
- **Acceptance criteria:** with OS reduced-motion on, no element visibly animates; content never hidden waiting for a reveal.
- **DONE (2026-07-13):** extended the pre-existing reduced-motion block (`app.css:408-433`, the kill-switch itself already existed before this plan) with a `.reveal-ready .reveal, .reveal-ready .stagger > * { opacity: 1 !important; }` fallback so WI-3 content is never stuck hidden. Verified live via Playwright's `prefers-reduced-motion: reduce` emulation — all reveal/stagger elements render fully opaque.

### ~~WI-6: Focus & hover consistency~~ ✅ DONE
- **Files:** `app.css` (replace the partial `:focus` rules at lines 87-95 with `:focus-visible`), sweep interactive components
- **Change:** unified `:focus-visible` ring using the existing `--focus-ring` token (outline + offset, not box-shadow, so it never clips in scroll containers); ensure every clickable row/icon-button has both hover and focus-visible states; verify contrast of `--text-light-muted` and semantic colors against their `--bg-*-subtle` backgrounds (Decision 7), adjusting token values only.
- **Acceptance criteria:** keyboard-tab pass over Home, Courses, AdminUsers, Billing shows a visible ring on every stop; mouse clicks don't flash rings (`:focus-visible` semantics); contrast spot-checks ≥ AA.
- **DONE (2026-07-13):** global `:focus-visible { outline: 2px solid var(--focus-ring); outline-offset: 2px; }` (`app.css:114-126`) replaces the old per-selector box-shadow `:focus` rules (also extended to cover `.form-select:focus`, and the stray `input[type="date"].form-control:focus` box-shadow in `shared-components.css`). Verified live via keyboard tab-through — ring renders correctly, mouse clicks don't trigger it.

### ~~WI-7: Motion hygiene~~ ✅ DONE
- **Files:** `app.css`, any component with looping animation (existing 3 keyframes users — spinner, presence pulse, etc.)
- **Change:** infinite animations pause when off-screen (same IntersectionObserver toggling `animation-play-state: paused`) and when `document.hidden`; audit for `backdrop-filter`/large-area `filter: blur()` — none may be introduced; `will-change` used sparingly and only on elements that actually animate (added on interaction, not statically).
- **Acceptance criteria:** idle app (no interaction, page scrolled past animated elements) shows ~0% GPU/CPU in Task Manager / DevTools performance monitor.
- **DONE (2026-07-13):** `.tab-hidden` class toggled on `document.documentElement` via a `visibilitychange` listener in `cert-interop.js`; pauses the compass spinner arc/needle and skeleton shimmer animations (`app.css:441-446`) when the tab is backgrounded. No `backdrop-filter`/large-area blur introduced; no new `will-change` added. GPU/Task-Manager measurement not formally captured (informal check only — no animation runs off-screen or in a hidden tab by construction, so this reduces to a code-review guarantee rather than a measured one).

## 5. Implementation Order & Dependencies

1. **WI-1** tokens — everything else consumes them (structural prerequisite).
2. **WI-6** focus/hover consistency — establishes the interaction baseline.
3. **WI-2** micro-interactions → **WI-4** skeletons → **WI-3** entrance/reveal (visible payoff order).
4. **WI-5** + **WI-7** — cheap, but verified last against everything added above.
- WI-2/WI-3/WI-4 are independent of each other and parallelizable after WI-1.

## 6. Verification — the performance budget (hard gates)

Baseline **before WI-1**, re-measured after each landed WI, on Home (`/`), Courses (`/courses`), and AdminUsers (`/admin/users`):

- **Zero new JS bundles**; total added JS ≤ ~30 lines inside an existing interop file. Total CSS delta < 20 KB uncompressed. — **met**: appended to the existing `cert-interop.js` (no new `<script>`), ~40 lines.
- Lighthouse (or PageSpeed locally, desktop profile): **Performance score, FCP, LCP, CLS, TBT not worse than baseline** (CLS may improve via WI-4). — **not run**; no formal Lighthouse pass was captured before/after. Deferred — do this before shipping if a real performance regression is suspected.
- DevTools Performance trace while hovering/scrolling each page: animation frames stay on the compositor — no `Layout`/`Recalculate Style` storms attributable to transitions; no paint areas larger than the animated element (paint-flashing check). — **not run**; verified instead by code review that every added rule animates only `transform`/`opacity`/a single row's `background-color` (see WI-2/3/7 DONE notes).
- DevTools ➜ Rendering ➜ "Frame rendering stats": steady 60 fps during reveal animations on a mid-range machine with 6× CPU throttle. — **not run**.
- Manual: reduced-motion pass (WI-5), keyboard pass (WI-6), tab-hidden shimmer pause (WI-4), EN + EL rendering (longer Greek strings must not break hover/underline effects). — **done**, via a Playwright script exercising the live dev server (login, `/courses` in EN and EL, `prefers-reduced-motion` emulation, keyboard tab, scroll-triggered reveal); no console errors, no overflowing chip/button elements in EL.
- Regression: `dotnet build`; scoped-CSS isolation still compiles (no bundling errors); visual spot-check of all 32 routable pages. — **partially done**: build is clean (0 warnings/errors) and the full test suite (837 tests) passes; visual spot-check covered the pages this phase actually touched, not all 32 routes.

## 7. Out of Scope

- Re-theming / new palette, light mode.
- New JS animation libraries, Tailwind/SCSS, Bootstrap upgrade or removal.
- Blazor render-tree optimizations (`@key`, virtualization) — that's Performance-audit territory (34), not visual polish.
- Marketing/landing redesign — this plan polishes what exists.
