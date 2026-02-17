## Content loading + editor initialization

Trigger: Open editor with course that has empty description/content
Correct Behavior: Editor loads with a clean empty state (placeholder), no phantom markup
False Behavior: “undefined/null” text, broken toolbar, or editor crashes

Trigger: Open editor with very large HTML (long course description / many blocks)
Correct Behavior: Loads within acceptable time; UI remains responsive; no input lag spikes
False Behavior: Browser freezes, keystrokes delayed, or content partially missing

Trigger: Editor library fails to initialize (bundle error, missing plugin, runtime exception)
Correct Behavior: Show fallback textarea or a clear error + retry; preserve user ability to save text
False Behavior: Blank editor area with no explanation; Save produces empty content

Trigger: Content contains unsupported nodes/features (tables, embeds, custom tags)
Correct Behavior: Safe degradation (strip/convert) with minimal loss; user warned if destructive
False Behavior: Hard crash or silent destructive data loss on load

Trigger: Course content includes non-UTF characters / mixed encodings
Correct Behavior: Correct rendering; round-trip safe (save then reload equals original)
False Behavior: Mojibake/garbled text or characters replaced on save

---

## Editing semantics (WYSIWYG correctness)

Trigger: Apply bold/italic/underline to selection
Correct Behavior: Only selection changes; toggling off restores previous state
False Behavior: Formatting bleeds outside selection or cannot be removed

Trigger: Change block type (paragraph -> H1/H2/list/quote)
Correct Behavior: Block transforms cleanly; caret stays predictable
False Behavior: Nested invalid blocks, duplicated text, or cursor jumps unexpectedly

Trigger: Create and edit ordered/unordered lists
Correct Behavior: Enter creates new list item; Tab/Shift+Tab indents/outdents reliably
False Behavior: Lists break into random paragraphs; indentation corrupts structure

Trigger: Undo/Redo after complex actions (paste, formatting, block changes)
Correct Behavior: Deterministic history; no lost content; caret restored sensibly
False Behavior: Undo deletes unrelated content or redo reintroduces stale states

Trigger: Copy/cut/paste within editor
Correct Behavior: Preserves structure/formatting as expected; no duplication
False Behavior: Paste duplicates blocks, drops text, or inserts invisible garbage nodes

Trigger: Keyboard shortcuts (Ctrl+B/I/U, Ctrl+Z/Y, Ctrl+K)
Correct Behavior: Work consistently across platforms; do not conflict with browser navigation
False Behavior: Shortcuts inconsistent or trigger unintended page actions

---

## Pasting + sanitization (high risk)

Trigger: Paste rich content from Word/Google Docs
Correct Behavior: Converts to clean HTML; strips unsafe styles/scripts; keeps lists/headings reasonably
False Behavior: Injects huge inline styles, broken markup, or loses most formatting

Trigger: Paste content containing images/data URLs
Correct Behavior: Enforce policy (upload workflow or block); clear message to user
False Behavior: Silent failure, broken `<img>` placeholders, or oversized payload saved

Trigger: Paste HTML with scripts/onclick attributes
Correct Behavior: Sanitizer strips dangerous attributes/tags; saved output is safe
False Behavior: XSS possible when viewing course later

Trigger: Paste malformed HTML
Correct Behavior: Normalized and repaired; editor remains stable
False Behavior: Editor DOM corruption, crashes, or invisible content

---

## Links + embeds

Trigger: Insert/edit/remove hyperlink
Correct Behavior: Validates URL scheme (http/https/mailto as allowed); edits don’t break surrounding text
False Behavior: Broken anchors, nested links, or links that can’t be removed

Trigger: Insert link with javascript: or data: scheme
Correct Behavior: Blocked or sanitized to safe output; clear user feedback
False Behavior: Stored XSS vector

Trigger: Embed external media (YouTube/iframe) when not allowed
Correct Behavior: Block or convert to safe placeholder; explicit messaging
False Behavior: Arbitrary iframe stored and executed later

