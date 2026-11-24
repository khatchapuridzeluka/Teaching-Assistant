import { type KeyboardEvent, useEffect, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useParams, useSearchParams } from "react-router";
import type {
  AssignmentGrading,
  ExtractionSummary,
  FetchProblem,
  FetchSummary,
  GradeAllResult,
  JobInfo,
  StoredSubmission,
  StoredVersion,
  Submission,
} from "../api/client";
import {
  useAssignments,
  useFetchSubmissions,
  useAssignmentJobs,
  useCancelJob,
  useGradeAll,
  useQueueGrading,
  useReExtract,
  useAssignmentGradings,
  useStoredSubmissions,
  useSubmissions,
} from "../api/hooks";
import { ConfirmDialog } from "../components/ConfirmDialog";
import { AssignmentTests } from "../components/AssignmentTests";
import { InstructionsBar } from "../components/InstructionsCard";
import { useTrackJob } from "../components/JobToasts";
import { PreviewPanel } from "../components/PreviewPanel";
import { SubmissionBoard } from "../components/SubmissionBoard";
import { hasWork, stateChip, SubmissionDetail } from "../components/SubmissionDetail";
import { Avatar, Button, Card, Chip, CountTabs, ErrorPanel, Menu, Modal, Notice, PageHeader, SearchField, Segmented, SkeletonList, Spinner, Stat, StatStrip } from "../components/ui";
import { formatDate, formatDay, initials } from "../format";
import { severityColor } from "../theme";
import { CircleCheck, CircleDashed, CloudDownload, Ellipsis, ExternalLink, Files, FlaskConical, LayoutList, PackageOpen, Send, Sparkles, SquareKanban, Undo2, UserX } from "lucide-react";

