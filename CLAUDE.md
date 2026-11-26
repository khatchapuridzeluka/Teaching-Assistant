# AI Teaching Assistant — Grading Agent

Grades Java homework for the teacher's Advanced Java courses: fetches submissions from Google Classroom (zip or GitHub link), compiles and checks them in Docker sandboxes, and drafts rubric-based scores, mistakes and feedback with Claude; the teacher reviews, edits and approves everything in a React dashboard, and student profiles track mistakes and strengths over time. One score per grading; regrading creates a new draft. The agent only drafts; nothing is final until the teacher approves it.
The product spec (problem, scope, features, submissions, rubric layers, UI screens, evaluation, technical choices, the deployment plan, open questions) is in [docs/PRODUCT.md](docs/PRODUCT.md).

## Non-negotiables

These outrank every other rule in this file. Breaking one harms students, the teacher's Google account or the teacher's bill, and reverting the commit does not undo it. If a task seems to need one broken, stop and ask the teacher.

1. **Never run student code on the host.** It runs only through `ISandboxRunner`, in a throwaway, locked-down Docker container. That includes a quick `javac` or `java` on a submission "just to check": student code is untrusted.
2. **Never widen Google or GitHub access beyond read-only.** The scopes in `src/TeachingAssistant.Infrastructure/Google/GoogleAuthorization.cs` stay read-only (plus `classroom.profile.photos`), the GitHub token stays limited to reading public repositories, and no code writes to Classroom, Drive or GitHub. Read-only access is what guarantees that grades never leave the tool and no student's repository is changed.
3. **Never trigger paid LLM calls** unless the teacher explicitly asks: gradings (single, Grade all, Try with model), class and student analyses, evaluation runs, agent chats and runs, and `GET /health/model`. Verify with fakes and free endpoints.
4. **Never send student names, emails or photos to grading or analysis prompts.** The model gets only what grading needs; the one exception is the chat agent's `list_submissions` (see Privacy).

## Commands

Run from the repository root; they work in bash and PowerShell alike. `.claude/launch.json` has `api` and `frontend` entries that start the API and Vite.

```bash
# Tests on Microsoft.Testing.Platform (global.json), which needs --project; builds the test project, not the WebAPI
dotnet test --project tests/TeachingAssistant.Tests
dotnet test --project tests/TeachingAssistant.Tests --filter-class "*GradesTests"   # one class; --filter-method and --filter-namespace work too

# Add missing braces (IDE0011)
dotnet format style --diagnostics IDE0011

# API: http profile on :5288 (the https profile adds :7107); Development applies pending migrations on start
dotnet run --project src/TeachingAssistant.WebAPI --launch-profile http

# Frontend: install once, then the dev server on :5173, which forwards /api to :5288 (set API_URL for another API)
npm --prefix frontend install
npm --prefix frontend run dev
npm --prefix frontend run build     # type check (tsc --noEmit), then a production build into frontend/dist
npm --prefix frontend run gen:api   # regenerate frontend/src/api/schema.d.ts; needs the API running at :5288

# EF Core migrations (dotnet-ef is a local tool); they build the WebAPI
dotnet tool restore
dotnet ef migrations add <Name> --project src/TeachingAssistant.Infrastructure --startup-project src/TeachingAssistant.WebAPI --output-dir Persistence/Migrations
dotnet ef migrations remove --project src/TeachingAssistant.Infrastructure --startup-project src/TeachingAssistant.WebAPI
dotnet ef database update --project src/TeachingAssistant.Infrastructure --startup-project src/TeachingAssistant.WebAPI   # outside Development only

# Postgres 18 + pgvector on 127.0.0.1:5432; copy .env.example to .env and set POSTGRES_PASSWORD first
docker compose up -d

# Sandbox images: ta-java:27 (first choice) and ta-java:21 (fallback); rebuild after changing sandbox/java/Dockerfile
docker build -t ta-java:27 sandbox/java
docker build -t ta-java:21 --build-arg JDK_VERSION=21 sandbox/java

# User-secrets of the WebAPI project; the teacher fills in the values, never print them (no `user-secrets list`)
dotnet user-secrets set "Anthropic:ApiKey" "<key>" --project src/TeachingAssistant.WebAPI
dotnet user-secrets set "Google:ClientId" "<client-id>" --project src/TeachingAssistant.WebAPI
dotnet user-secrets set "Google:ClientSecret" "<client-secret>" --project src/TeachingAssistant.WebAPI
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=teaching_assistant;Username=teaching_assistant;Password=<POSTGRES_PASSWORD>" --project src/TeachingAssistant.WebAPI
dotnet user-secrets set "GitHub:Token" "<token>" --project src/TeachingAssistant.WebAPI   # optional: fine-grained, public repositories read-only
```