---

## Images + file assets (if supported)

Trigger: Insert image via upload
Correct Behavior: Upload progress; final URL inserted; error handling; size constraints enforced
False Behavior: Broken image URLs, no progress feedback, or silent failure

Trigger: Upload very large image / unsupported format
Correct Behavior: Rejected with clear reason; no editor corruption
False Behavior: Upload loops forever or editor becomes unresponsive

Trigger: Replace/delete image
Correct Behavior: Updates content correctly; no orphaned references (if you track assets)
False Behavior: Old asset remains referenced or deletion removes wrong node

---

## Autosave + explicit save

Trigger: User types for a while then navigates away
Correct Behavior: Autosave (if enabled) or dirty-state warning; no silent data loss
False Behavior: User loses work with no warning

Trigger: Save while network is slow
Correct Behavior: Save shows in-progress state; prevents double-save; final confirmation on success
False Behavior: User can spam Save creating race conditions; ambiguous final state

Trigger: Save fails (500/timeout)
Correct Behavior: Content remains in editor; error shown; retry possible; optional local draft retained
False Behavior: Editor clears or claims success incorrectly

Trigger: Two tabs editing same course
Correct Behavior: Conflict detection (ETag/version) or last-write warning; user can reconcile
False Behavior: Silent overwrite of newer content

Trigger: Save returns normalized HTML different from editor HTML
Correct Behavior: UI updates to server canonical version; re-open matches saved output
False Behavior: On reload, content changes unexpectedly (non-round-trip)

---

## Output format + persistence (round-trip integrity)

Trigger: Save then reload the page
Correct Behavior: Rendered content matches editor content (semantic equivalence)
False Behavior: Formatting shifts, spacing changes, or blocks reorder

Trigger: Switch between WYSIWYG and “source/HTML” mode (if available)
Correct Behavior: Consistent conversion both directions; invalid HTML handled safely
False Behavior: Conversion introduces duplicates or strips large sections

Trigger: Server stores Markdown but editor uses HTML (or vice versa)
Correct Behavior: Deterministic conversion with test fixtures; no drift over repeated edits
False Behavior: “Conversion erosion” (content degrades each edit/save cycle)

---

## Accessibility + caret correctness

Trigger: Screen reader and keyboard-only usage
Correct Behavior: Toolbar accessible; focus order sane; ARIA labels present
False Behavior: Toolbar unreachable or editor traps focus incorrectly

Trigger: Caret near formatted boundaries (end of bold text, start of list)
Correct Behavior: Typing continues in expected style; backspace behaves predictably
False Behavior: Cursor jumps, deletes wrong block, or “sticky formatting” persists incorrectly

---

## Performance + stability

Trigger: Rapid typing, large paste, heavy undo/redo
Correct Behavior: No noticeable jank; memory stable; no exponential slowdown
False Behavior: Increasing latency, memory leak, or tab crashes

Trigger: Multiple editors on same page (course + lesson editors)
Correct Behavior: Instances isolated; toolbar binds to correct editor
False Behavior: Toolbar controls wrong editor or shared state leaks between instances

---

## Security + rendering context

Trigger: Render saved content on learner-facing page
Correct Behavior: Same sanitizer assumptions; safe rendering; no script execution
False Behavior: Content is safe in editor but unsafe when rendered elsewhere

Trigger: Content contains inline styles/classes
Correct Behavior: Either allowed subset or stripped consistently; avoids layout-breaking CSS injection
False Behavior: User can break page layout or hide UI with injected CSS

---

## i18n + locale-specific edge cases

Trigger: Mixed-language text (Greek/English) with punctuation and quotes
Correct Behavior: Correct directionality and spacing; no character loss
False Behavior: Corrupted punctuation, broken quotes, or spacing collapse

Trigger: Spellcheck/IME input (Greek keyboard, mobile composition events)
Correct Behavior: Composition events handled correctly; no duplicated characters
False Behavior: Characters repeat, drop, or commit incorrectly during IME composition