export function AssignmentPage() {
  const { courseId = "", assignmentId = "" } = useParams();
  const assignment = useAssignments(courseId).data?.find((a) => a.id === assignmentId);
  const submissions = useSubmissions(courseId, assignmentId);
  const stored = useStoredSubmissions(courseId, assignmentId);
  const fetch = useFetchSubmissions(courseId, assignmentId);
  const extract = useReExtract(courseId, assignmentId);
  const queue = useQueueGrading(courseId, assignmentId);
  const cancel = useCancelJob(courseId, assignmentId);
  const gradeAll = useGradeAll(courseId, assignmentId);
  const [confirmGradeAll, setConfirmGradeAll] = useState(false);
  const [regradeDrafted, setRegradeDrafted] = useState(false);

  function openGradeAll() {
    setRegradeDrafted(false);
    setConfirmGradeAll(true);
  }

  function startGradeAll() {
    setConfirmGradeAll(false);
    gradeAll.mutate(regradeDrafted, {
      onSuccess: (result) => {
        if (result.job) {
          trackJob(result.job, {
            label: `${assignment?.title ?? "Assignment"} · ${result.queued} ${result.queued === 1 ? "student" : "students"}`,
            to: `/courses/${courseId}/assignments/${assignmentId}`,
          });
        }
      },
    });
  }
  const jobs = useAssignmentJobs(courseId, assignmentId);
  const queryClient = useQueryClient();
  const trackJob = useTrackJob();
  const [previewing, setPreviewing] = useState<{ submission: StoredSubmission; versionId: string } | null>(null);
  const [sort, setSort] = useState<SortOption>("status");
  const [filter, setFilter] = useState<Filter>("all");
  const [query, setQuery] = useState("");
  const [pickedVersions, setPickedVersions] = useState<Record<string, string>>({});
  const [searchParams, setSearchParams] = useSearchParams();
  const [showDetail, setShowDetail] = useState(false);
  const requested = searchParams.get("view");
  const view = requested === "board" || requested === "tests" ? requested : "list";

  function setView(next: "list" | "board" | "tests") {
    setSearchParams((params) => {
      if (next !== "list") {
        params.set("view", next);
      } else {
        params.delete("view");
      }
      return params;
    }, { replace: true });
  }

  const latestJob = new Map<string, JobInfo>();
  for (const job of jobs.data ?? []) {
    if (job.versionId && !latestJob.has(job.versionId)) {
      latestJob.set(job.versionId, job);
    }
  }
  const activeJobs = [...latestJob.values()].filter((job) => job.status === "Queued" || job.status === "Running");
  const gradingVersionIds = new Map(activeJobs.map((job) => [job.versionId!, job.status]));

  const activeIds = useRef(new Set<string>());
  useEffect(() => {
    const now = new Set(activeJobs.map((job) => job.id));
    const finished = [...activeIds.current].some((id) => !now.has(id));
    activeIds.current = now;
    if (finished) {
      void queryClient.invalidateQueries({ queryKey: ["reviews"] });
      void queryClient.invalidateQueries({ predicate: (query) => query.queryKey.includes("gradings") });
    }
  });

  const storedById = new Map(stored.data?.map((s) => [s.externalSubmissionId, s]));
  const pickedVersion = (s: StoredSubmission | undefined) => s?.versions.find((v) => v.id === pickedVersions[s.id]) ?? s?.versions[0];

  const all = submissions.data ?? [];
  const versionOf = new Map(all.map((s) => [s.id, pickedVersion(storedById.get(s.id))]));
  const gradings = useAssignmentGradings(courseId, assignmentId);
  const reviews = new Map((gradings.data ?? []).filter((g) => g.current).map((g) => [g.versionId, g]));
  const [draftsOpen, setDraftsOpen] = useState(false);

  const counts: Record<Filter, number> = {
    all: all.length,
    returned: all.filter(filters.returned.matches).length,
    turnedIn: all.filter(filters.turnedIn.matches).length,
    missing: all.filter(filters.missing.matches).length,
  };
  const search = query.trim().toLowerCase();
  const rows = sortSubmissions(
    all.filter((s) => filters[filter].matches(s) && s.studentName.toLowerCase().includes(search)),
    sortOptions[sort],
  );

  const selected = all.find((s) => s.id === searchParams.get("student")) ?? rows[0];
  const selectedStored = selected && storedById.get(selected.id);
  const selectedVersion = selected && versionOf.get(selected.id);

  function select(submission: Submission, open = true) {
    setSearchParams((params) => {
      params.set("student", submission.id);
      return params;
    }, { replace: true });
    if (open) {
      setShowDetail(true);
    }
  }

  const withWork = all.filter(hasWork);
  const drafts = withWork.filter((s) => reviews.get(versionOf.get(s.id)?.id ?? "")).length;
  const gradeAllCount = regradeDrafted ? withWork.length : withWork.length - drafts;
  const pastDue = assignment?.dueAt != null && new Date(assignment.dueAt).getTime() < Date.now();

  return (
    <div className="flex flex-col gap-3 lg:h-[calc(100dvh-72px)] lg:min-h-[640px]">
      <PageHeader
        title={assignment?.title ?? "Assignment"}
        subtitle={assignment && `Due ${formatDate(assignment.dueAt)} · ${assignment.maxPoints ?? "–"} points · read-only from Classroom`}
        actions={
          <>
            <Segmented
              label="View"
              value={view}
              onChange={setView}
              options={[
                {
                  value: "list",
                  label: (
                    <span className="flex items-center gap-1.5">
                      <LayoutList className="h-3.5 w-3.5" /> List
                    </span>
                  ),
                },
                {
                  value: "board",
                  label: (
                    <span className="flex items-center gap-1.5">
                      <SquareKanban className="h-3.5 w-3.5" /> Board
                    </span>
                  ),
                },
                {
                  value: "tests",
                  label: (
                    <span className="flex items-center gap-1.5">
                      <FlaskConical className="h-3.5 w-3.5" /> Tests
                    </span>
                  ),
                },
              ]}
            />
            <Menu
              label="More actions"
              trigger={<Ellipsis className="h-4 w-4" />}
              items={[
                ...((gradings.data?.length ?? 0) > 0
                  ? [
                      {
                        label: `Saved drafts (${gradings.data!.filter((g) => g.status === "Draft").length})`,
                        hint: "Every AI draft saved for this assignment",
                        icon: <Files />,
                        onClick: () => setDraftsOpen(true),
                      },
                    ]
                  : []),
                ...((stored.data?.length ?? 0) > 0
                  ? [
                      {
                        label: extract.isPending ? "Extracting…" : "Re-extract code",
                        hint: "Rebuild the extracted code from the stored files; nothing is downloaded",
                        icon: <PackageOpen />,
                        disabled: extract.isPending || fetch.isPending,
                        onClick: () => extract.mutate(),
                      },
                    ]
                  : []),
                ...(assignment?.link
                  ? [{ label: "Open in Classroom", hint: "The assignment in Google Classroom, in a new tab", icon: <ExternalLink />, href: assignment.link }]
                  : []),
              ]}
            />
            {(stored.data?.length ?? 0) > 0 && (
              <Button
                icon={<Sparkles />}
                onClick={openGradeAll}
                disabled={gradeAll.isPending || fetch.isPending}
                title="Queue an AI grading for every student without a draft; they run in the background, 3 at a time"
              >
                {gradeAll.isPending ? "Queuing…" : "Grade all with AI"}
              </Button>
            )}
            <Button variant="primary" icon={<CloudDownload />} onClick={() => fetch.mutate()} disabled={fetch.isPending}>
              {fetch.isPending ? "Fetching…" : "Fetch from Classroom"}
            </Button>
          </>
        }
      />

      {submissions.data && (
        <StatStrip>
          <Stat compact icon={<Send />} label="Turned in" value={`${withWork.length} / ${all.length}`} note="including returned" />
          <Stat compact icon={<Undo2 />} label="Returned" value={counts.returned} note="in Classroom" />
          <Stat compact icon={<Sparkles />} label="AI drafts" value={`${drafts} / ${withWork.length}`} note="saved, waiting for review" />
          <Stat
            compact
            icon={<UserX />}
            label="Not submitted"
            value={counts.missing}
            note={assignment?.dueAt ? (pastDue ? "past due" : `due ${formatDay(assignment.dueAt)}`) : "no due date"}
          />
        </StatStrip>
      )}

      {assignment && (
        <div className="shrink-0">
          <InstructionsBar courseId={courseId} assignment={assignment} />
        </div>
      )}

      {fetch.isPending && (
        <Notice>
          <Spinner label="Fetching… downloading new and changed submissions from Google Drive." />
        </Notice>
      )}
      {fetch.isError && <ErrorPanel error={fetch.error} />}
      {fetch.data && <FetchBanner summary={fetch.data} onDismiss={() => fetch.reset()} />}
      {confirmGradeAll && (
        <ConfirmDialog
          title={`Grade all students of ${assignment?.title ?? "this assignment"}?`}
          confirmLabel={gradeAllCount > 0 ? `Grade up to ${gradeAllCount} students` : "Grade all"}
          onConfirm={startGradeAll}
          onCancel={() => setConfirmGradeAll(false)}
        >
          {regradeDrafted ? (
            <p>
              Every student who turned in work gets a new AI grading{withWork.length > 0 ? ` (up to ${withWork.length})` : ""}, including
              the {drafts} {drafts === 1 ? "student" : "students"} who already have a draft. The new draft becomes current; the earlier
              one stays in the history.
            </p>
          ) : (
            <p>
              Every student whose latest version has no draft yet gets an AI grading
              {gradeAllCount > 0 ? ` (up to ${gradeAllCount} of ${withWork.length} turned in)` : ""}. Students with a draft that is
              not discarded are skipped.
            </p>
          )}
          <label className="mt-2.5 flex items-start gap-2 rounded-lg border border-line px-3 py-2 text-[13px]">
            <input
              type="checkbox"
              checked={regradeDrafted}
              onChange={(e) => setRegradeDrafted(e.target.checked)}
              className="mt-0.5 accent-accent"
            />
            <span>
              <b className="font-semibold">Also grade the ones that already have a draft</b>
              <span className="block text-[11px] text-ink-muted">
                {drafts} {drafts === 1 ? "student has" : "students have"} a draft now; each is graded again
              </span>
            </span>
          </label>
          <p className="mt-1.5">
            Each grading is one paid AI call. They run in the background, 3 at a time; you can follow them on the System page.
          </p>
        </ConfirmDialog>
      )}
      {gradeAll.isError && <ErrorPanel error={gradeAll.error} />}
      {gradeAll.data && <GradeAllBanner result={gradeAll.data} onDismiss={() => gradeAll.reset()} />}
      {extract.isError && <ErrorPanel error={extract.error} />}
      {extract.data && <ExtractBanner summary={extract.data} onDismiss={() => extract.reset()} />}

      {submissions.isLoading && view !== "tests" && <SkeletonList rows={8} avatar />}
      {submissions.isError && <ErrorPanel error={submissions.error} />}
      {view === "tests" && (
        <div className="min-h-0 pb-20 lg:flex-1 lg:overflow-y-auto">
          <AssignmentTests courseId={courseId} assignmentId={assignmentId} assignmentTitle={assignment?.title ?? "Assignment"} />
        </div>
      )}
      {submissions.data && view === "board" && (
        <SubmissionBoard
          submissions={all}
          versionOf={versionOf}
          reviews={reviews}
          storedById={storedById}
          gradingVersionIds={gradingVersionIds}
          onOpen={(submission) => {
            setSearchParams((params) => {
              params.set("student", submission.id);
              params.delete("view");
              return params;
            }, { replace: true });
            setShowDetail(true);
          }}
        />
      )}
      {submissions.data && view === "list" && (
        <Card className="@container/workspace flex h-[calc(100dvh-96px)] min-h-[460px] shrink-0 overflow-hidden lg:h-auto lg:min-h-[420px] lg:flex-1 lg:shrink">
          <StudentList
            hidden={showDetail}
            rows={rows}
            counts={counts}
            filter={filter}
            onFilter={setFilter}
            query={query}
            onQuery={setQuery}
            sort={sort}
            onSort={setSort}
            selectedId={selected?.id}
            onSelect={select}
            versionOf={versionOf}
            reviews={reviews}
            storedById={storedById}
            gradingVersionIds={gradingVersionIds}
          />
          {selected ? (
            <div className={`min-w-0 flex-1 ${showDetail ? "flex" : "hidden @2xl/workspace:flex"}`}>
            <SubmissionDetail
              key={selectedVersion?.id ?? selected.id}
              onBack={() => setShowDetail(false)}
              submission={selected}
              stored={selectedStored}
              version={selectedVersion}
              onPickVersion={(versionId) => selectedStored && setPickedVersions((p) => ({ ...p, [selectedStored.id]: versionId }))}
              onPreview={() => selectedStored && selectedVersion && setPreviewing({ submission: selectedStored, versionId: selectedVersion.id })}
              onGrade={() => {
                if (selected && selectedVersion) {
                  queue.mutate(selectedVersion.id, {
                    onSuccess: (job) =>
                      trackJob(job, {
                        label: selected.studentName,
                        to: `/courses/${courseId}/assignments/${assignmentId}?student=${selected.id}`,
                      }),
                  });
                }
              }}
              job={selectedVersion ? latestJob.get(selectedVersion.id) : undefined}
              queuing={queue.isPending && queue.variables === selectedVersion?.id}
              queueError={queue.isError && queue.variables === selectedVersion?.id ? queue.error : null}
              onCancel={(jobId) => cancel.mutate(jobId)}
            />
            </div>
          ) : (
            <div className="hidden flex-1 items-center justify-center text-ink-muted @2xl/workspace:flex">No students in this assignment yet.</div>
          )}
        </Card>
      )}

      {previewing && (
        <PreviewPanel submission={previewing.submission} initialVersionId={previewing.versionId} onClose={() => setPreviewing(null)} />
      )}
      {draftsOpen && gradings.data && (
        <DraftsModal
          drafts={gradings.data}
          onOpen={(draft) => {
            const student = all.find((s) => s.id === draft.submissionId);
            const storedSubmission = student && storedById.get(student.id);
            if (student && storedSubmission) {
              setPickedVersions((p) => ({ ...p, [storedSubmission.id]: draft.versionId }));
              select(student);
            }
            setDraftsOpen(false);
          }}
          onClose={() => setDraftsOpen(false)}
        />
      )}
    </div>
  );
}