## Definition of done

A change is done when all of these hold. Each item checks a rule stated elsewhere in this file:

1. `dotnet test --project tests/TeachingAssistant.Tests` passes.
2. The build shows no IDE0011 warnings (Braces).
3. If contracts changed, the API types are regenerated (API types, under Working conventions).
4. If the change touches anything the README or its docs describe, they are updated (README, under Working conventions).
5. No paid LLM calls were made (non-negotiable 3), and the final message says what was verified and how.

## Tech stack

- **Web app**, runs locally (single user, no auth yet).
- **Backend:** C# / ASP.NET Core API on .NET 10 (the tool is .NET even though graded code is Java). Written and reviewed carefully by the teacher — all grading logic, agents and data live here.
- **Frontend:** React + Vite + TypeScript, Tailwind CSS with the project's own components (no component library), `@monaco-editor/react` (code viewer with inline mistake marks), Recharts, TanStack Query, React Router. **Mostly AI-generated ("vibe-coded")** — so it must stay a thin client: no grading/business logic in the frontend, it only displays and edits what the API returns.
- **API contract first:** endpoints and contracts defined in the backend, exposed via **Swashbuckle** (Swagger UI at `/swagger`, spec at `/swagger/v1/swagger.json`; not Microsoft.AspNetCore.OpenApi); TypeScript types are generated from it.
- **Microsoft Agent Framework** (`Microsoft.Agents.AI`) for agents with tools, over the Anthropic SDK's `IChatClient`. A tool is a public method marked `[AgentTool("name")]` with a `[Description]` (from `System.ComponentModel`), in a class under `Application/Tools/`: a small adapter that calls an existing service and returns only what the model needs, with failures returned as text. `AgentToolAttribute` is the project's own attribute, defined in `Application/Tools/AgentToolAttribute.cs`, so tools need no framework package; `ClaudeAgent` (`Infrastructure/LanguageModel/`) finds the marked methods by reflection and wraps each with `AIFunctionFactory.Create`. Only Infrastructure references `Microsoft.Agents.AI` (and `Microsoft.Extensions.AI`, which comes with it); never add either to Application or Domain.
- Google Classroom API + Drive API (read-only scopes), **personal Google account**: own Google Cloud project, OAuth consent screen in "Testing" mode with the teacher as test user. Note: refresh tokens in Testing mode expire after ~7 days — re-auth flow must be easy.
- **Postgres 18** with EF Core 10 (grades, mistake records, profiles); the pgvector extension is installed but not used yet.
- **Docker sandboxes** (`ta-java:27`, with `ta-java:21` as fallback) driven by the `docker` command line; **Serilog** for logging; **QuestPDF** with Markdig for the PDF reports.

## Architecture

**Monolith, onion layering, 4 projects + tests** (.NET 10). One repo:

```
TeachingAssistant.WebAPI.sln
src/TeachingAssistant.Domain          entities, enums, pure rules (score calc, policy, new/repeated labels, version compare)
src/TeachingAssistant.Application     use cases (GradeAssignment, RegradeNewVersion, ApproveGrade), interfaces, prompt templates
src/TeachingAssistant.Infrastructure  Claude (Anthropic SDK), EF Core/Postgres, Classroom/GitHub adapters, Docker runner
src/TeachingAssistant.WebAPI          presentation: thin controllers, OpenAPI, DI composition root
tests/TeachingAssistant.Tests         xUnit v3 — focus on Domain rules and Application flows with fakes
frontend/                             React + Vite (edited in Rider)
```

- Dependencies point inward: WebAPI → Application → Domain; Infrastructure → Application/Domain. WebAPI references Infrastructure **only** for DI registration.
- Domain and Application reference **no** external packages (no Anthropic, EF Core, Google, Docker). If they do, the boundary is broken.
- Organize inside projects by feature folders: `Ingestion/`, `Grading/`, `Sandbox/`, `Profiling/`.
- Application layout: `Interfaces/` (ports like `ILanguageModel`), `Interfaces/Services/` (use-case interfaces), `Services/` (use-case implementations — pure, no SDKs), `Contracts/` (what the API returns), `Models/` (internal port types), `Exceptions/`. Infrastructure implements ports and translates SDK exceptions into Application exceptions (e.g. `LanguageModelUnavailableException`).
- All LLM calls go through `ILanguageModel` — never call the Anthropic SDK from anywhere else. Model and settings in the `Anthropic` config section; the API key only in user-secrets (`Anthropic:ApiKey`) or the `ANTHROPIC_API_KEY` env var.
- No MediatR/CQRS, no generic repository/unit-of-work over EF, no mapping layers between identical shapes.
- All sandbox work goes through `ISandboxRunner`; only `Infrastructure/Sandbox/` starts the `docker` program.

