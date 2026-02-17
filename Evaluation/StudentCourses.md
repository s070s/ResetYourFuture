## Courses list (Available Courses)

Trigger: Courses API returns empty list
Correct Behavior: Show empty state (“No courses available yet”) + guidance/CTA
False Behavior: Blank page, broken layout, or crash

Trigger: Courses API is slow
Correct Behavior: Loading skeleton; course cards disabled until loaded
False Behavior: Flicker, partial cards, or clicking navigates with missing IDs

Trigger: Courses API fails (500/timeout)
Correct Behavior: Error banner + retry; no stale cards
False Behavior: Silent failure or infinite spinner

Trigger: Course card has missing fields (title/description/lessonCount)
Correct Behavior: Safe placeholders (“—”, 0 lessons); still clickable only if ID valid
False Behavior: Runtime error or card click goes to wrong/invalid route

Trigger: Very long title/description
Correct Behavior: Truncate/ellipsis; stable card height; no overlap
False Behavior: Layout overflow or cards become unusable

Trigger: Unicode/Greek content in course text
Correct Behavior: Correct rendering and line wrapping
False Behavior: Encoding artifacts or broken typography

Trigger: User has Free plan with course limit
Correct Behavior: UI indicates limit status; disables/enforces enroll beyond limit with upgrade CTA
False Behavior: UI allows enrolling but server blocks later, or UI blocks while server allows

Trigger: Sorting/filtering (if present or added later)
Correct Behavior: Deterministic order; stable results across refresh
False Behavior: Random reordering or inconsistent counts

---

## Course details page (before enrollment)

Trigger: Open course details for non-existent/unpublished course
Correct Behavior: 404 or “Course not available”; link back to courses
False Behavior: Blank content, leaked draft content, or crash

Trigger: Course details loads slowly
Correct Behavior: Loading state; disable “Enroll” until course data loaded
False Behavior: Enroll fires with null courseId or stale data

Trigger: Course has 0 modules/lessons
Correct Behavior: Show “No lessons yet” state; Enroll may be disabled (policy)
False Behavior: Enroll succeeds but course page is dead-end

Trigger: Course shows modules/lessons preview for non-enrolled users
Correct Behavior: Only shows allowed metadata (titles/durations); locked indicators where appropriate
False Behavior: Full lesson content accessible without enrollment

Trigger: “Enroll in this course” button state
Correct Behavior: Shows Enroll only if not enrolled; shows “Enrolled”/Continue if already enrolled
False Behavior: Enroll shown for enrolled user; double enrollment possible

Trigger: Clicking Enroll rapidly
Correct Behavior: Single enrollment; button disabled in-flight; idempotent endpoint
False Behavior: Multiple enrollments or inconsistent UI state

Trigger: Enroll fails (401/403/limit reached/500)
Correct Behavior: Clear error; if limit reached, show upgrade CTA; state unchanged
False Behavior: UI shows enrolled despite failure or generic error only

---

## Enrollment + access control consistency

Trigger: Enrolled user revisits course details
Correct Behavior: Shows progress/continue; lesson links enabled according to rules
False Behavior: Still shows locked state or requires re-enroll

Trigger: User upgrades plan mid-session (Free -> Plus) then enrolls
Correct Behavior: Entitlements refresh; enrollment allowed without requiring logout
False Behavior: UI still thinks Free and blocks until hard refresh

Trigger: User downgrades/cancels and loses entitlements
Correct Behavior: Access is restricted per policy; UI explains; progress preserved
False Behavior: Silent lockout or inconsistent access across pages

Trigger: Direct navigation to lesson URL without enrollment
Correct Behavior: 403/redirect to course details/pricing; no content leakage
False Behavior: Lesson content accessible by guessing URL

---

## Course lesson navigation (Next Lesson / Back to Course)

Trigger: “Next Lesson” from a lesson page
Correct Behavior: Navigates to the correct next lesson in sequence; disabled/hidden on last lesson
False Behavior: Skips lessons, loops incorrectly, or goes to wrong course

Trigger: “Back to Course”
Correct Behavior: Returns to the same course details with preserved scroll/context
False Behavior: Returns to wrong page or loses course context

Trigger: Breadcrumb links
Correct Behavior: Navigate correctly; reflect actual hierarchy (course -> module -> lesson)
False Behavior: Breadcrumb breaks or points to wrong resource

---

## Lesson content rendering (text/video/embed)

Trigger: Lesson type is Text/HTML content
Correct Behavior: Renders sanitized content; preserves formatting; no XSS
False Behavior: Broken markup, missing sections, or script execution

