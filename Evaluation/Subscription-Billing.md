## Pricing/Plans page (end-user)

Trigger: User opens Pricing page while not logged in
Correct Behavior: Either hide upgrade actions or route to login; preserve intended plan choice after login
False Behavior: Checkout starts without auth or user loses selected plan after login

Trigger: Pricing data (plans/prices/features) loads slowly
Correct Behavior: Loading state; disable Upgrade/Downgrade flows until prices loaded
False Behavior: Buttons clickable with missing price IDs; wrong plan purchased

Trigger: Pricing API fails (500/timeout)
Correct Behavior: Error + retry; no stale plan cards; no checkout attempt possible
False Behavior: Blank pricing cards or checkout tries with null product IDs

Trigger: Current plan banner is shown
Correct Behavior: Matches server subscription state; updates after any change
False Behavior: Banner says “Plus” while user is actually Free/Pro (stale state)

Trigger: Plan is current (e.g., Plus)
Correct Behavior: CTA becomes “Current Plan” (disabled) and other CTAs reflect valid transitions
False Behavior: Allows “Upgrade” to same plan or shows both “Upgrade” and “Current Plan”

Trigger: Downgrade path (e.g., Plus -> Free)
Correct Behavior: Clear effective date policy (immediate vs end-of-period); requires confirmation
False Behavior: Downgrade happens silently or policy unclear; features removed unexpectedly

Trigger: Pro is marked “Most Popular”
Correct Behavior: Badge is purely UI; does not affect checkout product selection
False Behavior: Badge influences wrong price ID selection

Trigger: i18n EN/EL toggle on plans page
Correct Behavior: All strings localize (features, CTAs, currency formatting); plan values remain unchanged
False Behavior: Mixed language, wrong decimal separators, or currency symbol inconsistencies

Trigger: Responsive/mobile layout
Correct Behavior: Cards stack; CTAs reachable; no overlap with banners/toasts
False Behavior: Cards clipped; CTAs off-screen; horizontal scrolling required

---

## Checkout start (Upgrade button)

Trigger: Click Upgrade to paid plan
Correct Behavior: Creates a single checkout session; CTA disabled in-flight; user sees progress
False Behavior: Multiple sessions created on double-click; multiple charges possible

Trigger: Network timeout after clicking Upgrade
Correct Behavior: User can safely retry; system deduplicates sessions or shows existing pending session
False Behavior: Retrying creates multiple active sessions leading to duplicate subscriptions

Trigger: User already has active subscription and clicks upgrade/downgrade
Correct Behavior: Uses correct flow (checkout vs customer portal vs subscription update); prevents duplicate subscriptions
False Behavior: Creates a second subscription instead of modifying existing one

Trigger: User has incomplete payment / requires SCA (3DS)
Correct Behavior: User is prompted appropriately; final state reflected only after confirmation
False Behavior: UI claims success before payment completes

Trigger: User cancels checkout and returns
Correct Behavior: App shows “No changes made” and stays on current plan; no stale “pending” banner
False Behavior: App marks user as upgraded despite cancelation

Trigger: Checkout success redirect includes `session_id`
Correct Behavior: Success page verifies session with backend; does not trust client params
False Behavior: Anyone can spoof success by changing URL parameters

Trigger: Checkout success page refresh/back button
Correct Behavior: Idempotent confirmation; safe to refresh; no duplicate side effects
False Behavior: Refresh triggers repeated “activation” logic or duplicate transactions

Trigger: User opens success URL from different account/device
Correct Behavior: Backend verifies ownership; shows error/unauthorized
False Behavior: Leaks subscription details or applies subscription to wrong user

---

## Subscription success page (post-checkout)

Trigger: Success page loads while backend webhook hasn’t arrived yet
Correct Behavior: Shows “Processing” state; polls/refreshes; final plan updates when ready
False Behavior: Shows wrong plan/dates permanently or confuses user with stale state

Trigger: Display start/renewal dates
Correct Behavior: Uses user locale/timezone rules; consistent date formats; correct billing period
False Behavior: Off-by-one date, wrong timezone, or renewal date incorrect

Trigger: Success page CTAs (“Browse Courses”, “View Plans”)
Correct Behavior: Navigate correctly; reflect updated entitlements immediately
False Behavior: User still blocked from paid content after success page

---

## Billing page (end-user)

Trigger: Billing page loads with slow subscription/transactions API
Correct Behavior: Loading state; actions disabled until state known
False Behavior: Cancel/Change buttons available with unknown subscription ID

Trigger: Subscription info missing (user is Free)
Correct Behavior: Shows Free plan state; hides Cancel Subscription; offers Upgrade path
False Behavior: Shows Cancel button for free users or errors due to null subscription

