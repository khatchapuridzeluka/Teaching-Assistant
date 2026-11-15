# Teaching Assistant — UI description (prompt for Lovable)

## What the app is

A desktop web app for one teacher who grades programming homework (Advanced Java) from Google Classroom.
An AI agent drafts grades in the background; the teacher reviews, edits and approves. The UI is the teacher's
workspace: calm, dense, professional — closer to Linear / GitHub PR review than to a colorful dashboard.

Important constraints:
- **Thin client.** The frontend only displays and edits what the API returns. No grading or business logic in the UI.
- **Read-only towards Google.** Nothing is ever written back to Classroom; the UI should say "read-only" where relevant.
- **Human in the loop.** Everything from the AI is a *draft*; the teacher decides.
- All UI text and AI feedback is in **English**.
- Desktop first (a laptop next to an open agent panel); no mobile layout needed.

Stack: React 19 + Vite + TypeScript, Tailwind CSS 4, TanStack Query, react-router, Monaco editor
(`@monaco-editor/react`) for code, `docx-preview` for Word files, `react-markdown` for agent replies.

## Visual style

Three fixed colors plus tints of them. **No gradients, no shadows-heavy cards, no extra status colors** — status is
shown by fill vs. outline vs. dashed border, not by red/yellow/green (the one exception: mistake severity in the code
viewer).

| Token | Hex | Use |
|---|---|---|
| ink | `#1f2326` | text, sidebar background, dark buttons, user chat bubbles |
| ink-muted | `#6b6e70` | secondary text, table headers |
| ink-soft | `#3a3f43` | sidebar hover / dividers |
| paper | `#f3f1ec` | page background (warm off-white) |
| paper-dark | `#edebe7` | row dividers, muted chips |
| line | `#dedcd7` | card borders, table borders |
| accent | `#2f5e4e` | deep green: primary buttons, active nav, links, "connected" dots |
| accent-tint | `#e4ece8` | light green chip backgrounds, avatars |

Typography: **Inter Variable** (fallback Segoe UI, system-ui), base size **13px**, line-height normal, antialiased.
Monospace: Consolas. Page titles 22px bold, tight tracking. Card titles 14px bold. Small uppercase labels: 10–11px,
letter-spacing 0.06–0.08em, muted or accent color.

Components:
- **Card:** white, 1px `line` border, radius 10px, no shadow. Optional header row: title + muted subtitle on the left,
  actions on the right, bottom border, padding ~18px × 13px.
- **Buttons:** height 34px (small: 28px, text 12px), radius 8px, semibold, 1px border. Variants:
  primary (accent fill, white text), dark (ink fill, white text), outline (white, `line` border, ink text).
- **Chips (status pills):** height 22px, fully rounded, 11px semibold. Tones: outline (ink border), tint (accent-tint
  bg, accent text), accent (accent fill, white), ink (ink fill, white), muted (paper-dark bg, muted text),
  dashed (dashed muted border) — used for "Late", "Soon", "Draft", "Planned".
- **Tables:** full width, uppercase 11px muted headers, rows separated by `paper-dark` lines, hover row `paper/60`,
  numbers tabular.
- **Stat tiles:** a row of 4 tiles separated by 1px `line` gaps inside a card; label (muted 12px), big value
  (28px bold), small accent note underneath.
- **Spinner:** small ring, `line` color with accent top. **Error panel:** card with a thick ink left border, bold title,
  muted message, and a link to Settings when Google is not connected.
- **Avatar:** 30px circle, accent-tint bg, accent initials.

## Layout (app shell)

Full-height, three columns, nothing scrolls except the main area:

1. **Sidebar** (left, 232px, ink background, light text):
   - Logo: green rounded square "TA" + "Teaching Assistant" / "Grading agent".
   - "WORKSPACE" label, then **Courses** (link to all courses, with a ▾/▸ toggle that folds the course list).
   - Nested course list (thin left border). The current course expands to its sections:
     Overview, Assignments (working), Students, Gradebook, Common mistakes, Rubric & policy
     (planned — marked with a small outlined "SOON" pill).
   - Workspace sections: Review queue, Evaluation (both "SOON"), Settings.
   - Bottom: **Google Classroom card** with a status dot (filled green = connected): "Connected · read-only" /
     "Not connected · click to connect" / "Access expired · click to reconnect".
   - Active item: accent fill, white, semibold. Hover: ink-soft.
2. **Main column:** white **top bar** (64px, bottom border) with breadcrumbs on the left
   (Courses / Java II / Assignments / Homework 2 / Instructions — last one bold) and on the right an
   "API online" status dot plus an **"Ask agent"** button (dark; becomes outline "Hide agent" when open).
   Below it the page content on `paper` background, padding 28px × 24px, **full width** (no max-width), sections
   stacked with 18px gaps.
3. **Agent panel** (right, 400px, white, left border) — toggled from the top bar. It **narrows the page instead of
   covering it**, and stays mounted when hidden so the conversation survives.

## Screens

