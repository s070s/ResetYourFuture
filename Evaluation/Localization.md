## Language detection + initial state

Trigger: First visit with no stored preference
Correct Behavior: App selects a deterministic default (e.g., browser `Accept-Language` or EN) and shows it everywhere
False Behavior: Random language per page/refresh or mixed EN/EL on first render

Trigger: Browser language is EL but app default is EN (or vice versa)
Correct Behavior: Defined precedence is applied consistently (stored pref > user profile > browser > default)
False Behavior: Different pages use different precedence rules

Trigger: Stored preference exists (localStorage/cookie)
Correct Behavior: Language loads before first paint or with minimal flicker; persisted across reloads
False Behavior: Flash of wrong language (FOUC) on every navigation

Trigger: User is authenticated and has profile language
Correct Behavior: Profile language overrides browser default and syncs with UI toggle
False Behavior: Toggle shows EL but app text is EN (state mismatch)

---

## Toggle behavior (EN/EL switch)

Trigger: Toggle language while on a page
Correct Behavior: All visible strings update immediately (nav, buttons, headings, toasts, modals, validation errors)
False Behavior: Only part of the page changes; mixed language remains

Trigger: Toggle language during in-flight API requests
Correct Behavior: Request/response handling remains stable; UI re-renders without losing user input
False Behavior: Lost form state, duplicated requests, or stale text from old language

Trigger: Toggle repeatedly (EN->EL->EN quickly)
Correct Behavior: Last toggle wins; no jank; no stuck intermediate state
False Behavior: Race condition causing mismatched toggle + text

Trigger: Toggle on one tab while another tab is open
Correct Behavior: Either sync across tabs (storage event) or remain isolated by design, but consistent
False Behavior: One tab shows EL toggle state while rendering EN strings

Trigger: Toggle affects routing (if you use `/en/...` `/el/...`)
Correct Behavior: Correct locale route; canonical redirects; back button behaves predictably
False Behavior: Broken links, 404s, or infinite redirects between locales

---

## Coverage + missing translations

Trigger: A translation key is missing in EL (or EN)
Correct Behavior: Fallback to default language for that key; optional dev warning/telemetry
False Behavior: Blank text, “undefined”, or raw i18n key displayed (e.g., `home.hero.title`)

Trigger: A component has hardcoded strings
Correct Behavior: All user-facing text is sourced through i18n; hardcoded strings are detected in review/testing
False Behavior: “Stray English” appears in EL mode (or vice versa)

Trigger: Dynamic/templated strings (`Hello, {name}`)
Correct Behavior: Correct interpolation; grammar-safe ordering per language
False Behavior: Placeholders shown literally, wrong word order, or missing variable errors

Trigger: Pluralization (“1 lesson” vs “2 lessons”)
Correct Behavior: Correct plural rules per language; covers 0/1/2+ cases
False Behavior: “1 lessons”, incorrect plural forms, or only English plural logic

Trigger: Gender/grammatical cases (if applicable)
Correct Behavior: Uses ICU/message format or equivalent; avoids concatenating translated fragments
False Behavior: Awkward/incorrect Greek due to string concatenation

---

## Layout + typography resilience

Trigger: EL strings are longer/shorter than EN
Correct Behavior: Responsive layout handles expansion; no overlaps/clipping; buttons grow or wrap gracefully
False Behavior: Truncated critical CTA text, overlapping nav items, broken alignment

Trigger: Large font settings / browser zoom 125–200%
Correct Behavior: Layout remains usable; text wraps; controls remain clickable
False Behavior: UI elements become unreachable or text overflows containers

Trigger: Line breaks in hero headings
Correct Behavior: Controlled wrapping (CSS) maintains design; no orphan words; no clipped text
False Behavior: Random wrapping that breaks readability or hides key words

Trigger: Mixed-script content (EN words inside Greek sentence)
Correct Behavior: Proper spacing/punctuation; no weird kerning or broken ligatures
False Behavior: Punctuation attaches incorrectly or words run together

---

## Dates, numbers, currencies, and formats

