# AI Teaching Assistant — Grading Agent

Solo hackathon project for the **AI Agents Builder Program (HIGGS × KIU)**, Oct 31 – Nov 28, 2026. No stack or data restrictions from organizers.
Demo Day: **Nov 28** — 5 min demo + 10 min Q&A, jury grades on predefined criteria (criteria not yet known — ask organizers).

## The problem

The author teaches **Advanced Java**, up to 8 students per course: advanced OOP, class design, design patterns, console I/O now; SQL, Maven/Gradle, Spring Boot, multithreading coming soon.

Today grading means: download each submission from Google Classroom, send it to Claude with the grading plan, then correct the result by hand. Plus writing a personal overview for every student. It takes hours.

## Scope order

Java only, starting with class design and design patterns, then SQL/Maven/Gradle/Spring Boot.

## The product (priority order)

1. **Grader — the core (~70% of effort).**
   - Ingestion via an `ISubmissionSource` adapter. **Google Classroom first**, zip upload as fallback. Classroom submissions may contain a **GitHub link** instead of a zip — the GitHub adapter clones it; the project may become GitHub-heavy.
   - Build + run in **Docker sandboxes**. Never run student code on the host.
   - Deterministic facts before the LLM: compiler warnings, Checkstyle/PMD/SpotBugs, stdin/stdout tests for console tasks.
   - Class design/patterns are mostly judged by the LLM against the rubric (task statements usually don't fix class/method names).
   - Batch features (all students solve the same task): calibration pass for consistent scores, class-wide clustering of common mistakes, review one explanation per cluster, similarity flags (flag only, never accuse).
   - **Human in the loop:** the agent only drafts; the teacher approves/edits in a review dashboard.
2. **Student profiler — thin layer on top of grading.** Every mistake stored as a structured record; new submissions labeled new / repeated / similar / previously fixed (to track growth, not to lower grades). GitHub commit history feeds it. Trends computed in code; LLM writes the narrative.

## Java submissions

- Usually a `.zip` of loose `.java` files, **no build file** (sometimes Maven/Gradle). Or a GitHub link.
- Java version varies per student, **17 to 27**. Compile with the newest JDK first (backward compatible for almost all student code); fall back to an older JDK image if needed.
- No build file → the runner must find all `.java` files, respect `package` declarations, locate `main`, and ignore junk (`__MACOSX`, `.idea`, `out/`, `.class` files, nested zips).
- Later: cached Maven/Gradle dependencies in the sandbox; Postgres container for SQL/Spring Boot; concurrency tests run multiple times + SpotBugs concurrency checks.

## Rubric and grading policy

Layered, all in the teacher's files:
1. **Base rubric** — static, general, reused across all assignments.
2. **Per-assignment extensions** — what this task adds or overrides.
3. **Policy rules** — explicit judgment calls, e.g. "typo in a printed string: not a mistake", "bad variable name: −5 points".

Rules are defined together with the teacher (old Claude chats had no hard rules). The teacher's edits in the review dashboard should suggest new policy rules. Policy goes into every grading prompt.

## Scores

One score per grading. **Dropped for Demo Day:** separate initial and final scores, diff-aware regrading (previous mistakes + diff → fixed / remaining / new) and the teacher choosing a final version. Grading a version again is enough: the latest draft is current, earlier drafts stay in the history to compare.

**Resubmissions have no fixed form:** usually new commits, but possibly a new zip or just a new/changed file in Classroom. No fixed deadline. Every fetch stores a **snapshot** (files + content hash, commit SHA for GitHub); a changed hash/SHA vs. the graded snapshot = new version detected, shown in the UI, and the teacher grades it again.

Stored **only in the tool** (DB + UI, exportable to the teacher's own file). **The agent must never upload or write grades anywhere** — enforced technically by using read-only Google OAuth scopes and read-only GitHub access, not only by instructions.

## UI (React) — the teacher's workspace

Agents work in the background; the UI is where the teacher reviews and decides. Screens:
1. **Home** — courses → assignments with status counts (new, drafted, approved, new version).
2. **Assignment** — student table (status, score, flags), "Grade all", class-wide mistake clusters with approve-per-cluster.
3. **Submission review** — code viewer with mistakes as **inline line comments** (like a PR review) + rubric scores, mistakes, feedback; edit/approve; version diff; "ask why" chat with the agent.
4. **Student profile** — score trend, mistake categories, repeated vs. fixed, narrative overview.
5. **Settings** — base rubric, policy rules, Classroom/GitHub connections.

## Evaluation

Previously graded homework exists (Claude-drafted, teacher-corrected). Use it as the eval set: "the agent agrees with the teacher's final grade X% of the time" is the key Demo Day number.

## Tech stack

- **Web app**, run locally first (single user, no auth yet), deployed to GCP later.
- **Backend:** C# / ASP.NET Core API (the tool is .NET even though graded code is Java). Written and reviewed carefully by the teacher — all grading logic, agents and data live here.
- **Frontend:** React + Vite + TypeScript, Tailwind + shadcn/ui, `@monaco-editor/react` (code viewer, inline comments, diff), Recharts, TanStack Query. **Mostly AI-generated ("vibe-coded")** — so it must stay a thin client: no grading/business logic in the frontend, it only displays and edits what the API returns.
- **API contract first:** endpoints and DTOs defined in the backend, exposed via **Swashbuckle** (Swagger UI at `/swagger`, spec at `/swagger/v1/swagger.json`; not Microsoft.AspNetCore.OpenApi); TypeScript types are generated from it, never hand-written.
- **Microsoft Agent Framework** (`Microsoft.Agents.AI`) for agents with tools, over the Anthropic SDK's `IChatClient` — Semantic Kernel is in maintenance mode since Agent Framework 1.0 (April 2026). A tool is a method marked `[AgentTool("name")]` with a `[Description]`, in a class under `Application/Tools/`: a small adapter that calls an existing service and returns only what the model needs, with failures returned as text. Infrastructure registers every marked method; only Infrastructure references the framework
- Google Classroom API + Drive API (read-only scopes), **personal Google account**: own Google Cloud project, OAuth consent screen in "Testing" mode with the teacher as test user. Note: refresh tokens in Testing mode expire after ~7 days — re-auth flow must be easy.
- Postgres + pgvector (grades, mistake records, profiles)
- Docker sandboxes (multiple JDK images); Polly for retries; OpenTelemetry logging
- Deploy: Cloud Run (web + API) + Vertex AI. Cloud Run can't run Docker inside itself → sandboxes run as Cloud Run Jobs or on a small Compute Engine VM. Keep the sandbox runner a separate component from day one.
- Course teaches Python LangChain/LangGraph — translate concepts: tools→`[AgentTool]` methods, LangGraph→the background agent run (`AgentRun` jobs with `wait_for_jobs`, see README), retrievers→pgvector behind a port

## Architecture

**Monolith, onion layering, 4 projects + tests** (.NET 10). One repo:

```
TeachingAssistant.WebAPI.sln
src/TeachingAssistant.Domain          entities, enums, pure rules (score calc, policy, new/repeated labels, version compare)
src/TeachingAssistant.Application     use cases (GradeAssignment, RegradeNewVersion, ApproveGrade), interfaces, prompt templates
src/TeachingAssistant.Infrastructure  Claude (Anthropic SDK), EF Core/Postgres, Classroom/GitHub adapters, Docker runner
src/TeachingAssistant.WebAPI          presentation: thin controllers, DTOs, OpenAPI, DI composition root; serves built React from wwwroot
tests/TeachingAssistant.Tests         xUnit v3 — focus on Domain rules and Application flows with fakes
frontend/                             React + Vite (edited in Rider); Vite proxies /api → ASP.NET in dev
```

- Dependencies point inward: WebAPI → Application → Domain; Infrastructure → Application/Domain. WebAPI references Infrastructure **only** for DI registration.
- Domain and Application reference **no** external packages (no Anthropic, EF Core, Google, Docker). If they do, the boundary is broken.
- Organize inside projects by feature folders: `Ingestion/`, `Grading/`, `Sandbox/`, `Profiling/`.
- Application layout: `Interfaces/` (ports like `ILanguageModel`), `Interfaces/Services/` (use-case interfaces), `Services/` (use-case implementations — pure, no SDKs), `Contracts/` (what the API returns), `Models/` (internal port types), `Exceptions/`. Infrastructure implements ports and translates SDK exceptions into Application exceptions (e.g. `LanguageModelUnavailableException`).- All LLM calls go through `ILanguageModel` — never call the Anthropic SDK from anywhere else. Model and settings in the `Anthropic` config section; the API key only in user-secrets (`Anthropic:ApiKey`) or the `ANTHROPIC_API_KEY` env var.
- No MediatR/CQRS, no generic repository/unit-of-work over EF, no mapping layers between identical shapes.
- Sandbox runner is the only part that becomes a separate deployable (week 3, Cloud Run Job/VM), behind `ISandboxRunner`.
- Production: one container — ASP.NET serves the API and the built React app.

### Controller conventions

Controllers live in `src/TeachingAssistant.WebAPI/Controllers/`. Contracts (use-case inputs/outputs the API returns) live in `src/TeachingAssistant.Application/Contracts/`; controllers return them directly. A WebAPI-side request type exists only for HTTP-specific shapes (e.g. `IFormFile` uploads), mapped to an Application contract in the controller. Controllers stay thin: constructor-inject an Application service, call it, return the result. Document every action with a short `<summary>` (what it returns/does, one sentence) and one `<response code>` line per status — no long remarks. Return `ActionResult<T>`, suffix async actions with `Async`. Every controller member uses a block body `{ }` with an explicit `return`, never an expression body (`=>`), even for one line. The XML docs flow into the OpenAPI document.

```csharp
/// <summary>
/// Returns a single assignment by ID.
/// </summary>
/// <response code="200">Assignment retrieved successfully</response>
/// <response code="404">Assignment not found</response>
[HttpGet("{id:guid}")]
public async Task<ActionResult<AssignmentDto>> GetByIdAsync(Guid id)
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

## README

When a change affects anything `README.md` describes (features and their status, stack, project structure, setup steps, configuration, API endpoints, security notes), update `README.md` in the same change.

## Working conventions

- **Tests:** build with `dotnet build tests/TeachingAssistant.Tests`, then run `dotnet tests/TeachingAssistant.Tests/bin/Debug/net10.0/TeachingAssistant.Tests.dll` — `dotnet test` reports zero tests on this setup.
- **The API runs in Rider** and locks its DLLs, so `dotnet build` of the WebAPI fails to copy while it runs; build the test project instead (or the WebAPI with `-o` to a temp folder). After backend changes, ask the teacher to restart the API in Rider.
- **API types:** after changing contracts, regenerate `frontend/src/api/schema.d.ts` with `npm run gen:api` (needs the API running at :5288). Never hand-write types; a hand edit is only a stopgap until the regeneration.
- **No Prettier or other formatters on the frontend** — the project does not use one, and running it rewraps whole files.
- **Paid AI calls:** never trigger gradings, analyses or agent runs while verifying a change unless the teacher agrees; test with fakes and free endpoints.
- **Comments:** none in `src/` or the frontend except the controller XML docs.
- **Commits:** only when the teacher asks; the teacher often commits themselves.

## Prompting conventions

- Instructions in natural language, explain the *why*.
- Student code raw, never JSON-escaped. Sections delimited with XML tags (`<task>`, `<rubric>`, `<grading_policy>`, `<build_and_test_results>`, `<submission>`, `<student_history>`).
- Order: shared parts first (task, rubric, policy, reference solution) → per-student parts last, for prompt caching.
- Output: structured output bound to C# records (`GradingResult`, `CriterionScore`, `Mistake`).
- `Category` and `Severity` are fixed enums, never free text.
- **All feedback and mistakes in English** (the teacher delivers in Georgian personally).
- Score first (structured), then generate student-facing feedback in a separate pass.

## Privacy

The program owners approved real student data for the MVP and Demo Day (sensitive data reaching the LLM is acceptable at this stage). Still send the model only what grading needs: never names, emails or photos. One exception, decided by the teacher for the demo: the chat agent's `list_submissions` tool returns student names so the teacher can ask about students by name; grading and analysis prompts still never get them. For later scale, consider a local model or anonymizing again before anything goes to an LLM. Student photos are stored locally (one copy per student, no history) and never sent to the model.

## Timeline

- **By Nov 8:** Classroom fetch (zip + GitHub link) → sandbox compile → rubric + policy grading of a Java class-design task → review screen with the score.
- **By Nov 15:** mistake DB + new/repeated labels, orchestrator, batch calibration + mistake clustering.
- **By Nov 22:** student profile page with charts, accuracy eval vs. past grades, GCP deploy.
- **Nov 23–28:** demo polish only — no new features.

## Open questions

- **Automated tests vs. free naming:** task statements don't always fix class/method names. Options: fix names in future tasks (enables hidden JUnit tests), test only console input → output, or rely on LLM review. Decide per assignment type.
- **Base rubric:** write it together with the teacher — categories, point weights, policy rules.