### One type per file

Every class, record, enum, interface and struct gets its own file, named after the type (`AgentRunResponse.cs`), small helper records and enums included. Files are grouped in feature folders, never in subfolders by size, and namespaces match folders, except in `Contracts/`:

- **Contracts:** one folder per controller, named exactly like it (`Contracts/Agent/` for `AgentController`, `Contracts/Students/` for `StudentsController`). A contract goes in the folder of the controller that accepts or returns it; a helper record no controller names goes with the contract that holds it; a contract several controllers use goes with the feature that owns it (`JobResponse` in `Jobs/`). No nested types.
- **One flat namespace for all contracts:** every file in `Contracts/` declares `namespace TeachingAssistant.Application.Contracts;`, whatever its folder. Swagger names each schema by its bare type name and the frontend's type aliases come from those names, so contract names must be unique, and one namespace makes the compiler check that. Contracts may expose Domain enums; `ContractsTests` fails if any type the contracts expose shares a schema name with another.
- **Nested types:** only `private` helper types may stay nested inside a class (and `internal` ones the tests use); anything else gets its own file.
- **Enforced in the build:** StyleCop's SA1402 (one type per file) and SA1649 (file named after its type) are errors everywhere except the generated migrations, and IDE0130 (namespace matches folder) warns everywhere except `Contracts/`; all set in `.editorconfig`, where every other StyleCop rule is off.

### Records

Every record, single-property ones included, is declared with `init` properties, never a positional parameter list, and is built with an object initializer, never positionally:

- A non-nullable property that must be set is `required`. An optional property is nullable with no default; give a default (`= …`) only when it must be a non-null value. Lists are `required` and never nullable (empty is `[]`).
- One property per line, in the order callers set them; one-line `<summary>` docs go on the property, not as `<param>` tags on the record.
- Build with `new AgentLink { Kind = …, Label = …, CourseId = … }`, or `new() { … }` when the type is clear; leave out optional properties instead of setting them to `null`.
- Some records are stored as JSON and read back (job payloads and results, `AgentToolCall`, `AgentLink`, `RunMistake`): a property added to one of them must be optional or have a default, never `required`, or rows written before it no longer load.
- `RecordsTests` fails if any record still has a positional constructor.

```csharp
public record StartAgentRunRequest
{
    public required string Text { get; init; }
    public Guid? ConversationId { get; init; }
    public string? Page { get; init; }
    public string? CourseId { get; init; }
    public string? AssignmentId { get; init; }
}
```

### Controller conventions

Controllers live in `src/TeachingAssistant.WebAPI/Controllers/`. Contracts (use-case inputs/outputs the API returns) live in `src/TeachingAssistant.Application/Contracts/`; controllers return them directly. Contract names never get a `Dto` suffix: what an endpoint returns ends in `Response` (`AssignmentResponse`), what it takes ends in `Request` (`PolicyRuleRequest`), and the result of an action may be a `Summary` (`FetchSummary`). Smaller records used only inside a contract may have plain names (`FetchProblem`, `GradebookColumn`). Where each contract file goes is under One type per file. A WebAPI-side request type exists only for HTTP-specific shapes (e.g. `IFormFile` uploads), mapped to an Application contract in the controller. Controllers stay thin: constructor-inject an Application service, call it, return the result. Document every action with a short `<summary>` (what it returns/does, one sentence) and one `<response code>` line per status — no long remarks. Return `ActionResult<T>`, suffix async actions with `Async`. Every controller member uses a block body `{ }` with an explicit `return`, never an expression body (`=>`), even for one line. The XML docs flow into the OpenAPI document.

```csharp
/// <summary>
/// Returns a single assignment by ID.
/// </summary>
/// <response code="200">Assignment retrieved successfully</response>
/// <response code="404">Assignment not found</response>
[HttpGet("{id:guid}")]
public async Task<ActionResult<AssignmentResponse>> GetByIdAsync(Guid id)
{
    return Ok(await _assignmentService.GetByIdAsync(id));
}
```

### Braces

Every `if`, `else`, `for`, `foreach`, `while`, `do`, `using` and `lock` body uses braces `{ }`, even when it is a single line — in C# and in the frontend's TypeScript alike. Never write a one-line body without them:

```csharp
// Wrong
if (request.Messages.Count == 0)
    throw new InvalidRequestException("...");

// Right
if (request.Messages.Count == 0)
{
    throw new InvalidRequestException("...");
}
```

