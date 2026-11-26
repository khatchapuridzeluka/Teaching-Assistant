# API endpoints

Every endpoint of the API with a one-line description. The source of truth is the OpenAPI document generated
from the controllers' XML comments: run the API locally and open Swagger UI at `/swagger`. The
[README](../README.md#api) groups the endpoints by area.

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/health` | API liveness check |
| `GET` | `/health/model` | Sends a test prompt to Claude; returns model, latency and reply |
| `GET` | `/auth/google/status` | Google connection state: `NotConfigured`, `NotConnected`, `Connected` or `ReconnectRequired` |
| `POST` | `/auth/google/connect` | Returns Google's consent page URL for the browser to open |
| `GET` | `/auth/google/callback` | Google redirects here after consent; stores the authorization and redirects to Settings |
| `POST` | `/auth/google/disconnect` | Revokes access at Google and deletes the stored token |
| `GET` | `/courses` | Courses the teacher teaches in Google Classroom |
| `POST` | `/courses/{courseId}/sync` | Copies the course's roster, assignments and every student's state and Classroom grade history into the database (no files, nothing written to Classroom) |
| `GET` | `/courses/{courseId}/students` | The course's students with their grade per assignment and overall score, from the database |
| `GET` | `/courses/{courseId}/students/{userId}/mistakes` | A student's mistakes (per graded assignment: your newest report when you saved one, even over a newer AI draft, otherwise the newest AI draft; each item and assignment says which (Draft, InReview, Approved)), newest assignment first, each labelled new or repeated, with counts and percentages per category, severity, label and assignment; optional `assignmentId`, `category`, `severity`, `label` filters |
| `GET` | `/courses/{courseId}/students/{userId}/strengths` | A student's strengths (per graded assignment: your newest report when you saved one, even over a newer AI draft, otherwise the newest AI draft; each item and assignment says which (Draft, InReview, Approved)), newest assignment first, each labelled new, consistent or improved, with breakdowns by category and label and strengths vs. mistakes per assignment; filters `assignmentId`, `category`, `label` |
| `GET` | `/courses/{courseId}/grading-guidance` | What the course is graded with: the course's own guidance, the built-in approach and the grading rules, each with the exact text the grader receives (read-only for now) |
| `PUT` | `/courses/{courseId}/grading-guidance/course-guidance` | Sets the course's own grading guidance (`400` when longer than 4,000 characters; an empty text removes it) and returns the course's guidance; the course's next grading uses it |
| `GET` | `/courses/{courseId}/assignments/{assignmentId}/notes` | The teacher's notes for one assignment, in order; the grader reads them only for that assignment |
| `POST` | `/courses/{courseId}/assignments/{assignmentId}/notes` | Adds a note to the assignment after its existing ones (`201`; `400` when the title or text is empty or too long) |
| `PUT` | `/assignment-notes/{id}` | Changes a note's title and text (`400` as above; `404` when missing or deleted) |
| `DELETE` | `/assignment-notes/{id}` | Marks a note deleted (`204`; `404` when missing or already deleted); the assignment's next grading leaves it out |
| `POST` | `/policy-rules` | Adds a grading rule after the existing ones (`201`; `400` when a field is empty or too long, or a deduction has no points between 0 and 100); the next grading in every course uses it |
| `PUT` | `/policy-rules/{id}` | Changes a grading rule (`400` as above; `404` when missing or deleted) |
| `DELETE` | `/policy-rules/{id}` | Marks a grading rule deleted (`204`; `404` when missing or already deleted); the next grading in every course leaves it out |
| `POST` | `/grading-principles` | Adds a built-in grading principle after the existing ones (`201`; `400` when the title or text is empty or too long); the next grading in every course uses it |
| `PUT` | `/grading-principles/{id}` | Changes a built-in grading principle's title and text (`400` when empty or too long, `404` when missing or deleted); the next grading in every course uses it |
| `DELETE` | `/grading-principles/{id}` | Marks a built-in grading principle deleted (`204`; `404` when missing or already deleted); the next grading in every course leaves it out |
| `GET` | `/courses/{courseId}/students/{userId}/overview` | A student's full overview counted in code from the same records: score per graded assignment (report, otherwise AI draft), mistakes and strengths per assignment and per category, average / first / latest score, top mistake and strength categories |
| `GET` | `/courses/{courseId}/students/{userId}/analysis?kind=Mistakes\|Strengths\|Overview` | The latest AI analysis of the student's mistakes (feedback and next steps), strengths (skill groups and overview) or full overview (overall picture and next steps); `204` when not analyzed yet |
| `POST` | `/courses/{courseId}/students/{userId}/analysis?kind=Mistakes\|Strengths\|Overview` | Queues that AI analysis from each graded assignment: your newest report when you saved one, otherwise the newest AI draft (`202`, returns the job; the same job if one is running) |
| `GET` | `/courses/{courseId}/gradebook` | Enrolled students × assignments with each cell's grade status (graded, awaiting grade, missing, not due, not submitted), per-assignment averages and counts, and the class average |
| `GET` | `/courses/{courseId}/gradebook/export` | The gradebook as a CSV file (the grade per assignment, overall %) |
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
| `POST` | `/versions/{versionId}/try` | Grades a stored version once with the chosen `model`, exactly like a draft, and returns the answer in the draft's shape without saving it; 400 for an unknown model |
| `GET` | `/grading-models` | The models *Try with model* and evaluation runs can use, and the one that grades drafts now |
| `POST` | `/versions/{versionId}/grade/queue` | Queues an AI grading after quick checks and returns the job (`202 Accepted`); the worker saves the draft. Returns the existing job if one is already queued or running |
| `GET` | `/jobs/{jobId}` | A background job: status, progress, attempts, error, and the draft a finished grading produced |
| `GET` | `/model-usage` | Claude calls of the last `days` (default 30), optionally for one `purpose`: a summary, totals per purpose (calls, failures, tokens incl. cache, averages per call, cache hit rate, average duration), tokens per day, and the 200 most recent calls with their student and assignment |
| `GET` | `/jobs` | Queue overview: counts (queued and running now, finished in the last 24 hours) and the 200 most recent jobs with their student and assignment; optional `status`, `type`, `courseId`, `assignmentId` filters |
| `POST` | `/courses/{courseId}/assignments/{assignmentId}/grade-all` | Queues a parent job with one grading per student without a draft, or per student with work when `?regrade=true` (`202`), or says why nothing was queued (`200`) |
| `GET` | `/courses/{courseId}/assignments/{assignmentId}/common-mistakes` | The latest common-mistakes analysis: groups with the AI's and your approved explanation, students affected, points and members; `204` before the first analysis |
| `POST` | `/courses/{courseId}/assignments/{assignmentId}/common-mistakes/analyze` | Queues an analysis of the assignment's draft mistakes (`202` with the job) |
| `GET` | `/courses/{courseId}/assignments/{assignmentId}/tests` | The assignment's latest test set: requirements with coverage counts, interface, cases and the settings it was made with (`204` when there is none) |
| `POST` | `/courses/{courseId}/assignments/{assignmentId}/tests/generate` | Queues one paid call that designs test cases from the stored instructions and the given settings (`202` with the job; the result is a new draft version) |
| `PUT` | `/courses/{courseId}/assignments/{assignmentId}/tests/{testSetId}` | Saves edits to the newest test set: a draft changes in place, an approved one becomes a new draft version |
| `POST` | `/courses/{courseId}/assignments/{assignmentId}/tests/{testSetId}/approve` | Approves the newest version; it becomes the one gradings will use |
| `POST` | `/courses/{courseId}/assignments/{assignmentId}/tests/{testSetId}/code/generate` | Queues one paid call that writes the JUnit test code for an approved class-design set and compiles it in the sandbox (also queued on approve) |
| `GET` | `/courses/{courseId}/assignments/{assignmentId}/tests/{testSetId}/runs` | The newest test run of each student's version: per case passed / failed / not implemented, the adapter, compiler output |
| `POST` | `/courses/{courseId}/assignments/{assignmentId}/tests/{testSetId}/runs` | Queues a test run of every student's latest stored version (`202` with how many were queued and who was skipped) |
| `POST` | `/courses/{courseId}/assignments/{assignmentId}/tests/{testSetId}/runs/{versionId}` | Queues a test run of one student's version (`202` with the job); `?newAdapter=true` writes the student's adapter again instead of reusing the stored one |
| `POST` | `/mistake-clusters/{clusterId}/approve` | Approves your explanation for a group of common mistakes |
| `POST` | `/courses/{courseId}/students/{userId}/grade-all` | Queues a *Grade student* parent job that first fetches the student's turned-in work from Classroom, then grades every assignment without a draft, or every assignment with stored work when `?regrade=true` (`202`, returns the parent job; the same job if one is already running; a retry keeps the choice) |
| `GET` | `/jobs/{jobId}/children` | The child jobs of a parent job (each student's grading under a grade-all) |
| `POST` | `/jobs/{jobId}/retry` | Queues a failed or canceled job again as a new job (a grading is checked again first) |
| `POST` | `/jobs/{jobId}/cancel` | Cancels a job (queued: at once; running: at the worker's next check) |
| `GET` | `/courses/{courseId}/assignments/{assignmentId}/jobs` | An assignment's recent jobs, newest first |
| `GET` | `/versions/{versionId}/review` | The version's current draft (newest not discarded); 204 when it has none |
| `GET` | `/versions/{versionId}/gradings` | Every saved grading of a version, drafts and discarded, newest first |
| `GET` | `/courses/{courseId}/gradings` | A course's saved drafts as list rows (assignment, student, version, score, mistake counts), newest first; `?assignmentId=` for one assignment, `?studentId=` (Google user ID) for one student across assignments (both combine), `?all=true` to include earlier and discarded drafts. Filtered and counted in the database (gradings → versions → submissions) |
| `GET` | `/courses/{courseId}/assignments/{assignmentId}/gradings` | Every saved grading of an assignment with student, version, score and mistake counts |
| `GET` | `/gradings/{gradingId}/export/pdf` | Downloads one grading as a PDF report (your report when one exists, otherwise the AI draft, with a status banner); generated on request, never stored or sent anywhere; `404` for an unknown grading |
| `POST` | `/gradings/{gradingId}/discard` | Discards a draft (it stays in the history), also one with a report: its mistake and strength records are removed, so an approved report stops counting. Optional body `{ reason, note }`: PoorGrading, WrongSubmission, NotNeeded or Other (Superseded is only set automatically), note up to 500 characters; the screens ask for the reason first and show the draft as *Discarded · reason* afterwards |
| `GET` | `/gradings/{gradingId}/report` | Your report on an AI draft: edited score, feedback and mistakes with their decision (kept / edited / rejected); `204` before the first save |
| `PUT` | `/gradings/{gradingId}/report` | Saves your edits as a report in review (created from the draft on first save; saving an approved report reopens it) |
| `POST` | `/gradings/{gradingId}/report/approve` | Saves your edits and approves the report as final |
| `GET` | `/evaluation` | How the AI drafts compare with the approved reports made from them: scores, mistakes, strengths, Classroom check, per model, per report, and the discarded drafts with why (`?courseId=` for one course); with `?model=`, the same for that model's side gradings from evaluation runs, plus which of your kept mistakes it found |
| `GET` | `/evaluation/models` | The models an evaluation run can use, the latest run's progress and, per model, how its side gradings compare with the approved reports (`?courseId=` for one course) |
| `POST` | `/evaluation/runs` | Starts an evaluation run: grades every approved report's submission again with each chosen model in the background, without creating drafts, and returns the comparison with the run's progress |
| `GET` | `/review-queue` | Across all courses: current drafts waiting for review (oldest first, flagged when a newer version exists) and turned-in submissions whose latest version has no draft |
| `GET` | `/versions/{versionId}/code/file?path=…` | One extracted file (source code as UTF-8 text); paths outside the version's code folder are refused |
| `POST` | `/agent/chat` | Chat with the assistant: sends the conversation so far with the teacher's new message and returns the whole reply (Markdown); long histories are trimmed to the most recent messages. The assistant can call its tools (list courses, list assignments, fetch submissions, list stored submissions); optional `courseId` and `assignmentId` tell it which page the teacher has open |
| `POST` | `/agent/runs` | Start a background agent run: stores the teacher's message (in a new conversation, or the one named by `conversationId`), queues the run and returns it at once (`202`) |
| `GET` | `/agent/runs/{runId}` | An agent run: status, final reply or error, and its messages so far, tool calls and tool results included |
| `POST` | `/agent/runs/{runId}/cancel` | Cancel an agent run; jobs it already started keep running |
| `GET` | `/agent/conversations/{conversationId}` | A stored conversation: all messages in order and the run still in progress, if any |

Errors are returned as [RFC 9457 Problem Details](https://www.rfc-editor.org/rfc/rfc9457). The full, documented specification is available at `/swagger/v1/swagger.json`.
