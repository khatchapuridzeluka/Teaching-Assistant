# AI Teaching Assistant — Grading Agent

Solo hackathon project for the **AI Agents Builder Program (HIGGS × KIU)**, Oct 31 – Nov 28, 2026. No stack or data restrictions from organizers.
Demo Day: **Nov 28** — 5 min demo + 10 min Q&A, jury grades on predefined criteria (criteria not yet known — ask organizers).

## The problem

The author teaches two groups:
- **Advanced Java — 15 students.** Advanced OOP, class design, design patterns, console I/O now; SQL, Maven/Gradle, Spring Boot, multithreading coming soon.
- **C++ basics — 15 students.** Nested loops, sorting, simple console programs.

Today grading means: download each submission from Google Classroom, send it to Claude with the grading plan, then correct the result by hand. Plus writing a personal overview for every student. It takes hours.

## Scope order

**Java first** (hardest case — if it handles Java, C++ basics is easy), starting with class design and design patterns, then SQL/Maven/Gradle/Spring Boot. C++ later.

## The product (priority order)

1. **Grader — the core (~70% of effort).**
   - Ingestion via an `ISubmissionSource` adapter. **Google Classroom first**, zip upload as fallback. Classroom submissions may contain a **GitHub link** instead of a zip — the GitHub adapter clones it; the project may become GitHub-heavy.
   - Build + run in **Docker sandboxes**. Never run student code on the host.
   - Deterministic facts before the LLM: compiler warnings, Checkstyle/PMD/SpotBugs, stdin/stdout tests for console tasks.
   - Class design/patterns are mostly judged by the LLM against the rubric (task statements usually don't fix class/method names).
   - Batch features (all students solve the same task): calibration pass for consistent scores, class-wide clustering of common mistakes, review one explanation per cluster, similarity flags (flag only, never accuse).
   - **Human in the loop:** the agent only drafts; the teacher approves/edits in a review dashboard.
2. **Student profiler — thin layer on top of grading.** Every mistake stored as a structured record; new submissions labeled new / repeated / similar / previously fixed (to track growth, not to lower grades). GitHub commit history feeds it. Trends computed in code; LLM writes the narrative.
3. **Lecture participation — stretch goal only.**

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

Two scores per student per assignment:
- **Initial score** — first submission.
- **Final score** — after the student fixes mistakes and resubmits/recommits.

**Resubmissions have no fixed form:** usually new commits, but possibly a new zip or just a new/changed file in Classroom. No fixed deadline. So:
- Every fetch stores a **snapshot** (files + content hash, commit SHA for GitHub). A changed hash/SHA vs. the graded snapshot = new version detected, shown in the UI.
- Regrading a new version gets the previous grading's mistakes + the **diff**, and reports which mistakes were fixed, which remain, and any new ones.
- The **teacher decides** which version counts as final — never inferred automatically.

Stored **only in the tool** (DB + UI, exportable to the teacher's own file). **The agent must never upload or write grades anywhere** — enforced technically by using read-only Google OAuth scopes and read-only GitHub access, not only by instructions.

## UI (React) — the teacher's workspace

Agents work in the background; the UI is where the teacher reviews and decides. Screens:
1. **Home** — courses → assignments with status counts (new, drafted, approved, new version).
2. **Assignment** — student table (status, initial/final score, flags), "Grade all", class-wide mistake clusters with approve-per-cluster.
3. **Submission review** — code viewer with mistakes as **inline line comments** (like a PR review) + rubric scores, mistakes, feedback; edit/approve; version diff; "ask why" chat with the agent.
4. **Student profile** — score trend, mistake categories, repeated vs. fixed, narrative overview.
5. **Settings** — base rubric, policy rules, Classroom/GitHub connections.

## Evaluation

Previously graded homework exists (Claude-drafted, teacher-corrected). Use it as the eval set: "the agent agrees with the teacher's final grade X% of the time" is the key Demo Day number.

## Tech stack

- **Web app**, run locally first (single user, no auth yet), deployed to GCP later.
- **Backend:** C# / ASP.NET Core API (the tool is .NET even though graded code is Java/C++). Written and reviewed carefully by the teacher — all grading logic, agents and data live here.
- **Frontend:** React + Vite + TypeScript, Tailwind + shadcn/ui, `@monaco-editor/react` (code viewer, inline comments, diff), Recharts, TanStack Query. **Mostly AI-generated ("vibe-coded")** — so it must stay a thin client: no grading/business logic in the frontend, it only displays and edits what the API returns.
- **API contract first:** endpoints and DTOs defined in the backend, exposed via **Swashbuckle** (Swagger UI at `/swagger`, spec at `/swagger/v1/swagger.json`; not Microsoft.AspNetCore.OpenApi); TypeScript types are generated from it, never hand-written.
- Semantic Kernel — or Microsoft Agent Framework if that's where SK's agent features moved (verify current status before starting)
- Google Classroom API + Drive API (read-only scopes), **personal Google account**: own Google Cloud project, OAuth consent screen in "Testing" mode with the teacher as test user. Note: refresh tokens in Testing mode expire after ~7 days — re-auth flow must be easy.
- Postgres + pgvector (grades, mistake records, profiles)
- Docker sandboxes (multiple JDK images); Polly for retries; OpenTelemetry logging
- Deploy: Cloud Run (web + API) + Vertex AI. Cloud Run can't run Docker inside itself → sandboxes run as Cloud Run Jobs or on a small Compute Engine VM. Keep the sandbox runner a separate component from day one.
- Course teaches Python LangChain/LangGraph — translate concepts: tools→SK plugins, LangGraph→SK orchestration/Process Framework, retrievers→SK vector store

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

## README

When a change affects anything `README.md` describes (features and their status, stack, project structure, setup steps, configuration, API endpoints, security notes), update `README.md` in the same change.

## Prompting conventions

- Instructions in natural language, explain the *why*.
- Student code raw, never JSON-escaped. Sections delimited with XML tags (`<task>`, `<rubric>`, `<grading_policy>`, `<build_and_test_results>`, `<submission>`, `<student_history>`).
- Order: shared parts first (task, rubric, policy, reference solution) → per-student parts last, for prompt caching.
- Output: structured output bound to C# records (`GradingResult`, `CriterionScore`, `Mistake`).
- `Category` and `Severity` are fixed enums, never free text.
- **All feedback and mistakes in English** (the teacher delivers in Georgian personally).
- Score first (structured), then generate student-facing feedback in a separate pass.

## Privacy

Anonymize student IDs before sending anything to an LLM. Use anonymized or synthetic data on Demo Day.

## Timeline

- **By Nov 8:** Classroom fetch (zip + GitHub link) → sandbox compile → rubric + policy grading of a Java class-design task → review screen with initial score.
- **By Nov 15:** resubmission → final score, mistake DB + new/repeated labels, orchestrator, batch calibration + mistake clustering.
- **By Nov 22:** student profile page with charts, accuracy eval vs. past grades, GCP deploy.
- **Nov 23–28:** demo polish only — no new features.

## Open questions

- **Automated tests vs. free naming:** task statements don't always fix class/method names. Options: fix names in future tasks (enables hidden JUnit tests), test only console input → output, or rely on LLM review. Decide per assignment type.
- **Base rubric:** write it together with the teacher — categories, point weights, policy rules.