type SortKey = "student" | "status" | "turnedIn";
type Sort = { key: SortKey; descending: boolean };
type SortOption = "status" | "newest" | "oldest" | "name";

const sortOptions: Record<SortOption, Sort> = {
  status: { key: "status", descending: false },
  newest: { key: "turnedIn", descending: true },
  oldest: { key: "turnedIn", descending: false },
  name: { key: "student", descending: false },
};
const sortLabels: Record<SortOption, string> = { status: "Status", newest: "Newest first", oldest: "Oldest first", name: "Name" };

const statusRank: Record<string, number> = { RETURNED: 0, TURNED_IN: 1, RECLAIMED_BY_STUDENT: 2 };

function time(value: string | null | undefined): number {
  return value ? new Date(value).getTime() : 0;
}

function sortSubmissions(list: Submission[], sort: Sort): Submission[] {
  const direction = sort.descending ? -1 : 1;
  const byName = (a: Submission, b: Submission) => a.studentName.localeCompare(b.studentName);
  const byNewest = (a: Submission, b: Submission) => time(b.turnedInAt) - time(a.turnedInAt);
  const byKey: Record<SortKey, (a: Submission, b: Submission) => number> = {
    student: byName,
    status: (a, b) => (statusRank[a.state] ?? 9) - (statusRank[b.state] ?? 9),
    turnedIn: (a, b) => time(a.turnedInAt) - time(b.turnedInAt),
  };

  return [...list].sort((a, b) => {
    const missing = Number(!hasWork(a)) - Number(!hasWork(b));
    if (missing !== 0) {
      return missing;
    }
    return direction * byKey[sort.key](a, b) || byNewest(a, b) || byName(a, b);
  });
}

