# Plan: Visual Polish — Visuals, Reactivity, Animations, Colors (Zero Performance Loss)

| | |
|---|---|
| Status | Draft |
| Created | 2026-07-11 |
| Depends on | none |
| Related audits | UI (32), UX (33), Performance (34) |

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

### WI-1: Design-token consolidation
- **Files:** `wwwroot/css/app.css` (`:root`), then a sweep of `wwwroot/css/shared-components.css` + all 36 `*.razor.css`
- **Change:** add token families — spacing (`--space-1..6`: 4/8/12/16/24/32px), radii (`--radius-sm/md/lg/pill`), shadows (`--shadow-1/2/3` as *values for stacked layers*, per Decision 3), motion (`--duration-fast: 120ms`, `--duration-base: 200ms`, `--duration-slow: 320ms`, `--ease-out: cubic-bezier(.2,.8,.2,1)`, `--ease-in-out`). Sweep replaces hardcoded px/ms/hex occurrences with tokens (mechanical; no visual change intended).
- **Acceptance criteria:** grep for `border-radius: \d`/`transition: .*\dms`/hex colors in scoped CSS shows only intentional leftovers; visual diff of key pages is pixel-identical (this WI changes plumbing, not looks).

### WI-2: Micro-interactions (buttons, cards, rows, nav)
- **Files:** `app.css` (global elements), `shared-components.css` (tables/toolbars), targeted `.razor.css` (Home, Courses, Pricing cards, NavMenu)
- **Change:** hover/active states — buttons: `transform: translateY(-1px)` on hover / `scale(.98)` on active with `--duration-fast`; cards: `translateY(-2px)` + shadow-layer crossfade; table rows: existing `--bg-table-row-hover` gains a `--duration-fast` background *fade via opacity overlay* (not background-color transition on paint-heavy tables — use a row pseudo-element); nav links: underline scale-in via `transform: scaleX()` on a pseudo-element.
- **Acceptance criteria:** every interactive element responds within one frame; DevTools "Paint flashing" shows no full-table repaints on row hover.

### WI-3: Entrance + scroll-reveal animations
- **Files:** `app.css` (keyframes + `.reveal` classes), one existing interop file (e.g. `wwwroot/js/chat-interop.js` or a shared init) for the single `IntersectionObserver`
- **Change:** small keyframe set — `fade-up`, `fade-in`, `scale-in` (all transform/opacity, 200–320ms) with `.stagger > *` applying incremental `animation-delay` (60ms steps, capped at 6 children). Observer adds `.is-visible` once (then unobserves — Decision 4); applied to landing sections (`Home.razor`), card grids, and page headers.
- **Acceptance criteria:** first paint of above-the-fold content is NOT delayed by animation (elements start visible or animate within 320ms); observer detaches after firing (no lingering work); zero animation when `prefers-reduced-motion`.

### WI-4: Skeleton shimmer loaders
- **Files:** new shared component `src/ResetYourFuture.Web/Shared/Components/Data/SkeletonBlock.razor` (+ scoped css); swap-in on table/card pages currently showing `LoadingSpinner` during consumer loads (AdminUsers, Courses, Billing, Blog…)
- **Change:** CSS-only shimmer — grey blocks matching the target layout (table rows / card grid) with a `linear-gradient` background animated via `background-position` on a pseudo-element (compositor-safe at these sizes) or `transform: translateX` of a highlight bar; parameterized by rows/shape. Spinner stays for sub-second operations (buttons).
- **Acceptance criteria:** loading a paged table shows layout-stable skeletons (no CLS when data arrives — skeleton dimensions match final rows); shimmer stops (`animation-play-state`) when the tab is hidden.

### WI-5: Reduced-motion kill-switch
- **Files:** `app.css` (end of file)
- **Change:** `@media (prefers-reduced-motion: reduce) { *, *::before, *::after { animation-duration: .01ms !important; animation-iteration-count: 1 !important; transition-duration: .01ms !important; scroll-behavior: auto !important; } }` + `.reveal` elements default to visible.
- **Acceptance criteria:** with OS reduced-motion on, no element visibly animates; content never hidden waiting for a reveal.

### WI-6: Focus & hover consistency (a11y + polish in one pass)
- **Files:** `app.css` (replace the partial `:focus` rules at lines 87-95 with `:focus-visible`), sweep interactive components
- **Change:** unified `:focus-visible` ring using the existing `--focus-ring` token (outline + offset, not box-shadow, so it never clips in scroll containers); ensure every clickable row/icon-button has both hover and focus-visible states; verify contrast of `--text-light-muted` and semantic colors against their `--bg-*-subtle` backgrounds (Decision 7), adjusting token values only.
- **Acceptance criteria:** keyboard-tab pass over Home, Courses, AdminUsers, Billing shows a visible ring on every stop; mouse clicks don't flash rings (`:focus-visible` semantics); contrast spot-checks ≥ AA.

### WI-7: Motion hygiene
- **Files:** `app.css`, any component with looping animation (existing 3 keyframes users — spinner, presence pulse, etc.)
- **Change:** infinite animations pause when off-screen (same IntersectionObserver toggling `animation-play-state: paused`) and when `document.hidden`; audit for `backdrop-filter`/large-area `filter: blur()` — none may be introduced; `will-change` used sparingly and only on elements that actually animate (added on interaction, not statically).
- **Acceptance criteria:** idle app (no interaction, page scrolled past animated elements) shows ~0% GPU/CPU in Task Manager / DevTools performance monitor.

## 5. Implementation Order & Dependencies

1. **WI-1** tokens — everything else consumes them (structural prerequisite).
2. **WI-6** focus/hover consistency — establishes the interaction baseline.
3. **WI-2** micro-interactions → **WI-4** skeletons → **WI-3** entrance/reveal (visible payoff order).
4. **WI-5** + **WI-7** — cheap, but verified last against everything added above.
- WI-2/WI-3/WI-4 are independent of each other and parallelizable after WI-1.

## 6. Verification — the performance budget (hard gates)

Baseline **before WI-1**, re-measured after each landed WI, on Home (`/`), Courses (`/courses`), and AdminUsers (`/admin/users`):

- **Zero new JS bundles**; total added JS ≤ ~30 lines inside an existing interop file. Total CSS delta < 20 KB uncompressed.
- Lighthouse (or PageSpeed locally, desktop profile): **Performance score, FCP, LCP, CLS, TBT not worse than baseline** (CLS may improve via WI-4).
- DevTools Performance trace while hovering/scrolling each page: animation frames stay on the compositor — no `Layout`/`Recalculate Style` storms attributable to transitions; no paint areas larger than the animated element (paint-flashing check).
- DevTools ➜ Rendering ➜ "Frame rendering stats": steady 60 fps during reveal animations on a mid-range machine with 6× CPU throttle.
- Manual: reduced-motion pass (WI-5), keyboard pass (WI-6), tab-hidden shimmer pause (WI-4), EN + EL rendering (longer Greek strings must not break hover/underline effects).
- Regression: `dotnet build`; scoped-CSS isolation still compiles (no bundling errors); visual spot-check of all 32 routable pages.

## 7. Out of Scope

- Re-theming / new palette, light mode.
- New JS animation libraries, Tailwind/SCSS, Bootstrap upgrade or removal.
- Blazor render-tree optimizations (`@key`, virtualization) — that's Performance-audit territory (34), not visual polish.
- Marketing/landing redesign — this plan polishes what exists.
