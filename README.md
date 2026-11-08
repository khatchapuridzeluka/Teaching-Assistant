# AI Teaching Assistant — Grading Agent

An AI teaching assistant that grades programming homework. It pulls student submissions from Google Classroom, builds and tests the code in isolated Docker sandboxes, drafts rubric-based scores and feedback with Claude, and leaves every final decision to the teacher.

Built for the **AI Agents Builder Program (HIGGS × KIU)**, October–November 2026.

> **Status:** in active development. Google Classroom integration is working; the grading pipeline and the review dashboard are in progress.

---

## Why

Grading a programming assignment for a class of 15–30 students means downloading every submission, reading and running the code, checking it against a rubric, and writing personal feedback for each student — then doing it again when students resubmit their fixes. It takes hours every week, and consistency suffers by the last submission.

This project automates the drafting, not the deciding:

- **The agent drafts** scores, mistakes and feedback.
- **The teacher reviews, edits and approves** every grade.
- **Grades never leave the tool** — the app has read-only access to Classroom and cannot publish anything.

## Features

| Area | What it does | Status |
|---|---|---|
| **Classroom ingestion** | Connects a teacher's Google account (read-only OAuth), lists courses, assignments and submissions, and fetches turned-in work into stored versions (snapshots); unchanged submissions are skipped. The same fetch stores the assignment's instructions (task statement and attached files) as versions too, downloading them again only when they change. Every fetch also checks that the latest stored version of each submission and of the instructions is really on disk: missing files are downloaded again in place under their recorded names (not as a new version) and missing extracted code is rebuilt, reported in the fetch summary | Listing ✅ · fetch & versions ✅ · instructions stored ✅ |
| **Submission formats** | Zip/rar/7z archives of `.java` files, loose files, documents, images, or GitHub repository links | Drive files stored ✅ · zip/rar/7z/tar extraction ✅ · GitHub cloning planned |
| **Sandboxed checks** | Compiles and runs student code in Docker (JDK 17–27), collecting compiler warnings, static analysis and test results before any AI judgment | Planned |
| **Rubric grading** | Claude grades against a layered rubric: base rubric → per-assignment extensions → the teacher's policy rules | First version ✅: "Grade with AI" sends one stored version (code, PDFs, images, including drawings inside the submitted archive; Word .docx as text plus the pictures embedded in it, such as code screenshots) with the assignment's stored instructions (saved by the fetch; no Classroom calls while grading) to Claude Opus 5.5 (`Anthropic:GradingModel`) as structured output: a Markdown review (score per task, feedback) plus a list of mistakes with fixed severity and category enums and the file and lines they are on (checked against the code that was sent). The code viewer marks each mistake with a coloured dot in the gutter and a tinted line, the message shows on hover, and clicking a mistake in the list jumps to its line; reads the code only. Every grading is **saved as a draft** the moment it arrives (review text, score / max / bonus as numbers, mistakes as rows, the raw answer, model and instructions version), so a refresh never loses a paid grading; "Grade again" adds a new draft and the newest one is current, earlier ones stay pickable, and "Discard" throws one away (kept in the history). The assignment list shows "Draft 88/100 · 9 mistakes", and "Drafts" opens every saved draft of the assignment. The course's **AI drafts** page (sidebar) opens on an overview, to choose first: **by assignment** (each with its drafts, students, average AI score and major mistakes) or **by student** (across assignments), or all drafts in one list. Choosing one opens only that choice's drafts ("← Overview" goes back): a student's drafts across their assignments (narrowable to one of them), or an assignment's drafts for its students; "all drafts" has an assignment (top) and a student (below) dropdown. Filters are applied by the API; the list shows score, mistake dots and version; the selected draft opens next to its code (review, mistakes that jump to their lines, Discard, "Open in assignment"). Filters and the open draft are kept in the URL; on phones the filters become two dropdowns. First policy rules are built into the prompt: trivial issues (printed-text wording, comments, unused imports or variables) are listed as gray Style suggestions and cost no points; a variable or method name that differs from one the instructions specify costs 3 points each · editing and approving drafts, base rubric and editable policy rules planned |
| **Background jobs** | Long work runs in a queue instead of inside the HTTP request | First version ✅ for grading: "Grade with AI" queues a job (`202 Accepted`) after quick checks; a background worker inside the API claims it (Postgres `FOR UPDATE SKIP LOCKED`), runs up to 3 at once, retries temporary failures (model overloaded, Google down) after 30 s, 1 min, 2 min…, recovers jobs whose worker stopped (10-minute lease with a heartbeat), and supports cancel. One active job per version (a double-click returns the same job). The page shows Queued… / Grading… and the draft appears when ready, even after leaving or refreshing the page. A green message bar at the top follows each grading you start on any page (queued → started → finished with an **Open draft** link, or failed / retrying); it polls `GET /jobs/{jobId}` every 2 s and survives a page refresh. The direct synchronous grade endpoint is kept. Fetch, sync and "grade all" are planned on the same queue |
| **Batch calibration** | Calibrates scores across the class and clusters common mistakes, so one explanation covers many students | Planned |
| **Resubmissions** | Detects new versions, grades the diff, reports fixed / remaining / new mistakes; tracks initial and final scores | Planned |
| **Teacher UI** | Sidebar with one Courses list; each course opens on an overview (assignment count, next deadline, recent assignments) with its sections: Assignments, Students, AI drafts, and planned Gradebook, Common mistakes, Rubric & policy, Participation, plus workspace-wide Review queue and Evaluation (planned pages describe what is coming). Each assignment is one workspace that fills the window: counts at the top (turned in, returned, AI drafts this session, not submitted), the instructions in one row (stored version, the task statement on click, materials as chips that open a modal previewing every material — documents rendered in the page, Drive videos in Drive's player — plus its own Instructions page and a note when Classroom changed since the stored version), then the students on the left (filters by status with counts, search, sort by status, date or name, students who turned nothing in always last, arrow keys to move) and the selected student on the right (version picker, files and what happened to each, the AI draft with its mistakes next to the code, tabs instead of columns when the agent panel leaves little room, "Expand" for the whole window); "Fetch from Classroom", preview of stored submissions (extracted code in a read-only Monaco viewer, image gallery, PDFs, exported Google Docs and Word .docx files inline, downloads), re-extraction, Google connection and model check. Responsive from phone to wide desktop: below 1024px the sidebar opens as a drawer, below 1280px the agent panel slides over the page instead of narrowing it, on phones the student list and the selected student take turns ("← All students" goes back), the code file list moves above the editor, and Word documents scale down to fit the screen | Initial version ✅ |
| **Agent chat** | "Ask agent" panel on the right of every page: replies from Claude rendered as Markdown, knows which course/assignment page is open (names only, no student data); remembers the conversation, which is kept in the browser tab (sessionStorage) and sent with each question | Chat ✅ · tools (fetch, read code, grade) planned |
| **Review dashboard** | Code viewer with inline mistake comments, score editing and approval | Planned |
| **Students and grades** | Roster, every student's state and grade per assignment, initial and final score, overall score per course | First version ✅: the **Students** page is a workspace like the assignment page: class stats at the top (students, class average, missing work, grades improved after a fix), the students on the left (filters All / Missing work / Improved / Left course, search, sort by name, overall or missing work, overall % with a bar) and the selected student's profile on the right (overall % and points, turned in / missing / late / improved tiles, a score-by-assignment chart with first-returned vs. final grade, the assignment list where each opens a modal with the full grade history and a link into the assignment workspace with that student selected, and "Coming next" cards for mistake history, the written overview and mistake categories). "Grade table" opens the whole class as one table (final grade, "70 → 95" when a fix raised it, "Missing · 0"). Works offline from the database; on phones the list and profile take turns. "Sync course" copies the roster (students and enrollments, students who left kept inactive), the assignments (maximum points, deadlines) and every submission's state and **Classroom grade history** in four requests sent at once, without files (`courseWorkId "-"` lists all assignments at once). Every assignment fetch also records state and grades for all students, including those who turned nothing in. Initial score = the first grade returned in Classroom, final score = the latest one (draft grades ignored); overall = final points over possible points, missing work (not handed in and past the deadline) counts as 0, work handed in but not graded yet is left out. Grades are only read from Classroom, never written. **Profile photos**: the sync stores one local copy per student (in the storage folder next to submissions, no history), downloaded only when Google's photo URL changed, the copy is missing or older than 30 days, and written only when the image itself (SHA-256) changed; `GET /students/{userId}/photo` serves it with the hash as ETag, and every avatar in the app shows it, falling back to initials. Needs the read-only `classroom.profile.photos` scope (reconnect once in Settings) |
| **Student profiles** | Mistake history per student, repeated vs. fixed issues, progress over time | Planned |

## Tech stack

| Layer | Technology |
|---|---|
| **Backend** | ASP.NET Core Web API on **.NET 10**, onion architecture |
| **AI** | **Anthropic Claude** via the official Anthropic C# SDK |
| **Database** | **PostgreSQL 18** (with pgvector) in Docker, **EF Core 10** with migrations |
| **Integrations** | **Google Classroom API** and **Google Drive API** (read-only scopes), Google OAuth 2.0 |
| **Sandbox** | **Docker** containers with multiple JDK images |
| **API docs** | **Swashbuckle** (Swagger UI) with XML documentation |
| **Logging** | **Serilog** (structured logging) |
| **Frontend** | **React 19** + Vite + TypeScript, Tailwind CSS, TanStack Query, React Router; API types generated from the Swagger spec (`openapi-typescript`) |
| **Tests** | **xUnit v3** on Microsoft Testing Platform |

## Project structure

```
TeachingAssistant.WebAPI.sln
global.json                              test runner configuration (Microsoft Testing Platform)
.editorconfig                            C# code-style rules (e.g. braces on every if/for), checked by the IDE and the build
Directory.Build.props                    settings for every project (code-style checks during dotnet build)
dotnet-tools.json                        local tools (dotnet-ef)
docker-compose.yml                       local Postgres
src/
  TeachingAssistant.Domain/
    Ingestion/                           submissions, versions (snapshots), content, change detection, course assignments
    Grading/                             grade history and the initial/final score rules, mistake enums
    Profiling/                           students, enrollments, overall score rule
    Jobs/                                background jobs (queued work such as grading and fetching) and their life cycle
  TeachingAssistant.Application/
    Contracts/                           what the API returns
    Interfaces/                          ports (ILanguageModel, ISubmissionSource)
    Interfaces/Services/                 use-case interfaces
    Services/                            use-case implementations
    Exceptions/                          application-level errors
  TeachingAssistant.Infrastructure/
    LanguageModel/                       Claude adapter
    Google/                              OAuth, Classroom adapter, Drive adapter (file metadata and download)
    Persistence/                         EF Core DbContext, entity mappings, migrations, submission/student/assignment stores
    Storage/                             submission files on disk (versions written atomically), code extraction
    Documents/                           reads Word .docx files (instructions, reports) for the model: text plus embedded pictures
  TeachingAssistant.WebAPI/
    Controllers/                         HTTP endpoints
    Extensions/                          service registration
    ExceptionHandling/                   exceptions → ProblemDetails responses
tests/
  TeachingAssistant.Tests/               xUnit tests for Domain and Application
frontend/                                React app (Vite)
  src/api/                               generated API types (schema.d.ts), client, one hook per endpoint
  src/components/                        layout (sidebar, top bar) and small UI pieces
  src/navigation.ts                      the course and workspace sections (sidebar, overview cards and routes read it)
  src/pages/                             Courses, Course overview, Assignments, Assignment (submissions + fetch), Instructions, planned sections, Settings
```

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/products/docker-desktop/) (for Postgres, and later the code sandboxes)
- An [Anthropic API key](https://console.anthropic.com/)
- A Google Cloud project with an OAuth client (for Classroom access — see below)

### 1. Clone and build

```bash
git clone <repository-url>
cd Teaching-assistant
dotnet tool restore
dotnet build
```

### 2. Configure secrets

Secrets are stored with [.NET user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) — never in source control or `appsettings.json`.

```bash
dotnet user-secrets set "Anthropic:ApiKey" "YOUR_ANTHROPIC_KEY" --project src/TeachingAssistant.WebAPI
dotnet user-secrets set "Google:ClientId" "YOUR_CLIENT_ID" --project src/TeachingAssistant.WebAPI
dotnet user-secrets set "Google:ClientSecret" "YOUR_CLIENT_SECRET" --project src/TeachingAssistant.WebAPI
```

The Anthropic key can alternatively be provided through the `ANTHROPIC_API_KEY` environment variable.

### 3. Start the database

Copy `.env.example` to `.env` (git-ignored) and choose a local password, then start Postgres and point the API at it:

```bash
docker compose up -d
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=teaching_assistant;Username=teaching_assistant;Password=YOUR_LOCAL_PASSWORD" --project src/TeachingAssistant.WebAPI
```

In Development the API creates the database and applies pending migrations when it starts, so there is no separate step. Outside Development, apply them deliberately with `dotnet ef database update --project src/TeachingAssistant.Infrastructure --startup-project src/TeachingAssistant.WebAPI`.

Data lives in a Docker volume, outside the repository. Submission files are stored under `%LOCALAPPDATA%\TeachingAssistant` (configurable as `Storage:Root`), outside the repository and outside synced folders such as OneDrive, as `submissions\{course}\{assignment}\{student} ({id})\v{n}\` with `original\` (exactly as submitted) and `code\` (archives unpacked, IDE folders and compiled files left out). Assignment instructions are stored next to them as `instructions\{course}\{assignment} ({id})\v{n}\original\`. After changing the entity model, add a migration with `dotnet ef migrations add <Name> --project src/TeachingAssistant.Infrastructure --startup-project src/TeachingAssistant.WebAPI --output-dir Persistence/Migrations`.

### 4. Set up Google Classroom access

1. Create a project in the [Google Cloud Console](https://console.cloud.google.com/).
2. Enable the **Google Classroom API** and the **Google Drive API**.
3. Configure the OAuth consent screen: audience **External**, publishing status **Testing**, and add your Google account as a test user.
4. Create an OAuth client ID of type **Desktop app** and store its ID and secret as shown above.

> In Testing mode, Google refresh tokens expire after about 7 days. Reconnect with `POST /auth/google/connect` when the status shows `ReconnectRequired`.

### 5. Run

```bash
dotnet run --project src/TeachingAssistant.WebAPI --launch-profile https
```

Swagger UI opens at `https://localhost:7107/swagger`. To connect Classroom, call `POST /auth/google/connect` — a browser window opens for Google sign-in and consent.

### 6. Run the frontend

Requires [Node.js](https://nodejs.org) (LTS). With the API running on `http://localhost:5288`:

```bash
cd frontend
npm install
npm run dev
```

Open `http://localhost:5173`. The Vite dev server forwards `/api/*` to the API, so no CORS setup is needed. After changing API contracts, regenerate the TypeScript types with `npm run gen:api` (the API must be running). To point the dev server at an API on another port, set `API_URL` (e.g. `API_URL=http://localhost:5299`).

## API

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/health` | API liveness check |
| `GET` | `/health/model` | Sends a test prompt to Claude; returns model, latency and reply |
| `GET` | `/auth/google/status` | Google connection state: `NotConfigured`, `NotConnected`, `Connected` or `ReconnectRequired` |
| `POST` | `/auth/google/connect` | Opens Google sign-in on the local machine and stores the authorization |
| `POST` | `/auth/google/disconnect` | Revokes access at Google and deletes the stored token |
| `GET` | `/courses` | Courses the teacher teaches in Google Classroom |
| `POST` | `/courses/{courseId}/sync` | Copies the course's roster, assignments and every student's state and Classroom grade history into the database (no files, nothing written to Classroom) |
| `GET` | `/courses/{courseId}/students` | The course's students with initial/final grade per assignment and overall score, from the database |
| `GET` | `/students/{userId}/photo` | A student's stored profile photo by Google user ID, with its hash as ETag (304 when unchanged); 404 when there is none |
| `GET` | `/courses/{courseId}/assignments` | Published assignments of a course, newest first, with the task statement, due date and materials (the teacher's attached instruction files and links) |
| `GET` | `/courses/{courseId}/assignments/{assignmentId}/submissions` | Every student's submission: state, late flag, latest turn-in, attachments (Drive files and links) |
| `POST` | `/courses/{courseId}/assignments/{assignmentId}/submissions/fetch` | Stores the assignment's instructions (when new or changed), then downloads and stores new or changed turned-in submissions as versions, and restores files of unchanged versions that are missing on disk; returns a summary with the instructions version and problems |
| `GET` | `/courses/{courseId}/assignments/{assignmentId}/submissions/stored` | Submissions stored by fetch, with all versions (newest first) and their files |
| `POST` | `/courses/{courseId}/assignments/{assignmentId}/submissions/extract` | Rebuilds the extracted code of all stored versions from the stored originals (no download) |
| `GET` | `/stored-files/{contentId}` | One stored file by ID, shown inline (images, PDF) or saved with `?download=true` |
| `GET` | `/courses/{courseId}/assignments/{assignmentId}/instructions` | The newest stored instructions version (task statement and materials), as grading sends them; 204 before the first fetch |
| `GET` | `/courses/{courseId}/assignments/{assignmentId}/instructions/files/{contentId}` | One stored instructions file, shown inline or saved with `?download=true` |
| `GET` | `/versions/{versionId}/code` | Files extracted from a stored version |
| `POST` | `/versions/{versionId}/grade` | Sends a stored version and its assignment's stored instructions to Claude saves the answer as a new draft and returns it: review (Markdown), score / max / bonus, mistakes (file, lines, severity, category, message), the instructions version and the files sent and skipped; 400 until a fetch has stored the instructions |
| `POST` | `/versions/{versionId}/grade/queue` | Queues an AI grading after quick checks and returns the job (`202 Accepted`); the worker saves the draft. Returns the existing job if one is already queued or running |
| `GET` | `/jobs/{jobId}` | A background job: status, progress, attempts, error, and the draft a finished grading produced |
| `POST` | `/jobs/{jobId}/cancel` | Cancels a job (queued: at once; running: at the worker's next check) |
| `GET` | `/courses/{courseId}/assignments/{assignmentId}/jobs` | An assignment's recent jobs, newest first |
| `GET` | `/versions/{versionId}/review` | The version's current draft (newest not discarded); 204 when it has none |
| `GET` | `/versions/{versionId}/gradings` | Every saved grading of a version, drafts and discarded, newest first |
| `GET` | `/courses/{courseId}/gradings` | A course's saved drafts as list rows (assignment, student, version, score, mistake counts), newest first; `?assignmentId=` for one assignment, `?studentId=` (Google user ID) for one student across assignments (both combine), `?all=true` to include earlier and discarded drafts. Filtered and counted in the database (gradings → versions → submissions) |
| `GET` | `/courses/{courseId}/assignments/{assignmentId}/gradings` | Every saved grading of an assignment with student, version, score and mistake counts |
| `POST` | `/gradings/{gradingId}/discard` | Discards a draft (it stays in the history) |
| `GET` | `/versions/{versionId}/code/file?path=…` | One extracted file (source code as UTF-8 text); paths outside the version's code folder are refused |
| `POST` | `/agent/chat` | Chat with the assistant: sends the conversation so far with the teacher's new message and returns the whole reply (Markdown); long histories are trimmed to the most recent messages |

Errors are returned as [RFC 9457 Problem Details](https://www.rfc-editor.org/rfc/rfc9457). The full, documented specification is available at `/swagger/v1/swagger.json`.

## Testing

```bash
dotnet test
```

Tests focus on Application use cases and Domain rules, using hand-written fakes for ports such as `ILanguageModel`.

## Security and privacy

- **Read-only by design.** The app requests only read-only Google scopes (`classroom.courses.readonly`, `classroom.coursework.students.readonly`, `classroom.rosters.readonly`, `classroom.profile.photos`, `drive.readonly`). Google itself rejects any write attempt, so grades can never be published by the tool. Grades are only *read* from Classroom (with their history) and kept in the local database.
- **Teacher in control.** The AI produces drafts only; nothing is final until the teacher approves it.
- **Student code is isolated.** Submissions run only inside Docker sandboxes, never on the host.
- **Privacy.** Real student data is approved for the MVP and Demo Day. The model receives only what grading needs (instructions, code and the student's files), never names, emails or photos. Photos are stored locally and served by the app itself. For later scale: a local model or anonymization before anything reaches an LLM.
- **Secrets stay local.** API keys and OAuth client secrets live in user-secrets or environment variables; OAuth tokens are stored outside the repository.
- **Student work stays on the machine.** Fetched submissions are stored in a local folder outside the repository and outside cloud-synced folders.

## Author

Built by Luka Khachapuridze as a solo project for the AI Agents Builder Program (HIGGS × KIU), 2026.