type Filter = "all" | "returned" | "turnedIn" | "missing";

const filters: Record<Filter, { label: string; matches: (s: Submission) => boolean }> = {
  all: { label: "All", matches: () => true },
  returned: { label: "Returned", matches: (s) => s.state === "RETURNED" },
  turnedIn: { label: "Turned in", matches: (s) => s.state === "TURNED_IN" },
  missing: { label: "Not submitted", matches: (s) => !hasWork(s) },
};

function StudentList({
  hidden,
  rows,
  counts,
  filter,
  onFilter,
  query,
  onQuery,
  sort,
  onSort,
  selectedId,
  onSelect,
  versionOf,
  reviews,
  storedById,
  gradingVersionIds,
}: {
  hidden: boolean;
  rows: Submission[];
  counts: Record<Filter, number>;
  filter: Filter;
  onFilter: (filter: Filter) => void;
  query: string;
  onQuery: (query: string) => void;
  sort: SortOption;
  onSort: (sort: SortOption) => void;
  selectedId: string | undefined;
  onSelect: (submission: Submission, open?: boolean) => void;
  versionOf: Map<string, StoredVersion | undefined>;
  reviews: Map<string, AssignmentGrading>;
  storedById: Map<string, StoredSubmission>;
  gradingVersionIds: Map<string, string>;
}) {
  function onKeyDown(e: KeyboardEvent<HTMLUListElement>) {
    if (e.key !== "ArrowDown" && e.key !== "ArrowUp") {
      return;
    }
    e.preventDefault();
    const index = rows.findIndex((s) => s.id === selectedId);
    const next = rows[Math.min(rows.length - 1, Math.max(0, index + (e.key === "ArrowDown" ? 1 : -1)))];
    if (next) {
      onSelect(next, false);
      document.getElementById(`student-${next.id}`)?.focus();
    }
  }

  return (
    <aside
      className={`w-full shrink-0 flex-col bg-white @2xl/workspace:flex @2xl/workspace:w-[270px] @2xl/workspace:border-r @2xl/workspace:border-line @4xl/workspace:w-[300px] @7xl/workspace:w-[340px] ${
        hidden ? "hidden" : "flex"
      }`}
    >
      <div className="flex shrink-0 flex-col gap-2 border-b border-line p-2.5">
        <CountTabs
          label="Filter students"
          value={filter}
          onChange={onFilter}
          options={(Object.keys(filters) as Filter[]).map((key) => ({ value: key, label: filters[key].label, count: counts[key] }))}
        />
        <div className="flex gap-2">
          <SearchField value={query} onChange={onQuery} placeholder="Search students" className="flex-1" />
          <select value={sort} onChange={(e) => onSort(e.target.value as SortOption)} aria-label="Sort students" className="field px-1.5 font-medium">
            {(Object.keys(sortLabels) as SortOption[]).map((key) => (
              <option key={key} value={key}>
                {sortLabels[key]}
              </option>
            ))}
          </select>
        </div>
      </div>

      {rows.length === 0 ? (
        <div className="px-4 py-8 text-center text-xs text-ink-muted">No students match.</div>
      ) : (
        <ul className="min-h-0 flex-1 overflow-y-auto" onKeyDown={onKeyDown} aria-label="Students">
          {rows.map((submission, i) => {
            const working = hasWork(submission);
            const version = versionOf.get(submission.id);
            const review = version && reviews.get(version.id);
            const state = stateChip[submission.state] ?? { tone: "muted" as const, label: submission.state };
            const isSelected = submission.id === selectedId;
            const previous = rows[i - 1];
            const files = submission.attachments.length;
            return (
              <li key={submission.id} className={`border-b border-paper-dark ${!working && previous && hasWork(previous) ? "border-t-2 border-t-line" : ""}`}>
                <button
                  id={`student-${submission.id}`}
                  onClick={() => onSelect(submission)}
                  aria-current={isSelected}
                  className={`flex w-full items-center gap-2.5 border-l-[3px] py-2.5 pl-3 pr-3 text-left transition-colors ${
                    isSelected ? "border-l-accent bg-accent-tint/60" : "border-l-transparent hover:bg-paper"
                  }`}
                >
                  <span className={working ? "" : "opacity-60 grayscale"}>
                    <Avatar text={initials(submission.studentName)} userId={submission.studentId} />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className={`block truncate font-semibold ${working ? "" : "text-ink-muted"}`}>{submission.studentName}</span>
                    <span className="block truncate text-[11px] text-ink-muted">
                      {working
                        ? `${formatDate(submission.turnedInAt)} · ${files} ${files === 1 ? "file" : "files"}${version && version.number > 1 ? ` · v${version.number}` : ""}`
                        : "Nothing turned in"}
                    </span>
                  </span>
                  <span className="flex shrink-0 flex-col items-end gap-1">
                    {version && gradingVersionIds.has(version.id) ? (
                      <Chip tone="info" icon={<span className="h-2.5 w-2.5 animate-spin rounded-full border-[1.5px] border-accent/30 border-t-accent" />}>
                        {gradingVersionIds.get(version.id) === "Queued" ? "Queued" : "Grading"}
                      </Chip>
                    ) : review?.reportStatus === "Approved" ? (
                      <Chip tone="success" icon={<CircleCheck />}>
                        Approved
                      </Chip>
                    ) : review?.reportStatus === "InReview" ? (
                      <Chip tone="mark">In review</Chip>
                    ) : review ? (
                      <Chip tone="warn" icon={<Sparkles />}>
                        Draft
                      </Chip>
                    ) : (
                      <Chip tone={state.tone}>{state.label}</Chip>
                    )}
                    {review ? (
                      <span className="flex items-center gap-1.5 font-mono text-[11px] text-ink-muted">
                        {review.score != null && (
                          <b className="font-semibold text-ink">
                            {points(review.score)}
                            <span className="font-normal text-ink-faint">/{review.maxScore ?? "–"}</span>
                          </b>
                        )}
                        <SeverityDots draft={review} />
                      </span>
                    ) : working && submission.late ? (
                      <Chip tone="warn">Late</Chip>
                    ) : working && !storedById.has(submission.id) ? (
                      <span className="flex items-center gap-1 text-[11px] text-ink-faint">
                        <CircleDashed className="h-3 w-3" />
                        Not fetched
                      </span>
                    ) : null}
                  </span>
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </aside>
  );
}

function FetchBanner({ summary, onDismiss }: { summary: FetchSummary; onDismiss: () => void }) {
  const instructions =
    summary.instructionsVersion == null
      ? "instructions could not be stored"
      : summary.newInstructions
        ? `instructions stored as v${summary.instructionsVersion}`
        : `instructions unchanged (v${summary.instructionsVersion})`;

  return (
    <ResultBanner
      title="Fetch finished"
      text={`${summary.submissions} checked · ${summary.newSubmissions} new · ${summary.newVersions} new versions · ${summary.unchanged} unchanged · ${summary.notSubmitted} not submitted · ${instructions}`}
      problems={summary.problems}
      onDismiss={onDismiss}
    />
  );
}

function GradeAllBanner({ result, onDismiss }: { result: GradeAllResult; onDismiss: () => void }) {
  const parts = [
    `${result.queued} queued`,
    result.alreadyDrafted > 0 ? `${result.alreadyDrafted} already have a draft` : null,
    result.alreadyQueued > 0 ? `${result.alreadyQueued} already queued` : null,
    result.notFetched > 0 ? `${result.notFetched} turned in but not fetched yet (Fetch from Classroom first)` : null,
    result.noWork > 0 ? `${result.noWork} not submitted` : null,
  ].filter(Boolean);
  const problems = result.problems.map((problem) => {
    const split = problem.indexOf(": ");
    return split > 0
      ? { studentName: problem.slice(0, split), detail: problem.slice(split + 2) }
      : { studentName: "", detail: problem };
  });

  return (
    <ResultBanner
      title={result.job ? "Grade all queued" : "Nothing to grade"}
      text={`${parts.join(" · ")} · ${result.students} students in total${result.job ? " · follow it on the System page" : ""}`}
      problems={problems as FetchProblem[]}
      onDismiss={onDismiss}
    />
  );
}

function ExtractBanner({ summary, onDismiss }: { summary: ExtractionSummary; onDismiss: () => void }) {
  return (
    <ResultBanner
      title="Extraction finished"
      text={`${summary.versionsWithCode} of ${summary.versions} stored versions have code (the others hold only images or documents)`}
      problems={summary.problems}
      onDismiss={onDismiss}
    />
  );
}

function ResultBanner({ title, text, problems, onDismiss }: { title: string; text: string; problems: FetchProblem[]; onDismiss: () => void }) {
  const [open, setOpen] = useState(false);

  return (
    <Notice
      tone={problems.length > 0 ? "warn" : "success"}
      title={title}
      onDismiss={onDismiss}
      action={
        problems.length > 0 && (
          <button onClick={() => setOpen(!open)} aria-expanded={open} className="shrink-0 font-semibold text-warn-ink underline">
            {problems.length} need{problems.length === 1 ? "s" : ""} your attention {open ? "▴" : "▾"}
          </button>
        )
      }
    >
      {text}
      {open && (
        <ul className="mt-2 flex max-h-[140px] flex-col gap-1 overflow-y-auto border-t border-warn/20 pt-2">
          {problems.map((problem, i) => (
            <li key={i} className="flex gap-2">
              <b className="shrink-0 text-ink">{problem.studentName}</b>
              <span>{problem.detail}</span>
            </li>
          ))}
        </ul>
      )}
    </Notice>
  );
}

function points(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(1);
}

function mistakeCount(draft: AssignmentGrading): number {
  return draft.major + draft.minor + draft.style;
}

function DraftsModal({
  drafts,
  onOpen,
  onClose,
}: {
  drafts: AssignmentGrading[];
  onOpen: (draft: AssignmentGrading) => void;
  onClose: () => void;
}) {
  const [showAll, setShowAll] = useState(false);
  const shown = showAll ? drafts : drafts.filter((d) => d.current);

  return (
    <Modal
      title="Saved drafts"
      subtitle="AI gradings saved for your review. Click one to open it next to the code."
      size="max-w-[920px]"
      onClose={onClose}
      headerExtra={
        <Segmented
          label="Which drafts"
          value={showAll ? "all" : "current"}
          onChange={(value) => setShowAll(value === "all")}
          options={[
            { value: "current", label: "Current", count: drafts.filter((d) => d.current).length },
            { value: "all", label: "All", count: drafts.length },
          ]}
        />
      }
    >
      {shown.length === 0 ? (
        <div className="px-5 py-10 text-center text-ink-muted">No current drafts; every draft was discarded.</div>
      ) : (
        <table className="w-full border-collapse text-left">
          <thead className="sticky top-0">
            <tr className="border-b border-line bg-paper">
              <th className="eyebrow px-4 py-2.5">Student</th>
              <th className="eyebrow px-3 py-2.5">Score</th>
              <th className="eyebrow px-3 py-2.5">Mistakes</th>
              <th className="eyebrow hidden px-3 py-2.5 sm:table-cell">Graded</th>
              <th className="eyebrow px-3 py-2.5">Status</th>
            </tr>
          </thead>
          <tbody>
            {shown.map((draft) => (
              <tr
                key={draft.id}
                onClick={() => onOpen(draft)}
                className={`cursor-pointer border-b border-paper-dark transition-colors hover:bg-paper ${draft.status === "Discarded" ? "text-ink-muted" : ""}`}
              >
                <td className="px-4 py-2.5">
                  <div className="flex items-center gap-2.5">
                    <Avatar text={initials(draft.studentName)} userId={draft.studentId} size="sm" />
                    <span className="font-semibold">{draft.studentName}</span>
                    <span className="font-mono text-[11px] text-ink-faint">v{draft.versionNumber}</span>
                  </div>
                </td>
                <td className="whitespace-nowrap px-3 py-2.5 font-mono text-xs">
                  {draft.score != null ? (
                    <>
                      <b>{points(draft.score)}</b>
                      <span className="text-ink-faint">/{draft.maxScore ?? "–"}</span>
                      {draft.bonus ? <span className="text-success"> +{points(draft.bonus)}</span> : null}
                    </>
                  ) : (
                    <span className="font-sans text-ink-muted">in the review text</span>
                  )}
                </td>
                <td className="px-3 py-2.5">
                  <SeverityCounts draft={draft} />
                </td>
                <td className="hidden whitespace-nowrap px-3 py-2.5 text-xs text-ink-muted sm:table-cell">{formatDate(draft.createdAt)}</td>
                <td className="px-3 py-2.5">
                  {draft.status === "Discarded" ? (
                    <Chip tone="muted">Discarded</Chip>
                  ) : draft.current ? (
                    <Chip tone="info">Current draft</Chip>
                  ) : (
                    <Chip tone="dashed">Earlier draft</Chip>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </Modal>
  );
}

function SeverityDots({ draft }: { draft: AssignmentGrading }) {
  const parts = (["major", "minor", "style"] as const).filter((key) => draft[key] > 0);
  if (parts.length === 0) {
    return null;
  }

  return (
    <span
      className="flex items-center gap-1.5"
      title={`${mistakeCount(draft)} ${mistakeCount(draft) === 1 ? "mistake" : "mistakes"}: ${parts.map((key) => `${draft[key]} ${key}`).join(", ")}`}
    >
      {parts.map((key) => (
        <span key={key} className="flex items-center gap-0.5">
          <span className="h-1.5 w-1.5 rounded-full" style={{ backgroundColor: severityDot[key] }} />
          {draft[key]}
        </span>
      ))}
    </span>
  );
}

const severityDot: Record<"major" | "minor" | "style", string> = { major: severityColor.Major, minor: severityColor.Minor, style: severityColor.Style };

function SeverityCounts({ draft }: { draft: AssignmentGrading }) {
  const parts = (["major", "minor", "style"] as const).filter((key) => draft[key] > 0);
  if (parts.length === 0) {
    return <span className="text-xs text-ink-muted">None</span>;
  }

  return (
    <span className="flex flex-wrap gap-2 text-xs">
      {parts.map((key) => (
        <span key={key} className="flex items-center gap-1" title={key}>
          <span className="h-2 w-2 rounded-full" style={{ backgroundColor: severityDot[key] }} />
          {draft[key]} {key}
        </span>
      ))}
    </span>
  );
}
