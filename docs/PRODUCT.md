# Product

What the AI Teaching Assistant is and why. How to work in the repository is in [CLAUDE.md](../CLAUDE.md); what is built so far is in [README.md](../README.md).

Solo hackathon project for the **AI Agents Builder Program (HIGGS × KIU)**. No stack or data restrictions from organizers.

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

One score per grading; regrading creates a new draft. The latest draft is current, earlier drafts stay in the history to compare.

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

Previously graded homework exists (Claude-drafted, teacher-corrected). Use it as the eval set: "the agent agrees with the teacher's final grade X% of the time" is the key number.

## Technical choices

- **Agent framework:** Microsoft Agent Framework (`Microsoft.Agents.AI`) rather than Semantic Kernel, which is in maintenance mode since Agent Framework 1.0 (April 2026).
- **The course's Python stack, translated:** the program teaches LangChain/LangGraph. Here tools become `[AgentTool]` methods, LangGraph becomes the background agent run (`AgentRun` jobs with `wait_for_jobs`, see README), and retrievers become pgvector behind a port.
- **Not used yet:** Polly for retries and OpenTelemetry were in the original plan. Today the job queue retries failed work itself, and logging is Serilog.

## Deployment

One VM with Docker Compose (`deploy/`, described in the README), switched on during the test stage:

- **Caddy** is the only public service: HTTPS, the built React app as static files, and `/api/*` passed to the API with the prefix stripped (the same split as Vite in development).
- **The API** runs in its own container and starts sandboxes through the VM's Docker socket, so `DockerSandboxRunner` works as it does locally. A one-off `migrate` container applies migrations before it starts.
- **Postgres** runs in a container with a volume.
- **No app login** for the test stage: the teacher connects their own Google account once (web redirect flow, token on a volume), and everyone who opens the address sees that data.
- **Why not Cloud Run:** it can't run Docker inside itself (the sandboxes), and it stops or throttles containers between requests, which would break the background job worker. For one teacher, a VM is simpler and cheaper.
- **Later:** an app login with owner and viewer roles before the app is open for longer than a test window; sandboxes on a separate small VM if the API container's access to the Docker socket becomes a concern.

## Open questions

- **Automated tests vs. free naming:** task statements don't always fix class/method names. Options: fix names in future tasks (enables hidden JUnit tests), test only console input → output, or rely on LLM review. Decide per assignment type.
- **Base rubric:** write it together with the teacher — categories, point weights, policy rules.
