# Data model

The PostgreSQL schema behind the app: 36 tables, managed by EF Core 10 migrations in `src/TeachingAssistant.Infrastructure/Persistence/` (one configuration class per table in `Configurations/`). The diagrams are generated from the migrations' model snapshot, so names and keys match the database.

How to read them:

- **Solid lines** are foreign keys enforced by the database; **dashed lines** are references by id that the code keeps without a constraint, mostly the Google Classroom ids (`course_id`, `assignment_id`, `student_id` are Classroom's own ids, stored as text).
- `PK` primary key, `FK` foreign key, `UK` unique. Courses have no table: the course list comes live from Google Classroom, and tables keep its `course_id`.
- Columns typed `jsonb` hold structures that are read and written whole (job payloads, test set contents, agent tool calls).

## The whole model

Every table and how they connect; each line is labelled with the column that holds the reference. The sections below show each area with all its columns.

```mermaid
erDiagram
    agent_conversations
    agent_messages
    agent_runs
    gradings
    assignment_notes
    mistake_cluster_members
    course_guidance
    evaluation_runs
    evaluation_run_results
    grade_events
    grading_mistakes
    grading_principles
    grading_reports
    grading_strengths
    mistake_analyses
    mistake_clusters
    policy_rules
    report_mistakes
    report_strengths
    assignment_instructions
    course_assignments
    instructions_contents
    instructions_versions
    submissions
    submission_versions
    version_contents
    jobs
    enrollments
    student_strength_groups
    students
    student_analyses
    student_mistakes
    student_strengths
    test_runs
    test_sets
    model_calls
    agent_conversations ||--o{ agent_messages : "conversation_id"
    jobs |o--o{ agent_messages : "job_id"
    agent_runs ||--o{ agent_messages : "run_id"
    agent_conversations ||--o{ agent_runs : "conversation_id"
    jobs |o--o{ agent_runs : "job_id"
    submission_versions ||--o{ gradings : "version_id"
    mistake_clusters ||--o{ mistake_cluster_members : "cluster_id"
    evaluation_runs ||--o{ evaluation_run_results : "run_id"
    submissions ||--o{ grade_events : "submission_id"
    gradings ||--o{ grading_mistakes : "grading_id"
    gradings ||--o| grading_reports : "grading_id"
    gradings ||--o{ grading_strengths : "grading_id"
    mistake_analyses ||--o{ mistake_clusters : "analysis_id"
    grading_reports ||--o{ report_mistakes : "report_id"
    grading_mistakes |o--o{ report_mistakes : "source_mistake_id"
    grading_reports ||--o{ report_strengths : "report_id"
    grading_strengths |o--o{ report_strengths : "source_strength_id"
    instructions_versions ||--o{ instructions_contents : "version_id"
    assignment_instructions ||--o{ instructions_versions : "instructions_id"
    submissions ||--o{ submission_versions : "submission_id"
    submission_versions ||--o{ version_contents : "version_id"
    jobs |o--o{ jobs : "parent_id"
    students ||--o{ enrollments : "student_id"
    student_analyses ||--o{ student_strength_groups : "analysis_id"
    grading_reports ||--o{ student_mistakes : "report_id"
    report_mistakes ||--o| student_mistakes : "report_mistake_id"
    grading_reports ||--o{ student_strengths : "report_id"
    report_strengths ||--o| student_strengths : "report_strength_id"
    test_sets ||--o{ test_runs : "test_set_id"
    students |o..o{ submissions : "student_id"
    course_assignments |o..o{ submissions : "assignment_id"
    course_assignments |o..o{ assignment_instructions : "assignment_id"
    course_assignments |o..o{ test_sets : "assignment_id"
    submission_versions |o..o{ test_runs : "version_id"
    course_assignments |o..o{ mistake_analyses : "assignment_id"
    grading_mistakes |o..o{ mistake_cluster_members : "grading_mistake_id"
    students |o..o{ student_analyses : "student_id"
    course_assignments |o..o{ assignment_notes : "assignment_id"
    grading_reports |o..o{ evaluation_run_results : "report_id"
    submission_versions |o..o{ model_calls : "version_id"
```

## Ingestion

Submissions and assignment instructions fetched from Google Classroom (or GitHub), each change stored as a new version with its files. Tables from other areas are shown with their keys only.

```mermaid
erDiagram
    course_assignments {
        uuid id PK
        text course_id
        timestamptz due_at
        text external_assignment_id
        float max_points
        text source
        timestamptz synced_at
        text title
    }
    submissions {
        uuid id PK
        float assigned_grade
        text assignment_id "id of course_assignments.external_assignment_id"
        text course_id
        timestamptz created_at
        text external_submission_id
        bool late
        text source
        text state
        timestamptz status_synced_at
        text storage_folder
        text student_id "id of students.external_user_id"
        text student_name
    }
    submission_versions {
        uuid id PK
        text content_hash
        text extraction_detail
        timestamptz fetched_at
        int number
        text status
        text status_detail
        text storage_path
        uuid submission_id FK
        timestamptz turned_in_at
    }
    version_contents {
        uuid id PK
        text external_id
        text fingerprint
        text kind
        text llm_ready_name
        text mime_type
        text name
        text origin
        text ref
        bigint size_bytes
        text status
        text stored_name
        uuid version_id FK
    }
    grade_events {
        uuid id PK
        timestamptz graded_at
        text kind
        float max_points
        float points_earned
        uuid submission_id FK
    }
    assignment_instructions {
        uuid id PK
        text assignment_id "id of course_assignments.external_assignment_id"
        text course_id
        timestamptz created_at
        text source
        text storage_folder
    }
    instructions_versions {
        uuid id PK
        text description
        timestamptz fetched_at
        uuid instructions_id FK
        float max_points
        int number
        text storage_path
        text title
    }
    instructions_contents {
        uuid id PK
        text external_id
        text fingerprint
        text mime_type
        text name
        text origin
        bigint size_bytes
        text status
        text stored_name
        uuid version_id FK
    }
    students {
        uuid id PK
        text external_user_id
    }
    submissions ||--o{ grade_events : "submission_id"
    instructions_versions ||--o{ instructions_contents : "version_id"
    assignment_instructions ||--o{ instructions_versions : "instructions_id"
    submissions ||--o{ submission_versions : "submission_id"
    submission_versions ||--o{ version_contents : "version_id"
    students |o..o{ submissions : "student_id"
    course_assignments |o..o{ submissions : "assignment_id"
    course_assignments |o..o{ assignment_instructions : "assignment_id"
```

## Grading and review

The AI's draft (`gradings` with its mistakes and strengths) and the teacher's report made from it. A report row keeps each AI mistake the teacher kept, edited or rejected (`decision`, `source_mistake_id`), so the evaluation can compare the two. Tables from other areas are shown with their keys only.

```mermaid
erDiagram
    gradings {
        uuid id PK
        float bonus
        text build_output
        text build_status
        timestamptz created_at
        varchar discard_note
        text discard_reason
        timestamptz discarded_at
        int instructions_version
        float max_score
        text model
        int pmd_findings
        text pmd_output
        text raw_output
        text review
        float score
        text status
        int style_findings
        text style_output
        uuid version_id FK
    }
    grading_mistakes {
        uuid id PK
        text category
        int end_line
        text file
        uuid grading_id FK
        text message
        float points
        int position
        text severity
        int start_line
    }
    grading_strengths {
        uuid id PK
        text category
        int end_line
        text file
        uuid grading_id FK
        text message
        int position
        int start_line
    }
    grading_reports {
        uuid id PK
        timestamptz approved_at
        text assignment_id
        float bonus
        text course_id
        timestamptz created_at
        uuid grading_id FK, UK
        text review
        float score
        text status
        text student_id
        timestamptz updated_at
        uuid version_id
    }
    report_mistakes {
        uuid id PK
        text category
        text decision
        int end_line
        text file
        text message
        float points
        int position
        uuid report_id FK
        text severity
        uuid source_mistake_id FK
        int start_line
    }
    report_strengths {
        uuid id PK
        text category
        text decision
        int end_line
        text file
        text message
        int position
        uuid report_id FK
        uuid source_strength_id FK
        int start_line
    }
    submission_versions {
        uuid id PK
        uuid submission_id FK
    }
    submission_versions ||--o{ gradings : "version_id"
    gradings ||--o{ grading_mistakes : "grading_id"
    gradings ||--o| grading_reports : "grading_id"
    gradings ||--o{ grading_strengths : "grading_id"
    grading_reports ||--o{ report_mistakes : "report_id"
    grading_mistakes |o--o{ report_mistakes : "source_mistake_id"
    grading_reports ||--o{ report_strengths : "report_id"
    grading_strengths |o--o{ report_strengths : "source_strength_id"
```

## Rubric layers

The teacher's grading rules, layered into every grading prompt: principles for all courses, policy rules, guidance per course and notes per assignment. Deleting one only marks it (`is_deleted`), so earlier gradings stay explainable. Tables from other areas are shown with their keys only.

```mermaid
erDiagram
    grading_principles {
        uuid id PK
        timestamptz created_at
        varchar detail
        bool is_deleted
        int position
        varchar title
        timestamptz updated_at
    }
    policy_rules {
        uuid id PK
        varchar covers
        timestamptz created_at
        varchar detail
        varchar effect
        bool is_deleted
        float points
        varchar points_per
        int position
        varchar title
        timestamptz updated_at
    }
    course_guidance {
        text course_id PK
        varchar text
        timestamptz updated_at
    }
    assignment_notes {
        uuid id PK
        text assignment_id "id of course_assignments.external_assignment_id"
        text course_id
        timestamptz created_at
        varchar detail
        bool is_deleted
        int position
        varchar title
        timestamptz updated_at
    }
    course_assignments {
        uuid id PK
        text external_assignment_id
    }
    course_assignments |o..o{ assignment_notes : "assignment_id"
```

## Student profiles

Approving a report copies its kept mistakes and strengths into the student's history (one row per report item, by the unique `report_mistake_id`); the AI analyses of a student read that history. Tables from other areas are shown with their keys only.

```mermaid
erDiagram
    students {
        uuid id PK
        timestamptz created_at
        text external_user_id
        text name
        text photo_content_type
        timestamptz photo_fetched_at
        text photo_hash
        text photo_stored_name
        text photo_url
        text source
    }
    enrollments {
        uuid id PK
        bool active
        text course_id
        timestamptz enrolled_at
        timestamptz last_seen_at
        uuid student_id FK
    }
    student_mistakes {
        uuid id PK
        timestamptz approved_at
        text assignment_id
        text category
        text course_id
        text decision
        int end_line
        text file
        uuid grading_id
        text message
        float points
        uuid report_id FK
        uuid report_mistake_id FK, UK
        text severity
        uuid source_mistake_id
        int start_line
        text student_id
        uuid submission_id
        uuid version_id
    }
    student_strengths {
        uuid id PK
        timestamptz approved_at
        text assignment_id
        text category
        text course_id
        text decision
        int end_line
        text file
        uuid grading_id
        text message
        uuid report_id FK
        uuid report_strength_id FK, UK
        uuid source_strength_id
        int start_line
        text student_id
        uuid submission_id
        uuid version_id
    }
    student_analyses {
        uuid id PK
        int assignments
        text course_id
        timestamptz created_at
        text kind
        text model
        text next_steps
        int records
        timestamptz records_approved_until
        text student_id "id of students.external_user_id"
        text summary
    }
    student_strength_groups {
        uuid id PK
        uuid analysis_id FK
        text explanation
        int position
        text title
        bool ungrouped
    }
    grading_reports {
        uuid id PK
        uuid grading_id FK, UK
    }
    report_mistakes {
        uuid id PK
        uuid report_id FK
        uuid source_mistake_id FK
    }
    report_strengths {
        uuid id PK
        uuid report_id FK
        uuid source_strength_id FK
    }
    grading_reports ||--o{ report_mistakes : "report_id"
    grading_reports ||--o{ report_strengths : "report_id"
    students ||--o{ enrollments : "student_id"
    student_analyses ||--o{ student_strength_groups : "analysis_id"
    grading_reports ||--o{ student_mistakes : "report_id"
    report_mistakes ||--o| student_mistakes : "report_mistake_id"
    grading_reports ||--o{ student_strengths : "report_id"
    report_strengths ||--o| student_strengths : "report_strength_id"
    students |o..o{ student_analyses : "student_id"
```

## Common mistakes

The class-wide analysis of one assignment: groups of similar mistakes from the students' current drafts, each with an explanation the teacher can approve. Tables from other areas are shown with their keys only.

```mermaid
erDiagram
    mistake_analyses {
        uuid id PK
        text assignment_id "id of course_assignments.external_assignment_id"
        text course_id
        timestamptz created_at
        int mistakes
        text model
        text next_lecture
        int students
        text summary
    }
    mistake_clusters {
        uuid id PK
        uuid analysis_id FK
        timestamptz approved_at
        text approved_explanation
        text explanation
        int position
        text title
        bool ungrouped
    }
    mistake_cluster_members {
        uuid id PK
        text category
        uuid cluster_id FK
        int end_line
        text file
        uuid grading_mistake_id "id of grading_mistakes.id"
        text message
        float points
        text severity
        int start_line
        text student_id
        text student_name
        text submission_id
        uuid version_id
    }
    course_assignments {
        uuid id PK
        text external_assignment_id
    }
    grading_mistakes {
        uuid id PK
        uuid grading_id FK
    }
    mistake_clusters ||--o{ mistake_cluster_members : "cluster_id"
    mistake_analyses ||--o{ mistake_clusters : "analysis_id"
    course_assignments |o..o{ mistake_analyses : "assignment_id"
    grading_mistakes |o..o{ mistake_cluster_members : "grading_mistake_id"
```

## Automated tests

Test sets are versioned per assignment (draft, then approved); the requirements, cases, interface and JUnit code are one JSON document (`content`). A run stores one student version's results and the adapter it used, so the next run can reuse it. Tables from other areas are shown with their keys only.

```mermaid
erDiagram
    test_sets {
        uuid id PK
        text assignment_id "id of course_assignments.external_assignment_id"
        jsonb content
        text course_id
        timestamptz created_at
        text model
        jsonb request
        text status
        text style
        int version
    }
    test_runs {
        uuid id PK
        text adapter
        text adapter_model
        text assignment_id
        text course_id
        timestamptz created_at
        text interface
        text output
        jsonb results
        text status
        text student_id
        uuid test_set_id FK
        int test_set_version
        uuid version_id "id of submission_versions.id"
    }
    course_assignments {
        uuid id PK
        text external_assignment_id
    }
    submission_versions {
        uuid id PK
        uuid submission_id FK
    }
    test_sets ||--o{ test_runs : "test_set_id"
    course_assignments |o..o{ test_sets : "assignment_id"
    submission_versions |o..o{ test_runs : "version_id"
```

## Jobs and the agent

The background job queue (claimed with `FOR UPDATE SKIP LOCKED`, deduplicated by `dedup_key`, children under a parent for Grade all) and the stored agent conversations, runs and messages.

```mermaid
erDiagram
    jobs {
        uuid id PK
        text assignment_id
        int attempts
        bool cancel_requested
        text course_id
        timestamptz created_at
        text dedup_key UK
        text error
        timestamptz finished_at
        text locked_by
        timestamptz locked_until
        int max_attempts
        uuid parent_id FK
        jsonb payload
        int progress_done
        int progress_total
        jsonb result
        timestamptz run_after
        timestamptz started_at
        text status
        text type
    }
    agent_conversations {
        uuid id PK
        timestamptz created_at
        timestamptz last_activity_at
        int message_count
    }
    agent_runs {
        uuid id PK
        text assignment_id
        uuid conversation_id FK
        text course_id
        timestamptz created_at
        text error
        timestamptz finished_at
        uuid job_id FK
        int max_steps
        text page
        text reply
        timestamptz started_at
        text status
        int steps
        timestamptz wait_until
    }
    agent_messages {
        uuid id PK
        uuid conversation_id FK
        timestamptz created_at
        uuid job_id FK
        jsonb links
        text role
        uuid run_id FK
        int sequence
        text text
        text tool_call_id
        jsonb tool_calls
        text tool_name
    }
    agent_conversations ||--o{ agent_messages : "conversation_id"
    jobs |o--o{ agent_messages : "job_id"
    agent_runs ||--o{ agent_messages : "run_id"
    agent_conversations ||--o{ agent_runs : "conversation_id"
    jobs |o--o{ agent_runs : "job_id"
    jobs |o--o{ jobs : "parent_id"
```

## Evaluation and usage

Evaluation runs grade approved reports' submissions again with other models, without creating drafts; every model call is logged with its purpose, tokens and duration. Tables from other areas are shown with their keys only.

```mermaid
erDiagram
    evaluation_runs {
        uuid id PK
        text course_id
        timestamptz created_at
    }
    evaluation_run_results {
        uuid id PK
        text answered_model
        float bonus
        text build_status
        timestamptz completed_at
        timestamptz created_at
        int duration_ms
        varchar error
        float max_score
        jsonb mistakes
        text model
        uuid report_id "id of grading_reports.id"
        uuid run_id FK
        float score
        text status
        uuid version_id
    }
    model_calls {
        uuid id PK
        text assignment_id
        bigint cache_read_tokens
        bigint cache_write_tokens
        text course_id
        timestamptz created_at
        bigint duration_ms
        varchar error
        bigint input_tokens
        text model
        bigint output_tokens
        text purpose
        text requested_model
        bool succeeded
        uuid version_id "id of submission_versions.id"
    }
    grading_reports {
        uuid id PK
        uuid grading_id FK, UK
    }
    submission_versions {
        uuid id PK
        uuid submission_id FK
    }
    evaluation_runs ||--o{ evaluation_run_results : "run_id"
    grading_reports |o..o{ evaluation_run_results : "report_id"
    submission_versions |o..o{ model_calls : "version_id"
```