### Courses (`/`)
Page header "Courses" — "Courses you teach in Google Classroom (read-only)." Grid of course cards (1/2/3 columns):
course name (15px bold), section in muted text, state chip (active = tint, archived = muted), "Open course →" accent
link and a small "Classroom ↗" external link. Card border turns accent on hover.

### Course overview (`/courses/:id`)
Header with course name, "section · active in Google Classroom", "Open in Classroom ↗" button. Stat tile row:
Assignments count, Next deadline (date + assignment title), Drafts to review ("soon", muted), Students ("soon",
muted). A "Recent assignments" card (4 rows: title, "Due … · N points", link "All assignments →"). Then an
"IN THIS COURSE" label and a grid of section cards (label, Ready/Soon chip, one-sentence description).

### Assignments (`/courses/:id/assignments`)
Table: Assignment (bold link), Due, Points, Task statement (one line, muted, truncated), small "Open" button.

### Assignment (`/courses/:id/assignments/:assignmentId`) — the main working screen
- **Header:** assignment title, subtitle (due date, points, counts), actions: "Open in Classroom ↗",
  "Re-extract code" (outline), **"Fetch from Classroom"** (primary; shows "Fetching…" while running).
- **Instructions card:** task statement text on the left; on the right a scrollable list (max ~148px) of the
  teacher's attached materials (docx, pdf, images, Drive videos, links) with type and a "View" button that opens the
  **Instructions modal**. Header shows "Instructions vN" and "Open instructions page →". A note appears when the
  instructions changed in Classroom since they were stored.
- **Result cards** after an action: "Fetch finished" (stat tiles: new submissions, new versions, unchanged, not
  turned in + a list of problems per student) and "Extraction finished".
- **Submissions table** (inside a card that adapts to its own width via container queries):
  - Filter tabs with counts: All / Returned / Turned in / Not submitted.
  - Sortable column headers (click flips direction).
  - Columns: Student (avatar initials, name, attachment names underneath), Status (chips: Returned = accent,
    Turned in = tint, Reclaimed = outline, Not submitted = muted; plus dashed "Late" chip), Turned in date (hidden
    when narrow; moves under status), Version picker (select: "v2 · Nov 6 (latest)"), Actions.
  - Default order: Returned, then Turned in, then by date (newest first); students with nothing turned in always at
    the bottom, separated by a thicker divider.
  - Row actions: **Preview** (outline), **Grade with AI** (primary), **AI review** (dark, when a review exists).
- **Preview panel** (full-screen overlay): header with student, version chip, Close (Escape closes). Sections:
  Code (code browser), Images (grid, click to enlarge on a dark overlay), PDFs in an iframe, Word documents rendered
  inline, Other files, Not stored (with reason).
- **AI review panel** (full-screen overlay, two columns):
  - Left (~44%): score, written review (markdown), **Mistakes list** — each with a severity dot, category,
    `File.java:28` location and message; clicking jumps to the line. Mistakes without a location show
    "Whole submission". Lists of files sent / not sent to the model. Header chips: dashed "Draft · kept until you
    refresh the page", "Regrade" button, Close.
  - Right: **code browser** — file list on the left with a mistake count badge per file (colored by worst severity),
    Monaco editor (read-only, light theme) on the right.
  - Mistake marks in the editor: a colored dot in the gutter and a faintly tinted line; hovering shows the message;
    marks also appear in the scrollbar. Severity colors (the only non-palette colors in the app):
    Major `#dc2626`, Minor `#d97706`, Style `#64748b`, line tint at 8% opacity.

### Instructions modal and page (`…/instructions`)
Modal: dark backdrop, ~88vh, tabs for each material, the selected one previewed inline (PDF iframe, Word rendered,
image, Drive video player, otherwise open/download fallback), footer with name/size/download, buttons
"Open as page" and Close (Escape closes). The page version shows the full statement plus the same viewer at nearly
full height.

### Settings (`/settings`, titled "Connections")
Two cards side by side:
- **Google Classroom:** "Read-only access: the app can never publish grades.", state chip, list of granted scopes in
  plain words each with a "read-only" tint chip, Connect / Reconnect (primary) or Disconnect (outline, confirms).
- **Grading model:** "Sends one short test prompt", chip "Healthy · 840 ms" / "Unavailable", model name and reply,
  "Test connection" button.

### Planned sections ("Coming soon" page)
Page header with the section name, its one-sentence description, dashed "Planned" chip, and a short bullet list of
what it will do (e.g. Students: score trend, mistakes labelled new/repeated/fixed, agent-written overview).

### Agent chat panel
Header with "New chat" and "Close" small buttons. It knows which page the teacher is on (course/assignment name only,
never student data). User messages: right-aligned ink bubbles with white text (rounded, small bottom-right corner).
Agent replies: left-aligned compact markdown (tight spacing, code blocks on ink background, tables scroll
horizontally). Input at the bottom: Enter sends, Shift+Enter new line; "Send" (primary) or "Stop" while streaming.

## Tone of the UI copy
Short, plain, factual sentences that say what happens: "Fetch finished", "Turned in without attachments.",
"Read-only access: the app can never publish grades.", "Draft · kept until you refresh the page".
No exclamation marks, no emoji, no marketing language.