Trigger: Current plan card shows quotas/features (courses/assessments)
Correct Behavior: Matches plan definition and current entitlements
False Behavior: Inconsistent feature list vs actual access control

Trigger: Transaction history is empty
Correct Behavior: Clean empty state; no broken table
False Behavior: Blank area or table headers only with errors

Trigger: Transaction history includes mixed event types (upgrade, free assignment, refund)
Correct Behavior: Clear labels; amounts formatted; negative amounts shown correctly
False Behavior: Refunds shown as positive charges or missing descriptions

Trigger: Transaction row has missing fields (amount/reference/date)
Correct Behavior: Safe placeholders (“—”); still renders; no crash
False Behavior: Runtime exception or broken columns

Trigger: Large transaction history
Correct Behavior: Pagination/infinite scroll; stable sorting (newest first)
False Behavior: Fetch-all causes lag or browser freeze

Trigger: Clicking a reference/session id (if clickable)
Correct Behavior: Either opens safe details view or copies value; no sensitive leakage
False Behavior: Exposes raw internal IDs in unsafe ways or links to unauthorized resources

Trigger: i18n on billing page
Correct Behavior: Dates, currency, labels, and status text localized consistently
False Behavior: Mixed locales; EUR formatting inconsistent (`€9.99` vs `9,99 €`)

---

## Cancel subscription

Trigger: Click “Cancel Subscription”
Correct Behavior: Confirmation modal explaining effective date and access until period end; cancel is reversible if supported
False Behavior: Immediate cancel with no confirmation or unclear consequences

Trigger: Cancel while payment is “past_due” / “incomplete”
Correct Behavior: Handles Stripe states explicitly; correct messaging; no crash
False Behavior: Cancel fails silently or leaves subscription in limbo

Trigger: Cancel succeeds but webhook update is delayed
Correct Behavior: UI shows “Cancellation scheduled” and updates status when confirmed
False Behavior: UI still shows Active indefinitely or flips back incorrectly

Trigger: Cancel clicked twice / rapid clicks
Correct Behavior: Idempotent; disables button in-flight
False Behavior: Multiple cancellation attempts causing errors/confusion

Trigger: Cancel fails (500/timeout/Stripe error)
Correct Behavior: Show error; keep current state; allow retry
False Behavior: UI shows canceled but backend remains active

---

## Change plan from Billing (“Change Plan”)

Trigger: Change Plan navigates back to Pricing or opens customer portal
Correct Behavior: Single clear flow; retains current plan context; prevents duplicate subscriptions
False Behavior: Creates new subscription instead of modifying existing

Trigger: Upgrade mid-cycle (proration policy)
Correct Behavior: If prorating, user is informed; invoice/charge behavior matches policy
False Behavior: Surprise charges with no disclosure or incorrect proration amounts

Trigger: Downgrade mid-cycle
Correct Behavior: Applies at period end (common) or immediate per policy; clearly communicated
False Behavior: Downgrade removes access immediately without warning (or never applies)

---

## Entitlements + access control consistency

Trigger: Subscription state changes (upgrade/downgrade/cancel)
Correct Behavior: Access control updates across the app (courses, assessments, certificates) within expected window
False Behavior: Billing says Pro but app blocks Pro features (or vice versa)

Trigger: User hits feature limit (e.g., Free up to 1 course)
Correct Behavior: UI blocks with localized message + CTA to upgrade; server enforces too
False Behavior: Client blocks but API allows, or client allows but API rejects unpredictably

---

## Security + privacy

Trigger: Pricing/billing endpoints called by unauthorized user
Correct Behavior: 401/403; no billing data rendered
False Behavior: Billing details accessible without auth

Trigger: User manipulates client-side plan IDs/price IDs
Correct Behavior: Backend validates allowed price IDs; refuses tampered requests
False Behavior: User can buy wrong product or cheaper price tier

Trigger: Success page URL parameters tampered (`session_id` changed)
Correct Behavior: Backend verifies session belongs to authenticated user; otherwise error
False Behavior: Subscription activated/viewable by guessing IDs

Trigger: Webhook spoofing / duplicate webhook deliveries
Correct Behavior: Webhook signature verified; idempotent processing; no double transactions
False Behavior: Duplicate charges/transactions or incorrect plan state

---

## UX/accessibility

Trigger: Keyboard-only navigation (plans page, modals, billing table)
Correct Behavior: Focus order correct; modals trap focus; CTA buttons accessible
False Behavior: Focus lost or actions unreachable

Trigger: Slow network / offline during critical actions
Correct Behavior: Clear error states; retry; no ambiguous “did I pay?” experience
False Behavior: Silent failure or misleading success messaging