Trigger: Display dates (createdAt, schedules, deadlines)
Correct Behavior: Localized format per locale (`el-GR` vs `en-US/en-GB`), consistent timezone strategy
False Behavior: EN date format shown in EL mode or inconsistent formats across pages

Trigger: Display numbers, percentages, decimals
Correct Behavior: Localized separators (`,` vs `.`) and percent placement per locale
False Behavior: “1,234.56” shown in Greek context where “1.234,56” is expected (or inconsistent)

Trigger: Currency display (€, pricing, invoices)
Correct Behavior: Correct currency symbol placement and formatting for locale
False Behavior: Currency appears with wrong separators or ambiguous formatting

Trigger: Sorting of names/titles
Correct Behavior: Locale-aware collation (Greek sorting in EL mode)
False Behavior: Sorting uses ASCII rules leading to incorrect order

---

## Validation errors, toasts, and system messages

Trigger: Form validation triggers (required, min length, invalid email)
Correct Behavior: Messages localized and consistent; field-level + summary localized
False Behavior: English validation text appears in EL, or raw server messages leak

Trigger: Toast/notification messages (e.g., “Course published”)
Correct Behavior: Localized, including past tense/grammar; no mixed language
False Behavior: Toast stays in previous language after toggle

Trigger: Confirmation modals (Delete/Disable/Reset Password)
Correct Behavior: Modal title/body/buttons localized; keyboard shortcuts unchanged
False Behavior: Modal partially localized or uses inconsistent terminology

Trigger: Server returns localized errors vs English-only errors
Correct Behavior: Client maps codes to localized messages; server strings are not trusted for i18n
False Behavior: Mixed-language error output depending on server response text

---

## API + content localization strategy

Trigger: Static UI strings are localized client-side; content is stored per language
Correct Behavior: Content fields support per-locale values (title_el/title_en) or translation layer; correct field shown based on locale
False Behavior: UI switches language but content remains fixed or mismatched

Trigger: Content is only stored once (single language)
Correct Behavior: App clearly treats it as non-localized content and only localizes UI chrome
False Behavior: Users expect content translation and get inconsistent partial translations

Trigger: Fetching localized content via `Accept-Language` header or `?lang=el`
Correct Behavior: Requests include locale; responses cached per locale; no cross-locale cache poisoning
False Behavior: Cached EN response reused in EL mode

---

## Persistence + hydration (SSR/SPA)

Trigger: SSR (if used) with locale-specific rendering
Correct Behavior: Server renders correct locale; client hydrates without mismatch warnings
False Behavior: Hydration mismatch, flicker, or content jumps after load

Trigger: SPA route navigation
Correct Behavior: Locale remains consistent across routes; no component reinitialization resets locale
False Behavior: Some routes revert to default language on navigation

Trigger: Hard refresh on a deep route
Correct Behavior: Locale still correct; route resolves; translations loaded
False Behavior: 404 due to locale routing or reset to default language

---

## Accessibility

Trigger: Screen reader reads language-specific content
Correct Behavior: `lang` attribute updates (`<html lang="en">` / `el`); SR pronunciation improves
False Behavior: `lang` stays fixed causing mispronunciation

Trigger: Keyboard navigation after language toggle
Correct Behavior: Focus preserved on the toggle or logical element; no focus loss
False Behavior: Focus resets to top or disappears

---

## Security + correctness

Trigger: Translation strings include HTML markup (if supported)
Correct Behavior: Render safely (sanitize/escape); only allow vetted markup
False Behavior: XSS via translations (especially if translations are remote-managed)

Trigger: User-provided content displayed inside localized templates
Correct Behavior: Proper escaping; no injection; formatting preserved
False Behavior: Injection through interpolated variables

---

## Observability + QA automation hooks

Trigger: Add new UI text without translation
Correct Behavior: CI/test fails (missing keys), or runtime logs a missing-key event
False Behavior: Missing translations ship unnoticed

Trigger: Screenshot/regression tests across locales
Correct Behavior: Baselines per locale; detects layout overflow and truncation
False Behavior: Only EN tested; EL breaks layout in production
