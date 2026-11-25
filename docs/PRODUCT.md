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

## Automated tests

Built so far: **generating** a draft test set (the form, the background job and the draft on the assignment's Tests view) **editing and approving** it, and **test code**: on approval one call turns the cases into a JUnit class against the interface (with a placeholder adapter so the sandbox can compile it at once), shown to the teacher with the compile result, and **test runs**: an adapter per student version (a cheaper model; reused while the interface is unchanged; one repair attempt when it does not compile), the student's code, the adapter and the shared tests compiled and run in the sandbox, results per case in a students × cases matrix, and **results in grading** (below). The layout is mocked up in [design/tests-tab-mockup.html](design/tests-tab-mockup.html).

**The idea: what is tested is the same for every student; how it reaches each student's code is per submission.** Students name classes and methods freely, so tests can't call their code directly. Instead:

- **Console tasks** (a program that reads input and prints output): the sandbox runs the student's `main` with each test's input on stdin and compares stdout leniently (spacing, case and extra labels such as "Result:" don't count, as in the grading policy). Names never matter.
- **Class-design tasks**: the tests are written once against a **neutral interface** that belongs to the test set (`interface Account { void deposit(int); void withdraw(int); int balance(); }`). For each submission, a small **adapter** maps that interface onto the student's own classes (`deposit` → `Wallet.addMoney`). Every student meets exactly the same cases and expected results; only the adapter differs.
- **Fixed names** stay an option per assignment: a task that names its classes and methods needs no adapter and can use plain hidden JUnit tests.

**Expected results never come from the model's guess.** They come from running the teacher's reference solution through the same tests in the sandbox, or the teacher writes or confirms them while reviewing.

**What is made when, and what it costs:**

| Part | When | Paid model calls |
|---|---|---|
| Test set (requirements, interface, cases) | once per assignment, when the teacher asks | one call (Opus), plus one per "Ask Claude for more" |
| Adapter | once per submission version, the first time tests run for it; stored with the version | one small call (a cheaper model) per new version, or when the interface changes |
| Running the tests | whenever needed: during grading, on "Run tests", after an edit | none (sandbox only) |

A regrade reuses the stored adapter, and editing cases reruns all adapters for free. Tests are never written fresh inside each grading: that would cost more every time and measure students against different checks.

**The generation request** (the "Create test cases" form): test style (console or class design); what to cover (normal cases, boundaries, invalid input and exceptions, large inputs, concurrency); how many (about 3 per category, a total, or as few as cover every requirement); a free-text focus; "don't test" (prefilled from the grading policy: wording of messages, names, input prompts); and an optional reference solution. The model also gets the stored instructions and the rubric layers, and answers as structured output:

1. the **requirements** it found (R1, R2, …);
2. the **neutral interface** (class-design style);
3. the **test cases**: id, description, category, the requirement it checks, steps (an input, or a sequence of calls), and the expected result or "from the reference solution";
4. what it **couldn't test**, and why (for example clean code, which the grader judges from the code).

Each case checks behaviour through the method that answers it directly (after an add: the size and the value at the promised position; after a remove: the returned value, the size and the neighbours), one value per check. The whole object as text (`toString`) is checked only in cases for a requirement about that format, so one formatting habit cannot fail every case and hide whether the rest works. The test code groups a case's checks in `assertAll`, so every failed check is reported. Simple cases are stored as structured data the teacher can read and edit without Java. Cases that need real code (threads, timing, long call sequences) come later as JUnit the model writes and the teacher sees.

**Coverage**, checked by the system rather than claimed by the model: every requirement needs at least one test (gaps show in red while reviewing); later, code coverage of the reference solution with JaCoCo in the sandbox, so an unreached branch points at a missing case.

**Adapter guardrails:** an adapter that can't map something says "not implemented" instead of guessing (a missing feature, not a crash); an adapter that doesn't compile is a "harness problem" and never counts as a failed test; the teacher can see each adapter next to the results.

**The Tests tab** (its own tab on the assignment page, next to Submissions, Board and Instructions):

1. **No tests yet:** an explanation, **Create test cases**, and an option to write cases by hand.
2. **Create form:** the request above, with a note that it is one paid call; it runs as a background job like gradings.
3. **Draft to review** (fully editable, not used in grading until approved): requirements with coverage, the interface in the code editor, and the cases table with inline editing, add, duplicate and delete. **Run on reference solution** fills in expected results and flags any case the reference fails (the case or the reference is wrong). **Ask Claude for more** adds cases marked *proposed*, which the teacher keeps or removes; the teacher's edits are never overwritten. **Approve** switches the tests on for the assignment.
4. **Approved:** a students × cases matrix (passed, failed, not implemented, harness problem) with summary numbers and the hardest case; a cell opens the failure in plain words and that student's adapter. **Run tests on all submissions** reruns them. Editing an approved set makes a **new version**, like instructions, and every grading records which test version it used.

**In grading:** when an assignment has an approved test set, grading compiles the submission, runs the tests (or reuses that version's stored run) and puts the results in the prompt's own `<automated_tests>` section after the compile and style checks; the draft's sent list records the test version and how many cases passed. Claude still writes the review and the score; a failed test becomes evidence ("withdraw allows a negative balance: T4"). Assignments without approved tests grade as they do today.

**Per-submission exploration** (later): an agent tool that tries extra inputs on one submission when the teacher suspects something. Its findings are evidence for the grader, never pass or fail, because the model picks both the inputs and what counts as correct.

**Build order:** console tests first (no adapter); then the interface and adapters for class-design tasks; then JaCoCo coverage, code-based cases and concurrency.

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

- **Base rubric:** write it together with the teacher — categories, point weights, policy rules.
