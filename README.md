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
| **Classroom ingestion** | Connects a teacher's Google account (read-only OAuth), lists courses, assignments and submissions, and fetches turned-in work into stored versions (snapshots); unchanged submissions are skipped | Listing ✅ · fetch & versions ✅ |
| **Submission formats** | Zip/rar/7z archives of `.java` files, loose files, documents, images, or GitHub repository links | Drive files stored ✅ · archive extraction and GitHub cloning planned |
| **Sandboxed checks** | Compiles and runs student code in Docker (JDK 17–27), collecting compiler warnings, static analysis and test results before any AI judgment | Planned |
| **Rubric grading** | Claude grades against a layered rubric: base rubric → per-assignment extensions → the teacher's policy rules | Planned |
| **Batch calibration** | Calibrates scores across the class and clusters common mistakes, so one explanation covers many students | Planned |
| **Resubmissions** | Detects new versions, grades the diff, reports fixed / remaining / new mistakes; tracks initial and final scores | Planned |
| **Review dashboard** | Code viewer with inline mistake comments, score editing and approval | Planned |
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
| **Frontend** | **React 19** + Vite + TypeScript, Tailwind CSS, shadcn/ui, Monaco Editor |
| **Tests** | **xUnit v3** on Microsoft Testing Platform |

## Project structure

```
TeachingAssistant.WebAPI.sln
global.json                              test runner configuration (Microsoft Testing Platform)
dotnet-tools.json                        local tools (dotnet-ef)
docker-compose.yml                       local Postgres
src/
  TeachingAssistant.Domain/
    Ingestion/                           submissions, versions (snapshots), content, change detection
  TeachingAssistant.Application/
    Contracts/                           what the API returns
    Interfaces/                          ports (ILanguageModel, ISubmissionSource)
    Interfaces/Services/                 use-case interfaces
    Services/                            use-case implementations
    Exceptions/                          application-level errors
  TeachingAssistant.Infrastructure/
    LanguageModel/                       Claude adapter
    Google/                              OAuth, Classroom adapter, Drive adapter (file metadata and download)
    Persistence/                         EF Core DbContext, entity mappings, migrations, submission store
    Storage/                             submission files on disk (versions written atomically)
  TeachingAssistant.WebAPI/
    Controllers/                         HTTP endpoints
    Extensions/                          service registration
    ExceptionHandling/                   exceptions → ProblemDetails responses
tests/
  TeachingAssistant.Tests/               xUnit tests for Domain and Application
frontend/                                React app (coming soon)
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

Copy `.env.example` to `.env` (git-ignored) and choose a local password, then start Postgres and create the tables:

```bash
docker compose up -d
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=teaching_assistant;Username=teaching_assistant;Password=YOUR_LOCAL_PASSWORD" --project src/TeachingAssistant.WebAPI
dotnet ef database update --project src/TeachingAssistant.Infrastructure --startup-project src/TeachingAssistant.WebAPI
```

Data lives in a Docker volume, outside the repository. Submission files are stored under `%LOCALAPPDATA%\TeachingAssistant` (configurable as `Storage:Root`), outside the repository and outside synced folders such as OneDrive, as `submissions\{course}\{assignment}\{student} ({id})\v{n}\original\`. After changing the entity model, add a migration with `dotnet ef migrations add <Name> --project src/TeachingAssistant.Infrastructure --startup-project src/TeachingAssistant.WebAPI --output-dir Persistence/Migrations`.

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

## API

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/health` | API liveness check |
| `GET` | `/health/model` | Sends a test prompt to Claude; returns model, latency and reply |
| `GET` | `/auth/google/status` | Google connection state: `NotConfigured`, `NotConnected`, `Connected` or `ReconnectRequired` |
| `POST` | `/auth/google/connect` | Opens Google sign-in on the local machine and stores the authorization |
| `POST` | `/auth/google/disconnect` | Revokes access at Google and deletes the stored token |
| `GET` | `/courses` | Courses the teacher teaches in Google Classroom |
| `GET` | `/courses/{courseId}/assignments` | Published assignments of a course, newest first, with the task statement and due date |
| `GET` | `/courses/{courseId}/assignments/{assignmentId}/submissions` | Every student's submission: state, late flag, latest turn-in, attachments (Drive files and links) |
| `POST` | `/courses/{courseId}/assignments/{assignmentId}/submissions/fetch` | Downloads and stores new or changed turned-in submissions as versions; returns a summary with problems |

Errors are returned as [RFC 9457 Problem Details](https://www.rfc-editor.org/rfc/rfc9457). The full, documented specification is available at `/swagger/v1/swagger.json`.

## Testing

```bash
dotnet test
```

Tests focus on Application use cases and Domain rules, using hand-written fakes for ports such as `ILanguageModel`.

## Security and privacy

- **Read-only by design.** The app requests only read-only Google scopes (`classroom.courses.readonly`, `classroom.coursework.students.readonly`, `classroom.rosters.readonly`, `drive.readonly`). Google itself rejects any write attempt, so grades can never be published by the tool.
- **Teacher in control.** The AI produces drafts only; nothing is final until the teacher approves it.
- **Student code is isolated.** Submissions run only inside Docker sandboxes, never on the host.
- **Privacy.** Student identifiers are anonymized before any data is sent to a language model.
- **Secrets stay local.** API keys and OAuth client secrets live in user-secrets or environment variables; OAuth tokens are stored outside the repository.
- **Student work stays on the machine.** Fetched submissions are stored in a local folder outside the repository and outside cloud-synced folders.

## Author

Built by Luka Khachapuridze as a solo project for the AI Agents Builder Program (HIGGS × KIU), 2026.
