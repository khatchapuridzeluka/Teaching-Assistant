import { type KeyboardEvent, type ReactNode, useEffect, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useParams, useSearchParams } from "react-router";
import type { AssignmentGrading, ExtractionSummary, FetchProblem, FetchSummary, JobInfo, StoredSubmission, StoredVersion, Submission } from "../api/client";
import {
  useAssignments,
  useFetchSubmissions,
  useAssignmentJobs,
  useCancelJob,
  useQueueGrading,
  useReExtract,
  useAssignmentGradings,
  useStoredSubmissions,
  useSubmissions,
} from "../api/hooks";
import { InstructionsBar } from "../components/InstructionsCard";
import { useTrackJob } from "../components/JobToasts";
import { PreviewPanel } from "../components/PreviewPanel";
import { hasWork, stateChip, SubmissionDetail } from "../components/SubmissionDetail";
import { Avatar, Button, Card, Chip, ErrorPanel, PageHeader, Spinner } from "../components/ui";
import { formatDate, formatDay, initials } from "../format";

export function AssignmentPage() {
  const { courseId = "", assignmentId = "" } = useParams();
  const assignment = useAssignments(courseId).data?.find((a) => a.id === assignmentId);
  const submissions = useSubmissions(courseId, assignmentId);
  const stored = useStoredSubmissions(courseId, assignmentId);
  const fetch = useFetchSubmissions(courseId, assignmentId);
  const extract = useReExtract(courseId, assignmentId);
  const queue = useQueueGrading(courseId, assignmentId);
  const cancel = useCancelJob(courseId, assignmentId);
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
  const pastDue = assignment?.dueAt != null && new Date(assignment.dueAt).getTime() < Date.now();

  return (
    <div className="flex flex-col gap-3 lg:h-[calc(100dvh-112px)] lg:min-h-[640px]">
      <PageHeader
        title={assignment?.title ?? "Assignment"}
        subtitle={assignment && `Due ${formatDate(assignment.dueAt)} · ${assignment.maxPoints ?? "–"} points · read-only from Classroom`}
        actions={
          <>
            {assignment?.link && (
              <a href={assignment.link} target="_blank" rel="noreferrer">
                <Button>Classroom ↗</Button>
              </a>
            )}
            {(stored.data?.length ?? 0) > 0 && (
              <Button
                onClick={() => extract.mutate()}
                disabled={extract.isPending || fetch.isPending}
                title="Rebuild the extracted code from the stored files; nothing is downloaded"
              >
                {extract.isPending ? "Extracting…" : "Re-extract"}
              </Button>
            )}
            {(gradings.data?.length ?? 0) > 0 && (
              <Button onClick={() => setDraftsOpen(true)} title="Every saved AI draft of this assignment">
                Drafts ({gradings.data!.filter((g) => g.status === "Draft").length})
              </Button>
            )}
            <Button variant="primary" onClick={() => fetch.mutate()} disabled={fetch.isPending}>
              {fetch.isPending ? "Fetching…" : "Fetch from Classroom"}
            </Button>
          </>
        }
      />

      {submissions.data && (
        <div className="grid shrink-0 grid-cols-2 border-t border-line md:grid-cols-4">
          <Stat label="Turned in" value={`${withWork.length} / ${all.length}`} note="including returned" />
          <Stat label="Returned" value={counts.returned} note="in Classroom" />
          <Stat label="AI drafts" value={`${drafts} / ${withWork.length}`} note="saved, waiting for review" />
          <Stat
            label="Not submitted"
            value={counts.missing}
            note={assignment?.dueAt ? (pastDue ? "past due" : `due ${formatDay(assignment.dueAt)}`) : "no due date"}
          />
        </div>
      )}

      {assignment && (
        <div className="shrink-0">
          <InstructionsBar courseId={courseId} assignment={assignment} />
        </div>
      )}

      {fetch.isPending && (
        <Banner>
          <Spinner label="Fetching… downloading new and changed submissions from Google Drive." />
        </Banner>
      )}
      {fetch.isError && <ErrorPanel error={fetch.error} />}
      {fetch.data && <FetchBanner summary={fetch.data} onDismiss={() => fetch.reset()} />}
      {extract.isError && <ErrorPanel error={extract.error} />}
      {extract.data && <ExtractBanner summary={extract.data} onDismiss={() => extract.reset()} />}

      {submissions.isLoading && <Spinner />}
      {submissions.isError && <ErrorPanel error={submissions.error} />}
      {submissions.data && (
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

function Stat({ label, value, note }: { label: string; value: ReactNode; note: string }) {
  return (
    <div className="min-w-0 border-line px-3 py-2.5 even:border-l [&:nth-child(n+3)]:border-t sm:px-4 md:[&:nth-child(n+3)]:border-t-0 md:[&:not(:first-child)]:border-l">
      <div className="text-xs text-ink-muted">{label}</div>
      <div className="flex items-baseline gap-2">
        <b className="whitespace-nowrap text-[22px] leading-tight tracking-tight">{value}</b>
        <span className="truncate text-[11px] font-semibold text-accent">{note}</span>
      </div>
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
      className={`w-full shrink-0 flex-col bg-white @2xl/workspace:flex @2xl/workspace:w-[260px] @2xl/workspace:border-r @2xl/workspace:border-line @4xl/workspace:w-[300px] @7xl/workspace:w-[340px] ${
        hidden ? "hidden" : "flex"
      }`}
    >
      <div className="flex shrink-0 flex-col gap-2 border-b border-line p-3">
        <div className="grid grid-cols-4 rounded-lg border border-line bg-paper p-0.5" role="tablist" aria-label="Filter students">
          {(Object.keys(filters) as Filter[]).map((key) => (
            <button
              key={key}
              role="tab"
              aria-selected={filter === key}
              onClick={() => onFilter(key)}
              className={`flex flex-col items-center rounded-md px-1 py-1 leading-tight ${
                filter === key ? "bg-white text-ink shadow-sm" : "text-ink-muted hover:text-ink"
              }`}
            >
              <b className={`text-[13px] ${filter === key ? "text-accent" : ""}`}>{counts[key]}</b>
              <span className="text-[10px] font-semibold">{filters[key].label}</span>
            </button>
          ))}
        </div>
        <div className="flex gap-2">
          <input
            value={query}
            onChange={(e) => onQuery(e.target.value)}
            placeholder="Search students"
            aria-label="Search students"
            className="h-8 min-w-0 flex-1 rounded-lg border border-line bg-white px-2.5 text-xs outline-none focus:border-accent"
          />
          <select
            value={sort}
            onChange={(e) => onSort(e.target.value as SortOption)}
            aria-label="Sort students"
            className="h-8 cursor-pointer rounded-lg border border-line bg-white px-1.5 text-xs font-semibold hover:border-accent"
          >
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
              <li
                key={submission.id}
                className={`border-b border-paper-dark ${!working && previous && hasWork(previous) ? "border-t-2 border-t-line" : ""}`}
              >
                <button
                  id={`student-${submission.id}`}
                  onClick={() => onSelect(submission)}
                  aria-current={isSelected}
                  className={`flex w-full items-center gap-2.5 border-l-[3px] py-2.5 pl-3 pr-3 text-left ${
                    isSelected ? "border-l-accent bg-accent-tint/60" : "border-l-transparent hover:bg-paper/60"
                  }`}
                >
                  <Avatar text={initials(submission.studentName)} userId={submission.studentId} />
                  <span className="min-w-0 flex-1">
                    <span className={`block truncate font-semibold ${working ? "" : "text-ink-muted"}`}>{submission.studentName}</span>
                    <span className="block truncate text-[11px] text-ink-muted">
                      {working
                        ? `${formatDate(submission.turnedInAt)} · ${files} ${files === 1 ? "file" : "files"}${version && version.number > 1 ? ` · v${version.number}` : ""}`
                        : "Nothing turned in"}
                    </span>
                  </span>
                  <span className="flex shrink-0 flex-col items-end gap-1">
                    <Chip tone={state.tone}>{state.label}</Chip>
                    {version && gradingVersionIds.has(version.id) ? (
                      <span className="text-[11px] font-semibold text-accent">
                        {gradingVersionIds.get(version.id) === "Queued" ? "Queued…" : "Grading…"}
                      </span>
                    ) : review?.reportStatus === "Approved" ? (
                      <span className="text-[11px] font-semibold text-accent">✓ Report approved</span>
                    ) : review?.reportStatus === "InReview" ? (
                      <span className="text-[11px] font-semibold text-accent">Report in review</span>
                    ) : review ? (
                      <span className="text-[11px] font-semibold text-accent">
                        Draft {review.score != null ? `${points(review.score)}/${review.maxScore ?? "–"}` : ""} · {mistakeCount(review)}{" "}
                        {mistakeCount(review) === 1 ? "mistake" : "mistakes"}
                      </span>
                    ) : working && submission.late ? (
                      <Chip tone="dashed">Late</Chip>
                    ) : working && !storedById.has(submission.id) ? (
                      <span className="text-[11px] text-ink-muted">Not fetched</span>
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

function Banner({ children }: { children: ReactNode }) {
  return <div className="shrink-0 rounded-[10px] border border-line bg-white px-4 py-2.5 text-xs">{children}</div>;
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
    <Banner>
      <div className="flex items-center gap-3">
        <b className="shrink-0">{title}</b>
        <span className="min-w-0 flex-1 truncate text-ink-muted" title={text}>{text}</span>
        {problems.length > 0 && (
          <button onClick={() => setOpen(!open)} aria-expanded={open} className="shrink-0 font-semibold text-ink underline">
            {problems.length} need{problems.length === 1 ? "s" : ""} your attention {open ? "▴" : "▾"}
          </button>
        )}
        <button onClick={onDismiss} aria-label="Dismiss" className="shrink-0 px-1 text-ink-muted hover:text-ink">
          ✕
        </button>
      </div>
      {open && (
        <ul className="mt-2 flex max-h-[140px] flex-col gap-1 overflow-y-auto border-t border-line pt-2">
          {problems.map((problem, i) => (
            <li key={i} className="flex gap-2">
              <b className="shrink-0">{problem.studentName}</b>
              <span className="text-ink-muted">{problem.detail}</span>
            </li>
          ))}
        </ul>
      )}
    </Banner>
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

  useEffect(() => {
    const onKey = (e: globalThis.KeyboardEvent) => {
      if (e.key === "Escape") {
        onClose();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  return (
    <div className="fixed inset-0 z-40 flex items-center justify-center bg-ink/50 p-2 sm:p-6" onClick={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Saved drafts"
        onClick={(e) => e.stopPropagation()}
        className="flex max-h-[90dvh] w-full max-w-[920px] flex-col overflow-hidden rounded-xl border border-line bg-white shadow-xl"
      >
        <header className="flex flex-wrap items-center justify-between gap-3 border-b border-line px-5 py-3">
          <div>
            <b className="text-[15px]">Saved drafts</b>
            <div className="text-xs text-ink-muted">AI gradings saved for your review. Click one to open it next to the code.</div>
          </div>
          <div className="flex items-center gap-2">
            <div className="flex rounded-lg border border-line bg-paper p-0.5 text-xs font-semibold" role="tablist" aria-label="Which drafts">
              {[false, true].map((all) => (
                <button
                  key={String(all)}
                  role="tab"
                  aria-selected={showAll === all}
                  onClick={() => setShowAll(all)}
                  className={`rounded-md px-2.5 py-1 ${showAll === all ? "bg-white text-ink shadow-sm" : "text-ink-muted hover:text-ink"}`}
                >
                  {all ? `All (${drafts.length})` : `Current (${drafts.filter((d) => d.current).length})`}
                </button>
              ))}
            </div>
            <Button small onClick={onClose}>
              Close
            </Button>
          </div>
        </header>

        {shown.length === 0 ? (
          <div className="px-5 py-10 text-center text-ink-muted">No current drafts; every draft was discarded.</div>
        ) : (
          <div className="overflow-auto">
            <table className="w-full border-collapse text-left">
              <thead className="sticky top-0">
                <tr className="border-b border-line bg-[#faf9f6] text-[11px] uppercase tracking-[0.06em] text-ink-muted">
                  <th className="px-4 py-2.5 font-semibold">Student</th>
                  <th className="px-3 py-2.5 font-semibold">Score</th>
                  <th className="px-3 py-2.5 font-semibold">Mistakes</th>
                  <th className="hidden px-3 py-2.5 font-semibold sm:table-cell">Graded</th>
                  <th className="px-3 py-2.5 font-semibold">Status</th>
                </tr>
              </thead>
              <tbody>
                {shown.map((draft) => (
                  <tr
                    key={draft.id}
                    onClick={() => onOpen(draft)}
                    className={`cursor-pointer border-b border-paper-dark hover:bg-paper/60 ${draft.status === "Discarded" ? "text-ink-muted" : ""}`}
                  >
                    <td className="px-4 py-2.5">
                      <div className="font-semibold">{draft.studentName}</div>
                      <div className="text-[11px] text-ink-muted">v{draft.versionNumber}</div>
                    </td>
                    <td className="whitespace-nowrap px-3 py-2.5 tabular-nums">
                      {draft.score != null ? (
                        <>
                          <b>{points(draft.score)}</b>
                          <span className="text-ink-muted"> / {draft.maxScore ?? "–"}</span>
                          {draft.bonus ? <span className="text-ink-muted"> +{points(draft.bonus)}</span> : null}
                        </>
                      ) : (
                        <span className="text-xs text-ink-muted">in the review text</span>
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
                        <Chip tone="tint">Current draft</Chip>
                      ) : (
                        <Chip tone="dashed">Earlier draft</Chip>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}

const severityDot: Record<"major" | "minor" | "style", string> = { major: "#dc2626", minor: "#d97706", style: "#64748b" };

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