Trigger: Lesson type is Video (YouTube/Vimeo/hosted)
Correct Behavior: Embed loads; fallback shown if blocked; respects privacy/cookie policy
False Behavior: Blank iframe with no explanation or page performance collapse

Trigger: Video provider blocked (CSP, adblock, network)
Correct Behavior: Clear message + alternative link; does not break the rest of the page
False Behavior: Infinite loading spinner or unusable page

Trigger: Video autoplay restrictions
Correct Behavior: No unexpected autoplay with sound; user-initiated play works
False Behavior: Autoplay attempts repeatedly or audio starts unexpectedly

Trigger: Mixed locale UI around embedded player
Correct Behavior: App UI strings localize; external player chrome may differ but doesn’t break layout
False Behavior: App strings remain in wrong language after toggle

Trigger: Large media on slow connection
Correct Behavior: Page remains responsive; player loads progressively
False Behavior: Entire page janks/freezes while loading media

---

## Progress tracking (Mark as Complete / Completed state)

Trigger: Click “Mark as Complete”
Correct Behavior: Persists completion server-side; UI updates; idempotent on repeat
False Behavior: UI shows completed but refresh resets; or double increments progress

Trigger: User marks complete before video ends (if you require watch time)
Correct Behavior: Policy enforced (allowed or blocked); messaging clear
False Behavior: Completion rules inconsistent across lessons

Trigger: Offline/timeout when marking complete
Correct Behavior: Error shown; button re-enabled; no false “Completed” state
False Behavior: Shows completed despite failed persistence

Trigger: Progress bar (e.g., 1/1 100%)
Correct Behavior: Accurate across reloads/devices; updates immediately after completion
False Behavior: Wrong counts, stuck at 0%, or exceeds 100%

Trigger: Completing last lesson
Correct Behavior: Course completion state triggers; optional certificate eligibility updates
False Behavior: Course never completes or completes prematurely

Trigger: Concurrency (two tabs marking completion)
Correct Behavior: Final state consistent; no duplicate events/transactions
False Behavior: Progress oscillates or records duplicated completions

---

## Course completion + post-completion actions

Trigger: Course completed banner shown
Correct Behavior: Shows once; reflects true server completion; persists across sessions
False Behavior: Banner shows without completing or disappears incorrectly

Trigger: User revisits completed lessons
Correct Behavior: Shows Completed state; still allows review; progress unchanged
False Behavior: Forces re-completion or blocks access

Trigger: Certificate unlock (if applicable)
Correct Behavior: Only for eligible plans; visible CTA; server enforces
False Behavior: Free users can claim certificate or Pro users can’t access it

---

## Localization + formatting in courses/lessons

Trigger: Toggle EN/EL on courses list/course/lesson pages
Correct Behavior: All UI chrome localizes (buttons, labels, progress text); content localization follows your content strategy
False Behavior: Mixed language UI or lost user state (scroll, completion, form inputs)

Trigger: Dates/durations displayed (e.g., “10 min”, “5 minutes”)
Correct Behavior: Localized units and pluralization (“λεπτό/λεπτά” etc.)
False Behavior: English units in EL mode or incorrect plural forms

---

## Security + authorization

Trigger: User without subscription tries to access paid-only course
Correct Behavior: Redirect to pricing with explanation; no lesson content leaked
False Behavior: Course detail/lesson content accessible without entitlement

Trigger: User manipulates courseId/lessonId in URL
Correct Behavior: Server authorization checks; 403/404; safe errors
False Behavior: IDOR allowing access to other users’ enrollment/progress

Trigger: Lesson HTML includes injected scripts
Correct Behavior: Sanitized at creation + render; CSP supports defense-in-depth
False Behavior: Stored XSS in lesson content or course description

---

## Performance + resilience

Trigger: Course with many modules/lessons (200+)
Correct Behavior: UI remains responsive; virtualization/accordion; minimal DOM
False Behavior: Slow scroll, heavy reflows, or browser memory issues

Trigger: Rapid navigation between lessons
Correct Behavior: Cancels stale requests; last navigation wins; no content flash from previous lesson
False Behavior: Old lesson content appears briefly or wrong completion state shown

Trigger: Caching of course/lesson payloads
Correct Behavior: Cache invalidation works; updated content appears after publish/update
False Behavior: Users see stale lessons after updates

---

## Accessibility + UX

Trigger: Keyboard-only learning flow
Correct Behavior: Focusable buttons/links; visible focus; player controls reachable; skip links if needed
False Behavior: Inaccessible controls or focus lost after navigation

Trigger: Small viewport/mobile
Correct Behavior: Video scales; buttons remain reachable; no horizontal scrolling
False Behavior: Player overflows or key controls are off-screen