Generated code (EF Core migrations, `schema.d.ts`) is left as generated. For C# the rule is enforced: `.editorconfig` sets IDE0011 and `Directory.Build.props` turns on `EnforceCodeStyleInBuild`, so `dotnet build` warns about a missing brace. `dotnet format style --diagnostics IDE0011` fixes them automatically. TypeScript has no linter yet, so check it by hand.

### Frontend layout

- `frontend/src/api/`: `schema.d.ts` is generated; `client.ts` holds the `openapi-fetch` client, `unwrap` and short aliases of the generated types (`export type Course = components["schemas"]["CourseResponse"]`); every TanStack Query hook goes in `hooks.ts` as `export const useX = …` with a hierarchical query key (`["courses", courseId, "assignments"]`), the agent chat's in `chat.ts`. Pages and components never call `api`, `fetch` or `useQuery` themselves.
- `frontend/src/pages/`: one `<Name>Page.tsx` per route; routes are registered in `main.tsx`, and the course and workspace sections (with their icons) in `navigation.ts`.
- `frontend/src/components/`: feature components named after what they show (`SubmissionDetail.tsx`, `Rubric*.tsx` for the rubric page's tabs). Shared primitives (`Button`, `Card`, `Modal`, `Chip`, `Tabs`, …) are in `ui.tsx` and small grading displays (`Score`, `SeverityDots`, `DueChip`) in `bits.tsx`; reuse them before writing new ones.
- `frontend/src/format.ts` holds formatting helpers; `theme.ts` holds colours for charts and code marks, kept in step with the design tokens in `index.css`.

## Working conventions

- **The API runs in Rider** on :5288, usually with Vite on :5173, and Rider locks the WebAPI's DLLs: anything that builds the WebAPI while it runs fails, `dotnet ef` included. Build and test through the test project instead, or build the WebAPI with `-o` to a temp folder. After backend changes, ask the teacher to restart the API in Rider.
- **API types:** after changing contracts and the teacher's restart, regenerate `frontend/src/api/schema.d.ts` with `npm --prefix frontend run gen:api`. Never hand-write or edit the types; a hand edit is only a stopgap until the regeneration.
- **README:** update the docs a change touches, in the same change. `README.md` is the short front door: one line per feature with its status, the architecture, stack, project structure, setup, deployment and CI, the API by area, prompts, testing and security notes. A feature's full detail goes in `docs/FEATURES.md`, and every endpoint with its one-line description in `docs/API.md`; a new or changed endpoint updates `docs/API.md`, and the README's area table only when it adds an area or route group.
- **No Prettier or other formatters on the frontend** — the project does not use one, and running it rewraps whole files.
- **Comments:** none in `src/` or the frontend except the controller XML docs.
- **Commits:** only when the teacher asks; the teacher often commits themselves.

## Prompting conventions

- **Where prompts live:** each prompt's fixed wording is a Markdown file in `src/TeachingAssistant.Application/Prompts/` (`agent.md`, `grading.md`, `class-mistakes.md`, `student-*.md`), embedded in the assembly (the csproj embeds `Prompts/*.md`) and read once by `PromptLibrary`. A `{{name}}` placeholder is filled with `PromptLibrary.Render`, and one left unfilled throws. Wording goes in the `.md` files, never in C# strings; only what changes per call (the teacher's rubric layers, the student's code and records, the JSON output schemas, the `[Description]` on each tool) is built in code. A new prompt is a new `.md` file plus a property on `PromptLibrary`.
- Instructions in natural language, explain the *why*.
- Student code raw, never JSON-escaped. Sections delimited with XML tags (`<task>`, `<rubric>`, `<grading_policy>`, `<build_and_test_results>`, `<submission>`, `<student_history>`).
- Order: shared parts first (task, rubric, policy, reference solution) → per-student parts last, for prompt caching.
- Output: structured output bound to C# records (`GradingResult`, `CriterionScore`, `Mistake`).
- `Category` and `Severity` are fixed enums, never free text.
- **All feedback and mistakes in English** (the teacher delivers in Georgian personally).
- Score first (structured), then generate student-facing feedback in a separate pass.

## Privacy

The program owners approved real student data for the MVP and Demo Day (sensitive data reaching the LLM is acceptable at this stage); what the model may still never get is non-negotiable 4. Its one exception, decided by the teacher for the demo: the chat agent's `list_submissions` tool returns student names so the teacher can ask about students by name. Student photos are stored locally (one copy per student, no history). For later scale, consider a local model or anonymizing again before anything goes to an LLM.
