import { type KeyboardEvent, type ReactNode, useEffect, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router";
import type { CourseStudent, CourseSyncSummary, StudentAssignmentColumn, StudentAssignmentResult } from "../api/client";
import { useCourseStudents, useSyncCourse } from "../api/hooks";
import { CourseName } from "../components/Layout";
import { stateChip } from "../components/SubmissionDetail";
import { Avatar, Button, Card, Chip, EmptyState, ErrorPanel, PageHeader, Spinner } from "../components/ui";
import { formatDate, formatDay, initials } from "../format";

export function StudentsPage() {
  const { courseId = "" } = useParams();
  const students = useCourseStudents(courseId);
  const sync = useSyncCourse(courseId);
  const [query, setQuery] = useState("");
  const [sort, setSort] = useState<SortOption>("turnedIn");
  const [filter, setFilter] = useState<Filter>("all");
  const [tableOpen, setTableOpen] = useState(false);
  const [searchParams, setSearchParams] = useSearchParams();
  const [showProfile, setShowProfile] = useState(false);

  const data = students.data;
  const synced = data?.syncedAt != null;
  const all = data?.students ?? [];
  const active = all.filter((s) => s.active);
  const scored = active.filter((s) => s.overallPercent != null);
  const average = scored.length > 0 ? scored.reduce((sum, s) => sum + s.overallPercent!, 0) / scored.length : null;

  const counts: Record<Filter, number> = {
    all: all.length,
    missing: all.filter(filters.missing.matches).length,
    improved: all.filter(filters.improved.matches).length,
    left: all.filter(filters.left.matches).length,
  };
  const search = query.trim().toLowerCase();
  const rows = sortStudents(all.filter((s) => filters[filter].matches(s) && s.name.toLowerCase().includes(search)), sort);
  const selected = all.find((s) => s.id === searchParams.get("student")) ?? rows[0];

  function select(student: CourseStudent, open = true) {
    setSearchParams((params) => {
      params.set("student", student.id);
      return params;
    }, { replace: true });
    if (open) {
      setShowProfile(true);
    }
  }

  return (
    <div className="flex flex-col gap-3 lg:h-[calc(100dvh-112px)] lg:min-h-[640px]">
      <PageHeader
        title="Students"
        subtitle={
          <>
            <CourseName courseId={courseId} /> · {synced ? `synced ${formatDate(data!.syncedAt)}` : "not synced yet"} · grades
            read from Classroom
          </>
        }
        actions={
          <>
            {synced && <Button onClick={() => setTableOpen(true)}>Grade table</Button>}
            <Button
              variant="primary"
              onClick={() => sync.mutate()}
              disabled={sync.isPending}
              title="Copy the roster, assignments and grades from Classroom; no files are downloaded and nothing is written to Classroom"
            >
              {sync.isPending ? "Syncing…" : "Sync course"}
            </Button>
          </>
        }
      />

      {sync.isPending && (
        <div className="shrink-0 rounded-[10px] border border-line bg-white px-4 py-2.5 text-xs">
          <Spinner label="Syncing… reading the roster, assignments and grades from Classroom." />
        </div>
      )}
      {sync.isError && <ErrorPanel error={sync.error} />}
      {sync.data && <SyncBanner summary={sync.data} onDismiss={() => sync.reset()} />}

      {students.isLoading && <Spinner />}
      {students.isError && <ErrorPanel error={students.error} />}

      {data && !synced && !sync.isPending && (
        <Card className="flex flex-1 items-center justify-center">
          <EmptyState>
            <div className="font-semibold text-ink">No students yet</div>
            Sync course copies the roster and every grade from Classroom in a few seconds. No files are downloaded.
            <div className="mt-3">
              <Button variant="primary" small onClick={() => sync.mutate()}>
                Sync course
              </Button>
            </div>
          </EmptyState>
        </Card>
      )}

      {data && synced && (
        <>
          <div className="grid shrink-0 grid-cols-2 border-t border-line md:grid-cols-4">
            <Stat
              label="Students"
              value={active.length}
              note={all.length > active.length ? `${all.length - active.length} left the course` : "on the roster"}
            />
            <Stat label="Class average" value={average == null ? "–" : `${Math.round(average)}%`} note="overall, final grades" />
            <Stat label="Missing work" value={active.reduce((sum, s) => sum + s.missing, 0)} note="past due, counts as 0" />
            <Stat label="Improved" value={active.reduce((sum, s) => sum + s.improved, 0)} note="grades raised after a fix" />
          </div>

          <Card className="@container/workspace flex h-[calc(100dvh-96px)] min-h-[460px] shrink-0 overflow-hidden lg:h-auto lg:min-h-[420px] lg:flex-1 lg:shrink">
            <StudentList
              hidden={showProfile}
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
            />
            <div className={`min-w-0 flex-1 ${showProfile ? "flex" : "hidden @2xl/workspace:flex"}`}>
              {selected ? (
                <StudentProfile
                  key={selected.id}
                  courseId={courseId}
                  student={selected}
                  assignments={data.assignments}
                  onBack={() => setShowProfile(false)}
                />
              ) : (
                <div className="flex flex-1 items-center justify-center text-ink-muted">No students match.</div>
              )}
            </div>
          </Card>
        </>
      )}

      {tableOpen && data && (
        <GradeTableModal
          students={sortStudents(all, "turnedIn")}
          assignments={data.assignments}
          onPick={(student) => {
            select(student);
            setTableOpen(false);
          }}
          onClose={() => setTableOpen(false)}
        />
      )}
    </div>
  );
}

type SortOption = "turnedIn" | "name" | "best" | "worst" | "missing";

const sortLabels: Record<SortOption, string> = {
  turnedIn: "Most turned in",
  name: "Name",
  best: "Overall: highest",
  worst: "Overall: lowest",
  missing: "Most missing",
};

type Filter = "all" | "missing" | "improved" | "left";

const filters: Record<Filter, { label: string; matches: (s: CourseStudent) => boolean }> = {
  all: { label: "All", matches: () => true },
  missing: { label: "Missing work", matches: (s) => s.active && s.missing > 0 },
  improved: { label: "Improved", matches: (s) => s.improved > 0 },
  left: { label: "Left course", matches: (s) => !s.active },
};

function sortStudents(list: CourseStudent[], sort: SortOption): CourseStudent[] {
  const byName = (a: CourseStudent, b: CourseStudent) => a.name.localeCompare(b.name);

  return [...list].sort((a, b) => {
    if (a.active !== b.active) {
      return a.active ? -1 : 1;
    }
    if (sort === "missing") {
      return b.missing - a.missing || byName(a, b);
    }
    if (sort === "turnedIn") {
      return b.turnedIn - a.turnedIn || (b.overallPercent ?? -1) - (a.overallPercent ?? -1) || byName(a, b);
    }
    if (sort === "best" || sort === "worst") {
      const [x, y] = [a.overallPercent ?? null, b.overallPercent ?? null];
      if (x == null || y == null) {
        return x == null && y == null ? byName(a, b) : x == null ? 1 : -1;
      }
      return (sort === "best" ? y - x : x - y) || byName(a, b);
    }
    return byName(a, b);
  });
}

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
}: {
  hidden: boolean;
  rows: CourseStudent[];
  counts: Record<Filter, number>;
  filter: Filter;
  onFilter: (filter: Filter) => void;
  query: string;
  onQuery: (query: string) => void;
  sort: SortOption;
  onSort: (sort: SortOption) => void;
  selectedId: string | undefined;
  onSelect: (student: CourseStudent, open?: boolean) => void;
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
      className={`w-full shrink-0 flex-col bg-white @2xl/workspace:flex @2xl/workspace:w-[270px] @2xl/workspace:border-r @2xl/workspace:border-line @4xl/workspace:w-[310px] @7xl/workspace:w-[340px] ${
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
          {rows.map((student, i) => {
            const isSelected = student.id === selectedId;
            const previous = rows[i - 1];
            return (
              <li
                key={student.id}
                className={`border-b border-paper-dark ${!student.active && previous?.active ? "border-t-2 border-t-line" : ""}`}
              >
                <button
                  id={`student-${student.id}`}
                  onClick={() => onSelect(student)}
                  aria-current={isSelected}
                  className={`flex w-full items-center gap-2.5 border-l-[3px] py-2.5 pl-3 pr-3 text-left ${
                    isSelected ? "border-l-accent bg-accent-tint/60" : "border-l-transparent hover:bg-paper/60"
                  }`}
                >
                  <Avatar text={initials(student.name)} userId={student.externalUserId} />
                  <span className="min-w-0 flex-1">
                    <span className={`block truncate font-semibold ${student.active ? "" : "text-ink-muted"}`}>{student.name}</span>
                    <span className="block truncate text-[11px] text-ink-muted">
                      {student.active ? `${student.turnedIn} turned in` : "Left the course"}
                      {student.missing > 0 && ` · ${student.missing} missing`}
                      {student.improved > 0 && ` · ${student.improved} improved`}
                    </span>
                  </span>
                  <span className="flex w-14 shrink-0 flex-col items-end gap-1">
                    <b className="tabular-nums">{student.overallPercent == null ? "–" : `${Math.round(student.overallPercent)}%`}</b>
                    <Meter percent={student.overallPercent} className="w-14" />
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

function StudentProfile({
  courseId,
  student,
  assignments,
  onBack,
}: {
  courseId: string;
  student: CourseStudent;
  assignments: StudentAssignmentColumn[];
  onBack: () => void;
}) {
  const [opened, setOpened] = useState<StudentAssignmentColumn | null>(null);
  const byAssignment = new Map(student.results.map((r) => [r.assignmentId, r]));

  return (
    <section className="@container flex min-h-0 min-w-0 flex-1 flex-col">
      <header className="flex flex-wrap items-center justify-between gap-3 border-b border-line bg-white px-4 py-3 sm:px-5">
        <button onClick={onBack} className="w-full text-left text-xs font-semibold text-accent @2xl/workspace:hidden">
          ← All students
        </button>
        <div className="flex min-w-0 items-center gap-3">
          <Avatar text={initials(student.name)} userId={student.externalUserId} />
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <b className="truncate text-[15px]">{student.name}</b>
              {student.active ? <Chip tone="tint">On the roster</Chip> : <Chip tone="dashed">Left course</Chip>}
            </div>
            <div className="text-xs text-ink-muted">
              {student.turnedIn} of {assignments.length} assignments turned in
            </div>
          </div>
        </div>
        <div className="text-right">
          <div className="text-[26px] font-bold leading-none tracking-tight tabular-nums">
            {student.overallPercent == null ? "–" : `${Math.round(student.overallPercent)}%`}
          </div>
          <div className="mt-1 text-[11px] text-ink-muted">
            {student.possible > 0 ? `${points(student.earned)} of ${points(student.possible)} points overall` : "nothing counted yet"}
          </div>
        </div>
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto">
        <div className="flex flex-col gap-4 p-4 sm:p-5">
          <div className="grid grid-cols-2 gap-px overflow-hidden rounded-[10px] border border-line bg-line @xl:grid-cols-4">
            <Tile label="Turned in" value={`${student.turnedIn} / ${assignments.length}`} note="handed in or returned" />
            <Tile label="Missing" value={student.missing} note="past due, counts as 0" strong={student.missing > 0} />
            <Tile label="Late" value={student.late} note="handed in after the deadline" />
            <Tile label="Improved" value={student.improved} note="grade raised after a fix" />
          </div>

          <div className="grid gap-4 @4xl:grid-cols-[minmax(0,1fr)_300px]">
            <div className="flex min-w-0 flex-col gap-4">
              <ScoreChart assignments={assignments} results={byAssignment} />

              <div className="rounded-[10px] border border-line bg-white">
                <div className="flex items-center justify-between border-b border-line px-4 py-2.5">
                  <b className="text-xs">Assignments</b>
                  <span className="text-[11px] text-ink-muted">Click one for its grade history</span>
                </div>
                <ul className="divide-y divide-paper-dark">
                  {assignments.map((assignment) => (
                    <AssignmentRow
                      key={assignment.id}
                      assignment={assignment}
                      result={byAssignment.get(assignment.id)}
                      onOpen={() => setOpened(assignment)}
                    />
                  ))}
                </ul>
              </div>
            </div>

            <ComingNext />
          </div>
        </div>
      </div>

      {opened && (
        <AssignmentModal
          courseId={courseId}
          student={student}
          assignment={opened}
          result={byAssignment.get(opened.id)}
          onClose={() => setOpened(null)}
        />
      )}
    </section>
  );
}

function ScoreChart({ assignments, results }: { assignments: StudentAssignmentColumn[]; results: Map<string, StudentAssignmentResult> }) {
  const share = (value: number | null | undefined, max: number | null | undefined) =>
    value != null && max ? (value / max) * 100 : null;
  const bars = assignments.map((a) => {
    const result = results.get(a.id);
    return { a, result, final: share(result?.finalGrade, a.maxPoints), initial: share(result?.initialGrade, a.maxPoints) };
  });
  const top = Math.max(100, ...bars.map((b) => b.final ?? 0), ...bars.map((b) => b.initial ?? 0));

  return (
    <div className="rounded-[10px] border border-line bg-white">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-line px-4 py-2.5">
        <b className="text-xs">Score by assignment</b>
        <span className="flex items-center gap-3 text-[11px] text-ink-muted">
          <span className="flex items-center gap-1">
            <span className="h-2 w-2 rounded-sm bg-accent-tint ring-1 ring-accent/30" /> first returned
          </span>
          <span className="flex items-center gap-1">
            <span className="h-2 w-2 rounded-sm bg-accent" /> final
          </span>
        </span>
      </div>
      <div className="flex h-[170px] items-end gap-2 px-4 pb-2 pt-4">
        {bars.map(({ a, result, final, initial }) => (
          <div key={a.id} className="flex h-full min-w-0 flex-1 flex-col items-center justify-end gap-1">
            <span className="text-[11px] font-semibold tabular-nums">
              {final != null ? `${Math.round(final)}%` : result?.missing ? "0" : ""}
            </span>
            <div className="relative flex w-full max-w-[56px] flex-1 items-end justify-center gap-1">
              {final != null ? (
                <>
                  {initial != null && initial !== final && (
                    <span
                      className="w-1/3 rounded-t bg-accent-tint ring-1 ring-accent/30"
                      style={{ height: `${(initial / top) * 100}%` }}
                      title={`First returned: ${points(result!.initialGrade!)}`}
                    />
                  )}
                  <span
                    className="w-1/2 rounded-t bg-accent"
                    style={{ height: `${(final / top) * 100}%` }}
                    title={`Final: ${points(result!.finalGrade!)} of ${a.maxPoints}`}
                  />
                </>
              ) : (
                <span
                  className={`w-1/2 rounded-t border border-dashed ${result?.missing ? "h-1 border-ink" : "h-full border-line"}`}
                  title={result?.missing ? "Missing: counts as 0" : result?.turnedIn ? "Turned in, no grade yet" : "Not due or not graded"}
                />
              )}
            </div>
            <span className="w-full truncate text-center text-[10px] text-ink-muted" title={a.title}>
              {a.title}
            </span>
          </div>
        ))}
      </div>
      <div className="border-t border-line px-4 py-1.5 text-[11px] text-ink-muted">
        Dashed: missing (0) or not graded yet. Bars above 100% include bonus points.
      </div>
    </div>
  );
}

function AssignmentRow({
  assignment,
  result,
  onOpen,
}: {
  assignment: StudentAssignmentColumn;
  result: StudentAssignmentResult | undefined;
  onOpen: () => void;
}) {
  const state = result?.state ? stateChip[result.state] : undefined;

  return (
    <li>
      <button onClick={onOpen} className="flex w-full items-center gap-3 px-4 py-2.5 text-left hover:bg-paper/60">
        <span className="min-w-0 flex-1">
          <span className="block truncate font-semibold">{assignment.title}</span>
          <span className="block text-[11px] text-ink-muted">
            {assignment.dueAt ? `Due ${formatDay(assignment.dueAt)}` : "No deadline"} · {assignment.maxPoints ?? "–"} points
          </span>
        </span>
        <span className="hidden shrink-0 gap-1.5 @lg:flex">
          {result?.missing ? <Chip tone="dashed">Missing</Chip> : state && <Chip tone={state.tone}>{state.label}</Chip>}
          {result?.turnedIn && result.late && <Chip tone="dashed">Late</Chip>}
        </span>
        <span className="w-24 shrink-0 text-right">
          <Grade result={result} maxPoints={assignment.maxPoints} />
        </span>
      </button>
    </li>
  );
}

function Grade({ result, maxPoints }: { result: StudentAssignmentResult | undefined; maxPoints: number | null | undefined }) {
  if (result?.finalGrade != null) {
    const changed = result.initialGrade != null && result.initialGrade !== result.finalGrade;
    return (
      <span className="whitespace-nowrap tabular-nums">
        {changed && <span className="text-ink-muted">{points(result.initialGrade!)} → </span>}
        <b>{points(result.finalGrade)}</b>
        <span className="text-ink-muted"> / {maxPoints ?? "–"}</span>
      </span>
    );
  }
  if (result?.missing) {
    return <b className="tabular-nums">0</b>;
  }
  return <span className="text-xs text-ink-muted">{result?.turnedIn ? "No grade yet" : "–"}</span>;
}

function ComingNext() {
  const plans = [
    {
      title: "Mistake history",
      text: "Every mistake from the AI reviews, labelled new, repeated or fixed since earlier assignments.",
    },
    {
      title: "Written overview",
      text: "A short summary of the student's progress, drafted by the agent for you to edit before sharing.",
    },
    {
      title: "Mistake categories",
      text: "Where the student loses points most often: design, naming, error handling, missing requirements.",
    },
  ];

  return (
    <aside className="flex flex-col gap-2">
      <h3 className="text-[11px] font-bold uppercase tracking-[0.06em] text-accent">Coming next</h3>
      {plans.map((plan) => (
        <div key={plan.title} className="rounded-[10px] border border-dashed border-line bg-white/60 px-4 py-3">
          <div className="flex items-center justify-between gap-2">
            <b className="text-xs">{plan.title}</b>
            <Chip tone="dashed">Planned</Chip>
          </div>
          <p className="mt-1 text-xs text-ink-muted">{plan.text}</p>
        </div>
      ))}
    </aside>
  );
}

function AssignmentModal({
  courseId,
  student,
  assignment,
  result,
  onClose,
}: {
  courseId: string;
  student: CourseStudent;
  assignment: StudentAssignmentColumn;
  result: StudentAssignmentResult | undefined;
  onClose: () => void;
}) {
  useEscape(onClose);
  const state = result?.state ? stateChip[result.state] : undefined;
  const workspace = `/courses/${courseId}/assignments/${assignment.id}${result?.submissionId ? `?student=${encodeURIComponent(result.submissionId)}` : ""}`;

  return (
    <div className="fixed inset-0 z-40 flex items-center justify-center bg-ink/50 p-3 sm:p-6" onClick={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-label={`${student.name} · ${assignment.title}`}
        onClick={(e) => e.stopPropagation()}
        className="flex max-h-[90dvh] w-full max-w-[520px] flex-col overflow-hidden rounded-xl border border-line bg-white shadow-xl"
      >
        <header className="flex items-start justify-between gap-3 border-b border-line px-5 py-3.5">
          <div className="min-w-0">
            <div className="truncate text-[15px] font-bold">{assignment.title}</div>
            <div className="text-xs text-ink-muted">
              {student.name} · {assignment.dueAt ? `due ${formatDate(assignment.dueAt)}` : "no deadline"} · {assignment.maxPoints ?? "–"} points
            </div>
          </div>
          <Button small onClick={onClose}>
            Close
          </Button>
        </header>

        <div className="flex flex-col gap-4 overflow-y-auto px-5 py-4">
          <div className="flex flex-wrap items-center gap-2">
            {result?.missing ? <Chip tone="dashed">Missing · counts as 0</Chip> : state && <Chip tone={state.tone}>{state.label}</Chip>}
            {result?.turnedIn && result.late && <Chip tone="dashed">Late</Chip>}
            <span className="ml-auto text-lg">
              <Grade result={result} maxPoints={assignment.maxPoints} />
            </span>
          </div>

          <div>
            <h3 className="mb-2 text-[11px] font-bold uppercase tracking-[0.06em] text-accent">Grade history</h3>
            {result && result.returns.length > 0 ? (
              <ol className="relative flex flex-col gap-3 border-l border-line pl-4">
                {result.returns.map((r, i) => (
                  <li key={i} className="relative">
                    <span className="absolute -left-[21px] top-1 h-2.5 w-2.5 rounded-full border-2 border-white bg-accent" />
                    <div className="flex items-baseline justify-between gap-3">
                      <b>
                        {i === 0 ? "Returned" : "Returned again"} with {r.points == null ? "no grade" : points(r.points)}
                      </b>
                      <span className="text-[11px] text-ink-muted">{formatDate(r.at)}</span>
                    </div>
                    {i === 0 && result.returns.length > 1 && <div className="text-[11px] text-ink-muted">Initial score</div>}
                    {i === result.returns.length - 1 && <div className="text-[11px] text-ink-muted">Final score</div>}
                  </li>
                ))}
              </ol>
            ) : (
              <p className="text-xs text-ink-muted">
                {result?.missing
                  ? "Nothing was handed in before the deadline, so this counts as 0 in the overall score."
                  : result?.turnedIn
                    ? "Handed in, not graded yet in Classroom. Left out of the overall score until it is."
                    : "Not handed in yet, and not due yet."}
              </p>
            )}
          </div>

          <p className="text-[11px] text-ink-muted">Grades are read from Classroom; this tool never changes them.</p>
        </div>

        <footer className="flex justify-end border-t border-line px-5 py-3">
          <Link to={workspace}>
            <Button small variant="primary">
              Open in the assignment workspace →
            </Button>
          </Link>
        </footer>
      </div>
    </div>
  );
}

function GradeTableModal({
  students,
  assignments,
  onPick,
  onClose,
}: {
  students: CourseStudent[];
  assignments: StudentAssignmentColumn[];
  onPick: (student: CourseStudent) => void;
  onClose: () => void;
}) {
  useEscape(onClose);

  return (
    <div className="fixed inset-0 z-40 flex items-center justify-center bg-ink/50 p-2 sm:p-6" onClick={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Grade table"
        onClick={(e) => e.stopPropagation()}
        className="flex max-h-[94dvh] w-full max-w-[1200px] flex-col overflow-hidden rounded-xl border border-line bg-white shadow-xl"
      >
        <header className="flex flex-wrap items-center justify-between gap-3 border-b border-line px-5 py-3">
          <div>
            <b className="text-[15px]">Grade table</b>
            <div className="text-xs text-ink-muted">Final grades; "70 → 95" when a fix raised it. Click a name to open the profile.</div>
          </div>
          <Button small onClick={onClose}>
            Close
          </Button>
        </header>
        <div className="overflow-auto">
          <table className="w-full border-collapse text-left">
            <thead className="sticky top-0 z-20">
              <tr className="border-b border-line bg-[#faf9f6] text-[11px] uppercase tracking-[0.06em] text-ink-muted">
                <th className="sticky left-0 z-10 bg-[#faf9f6] px-4 py-2.5 font-semibold">Student</th>
                <th className="px-3 py-2.5 font-semibold">Overall</th>
                {assignments.map((a) => (
                  <th key={a.id} className="min-w-[110px] px-3 py-2.5 align-bottom font-semibold" title={a.title}>
                    <span className="line-clamp-2 normal-case tracking-normal text-ink">{a.title}</span>
                    <span className="block whitespace-nowrap font-normal normal-case tracking-normal">
                      {a.maxPoints ?? "–"} pts{a.dueAt ? ` · ${formatDay(a.dueAt)}` : ""}
                    </span>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {students.map((student) => {
                const byAssignment = new Map(student.results.map((r) => [r.assignmentId, r]));
                return (
                  <tr key={student.id} className={`group border-b border-paper-dark hover:bg-paper/40 ${student.active ? "" : "text-ink-muted"}`}>
                    <td className="sticky left-0 z-10 bg-white px-4 py-2 group-hover:bg-[#fbfaf8]">
                      <button onClick={() => onPick(student)} className="font-semibold hover:text-accent hover:underline">
                        {student.name}
                      </button>
                    </td>
                    <td className="whitespace-nowrap px-3 py-2 font-semibold tabular-nums">
                      {student.overallPercent == null ? "–" : `${Math.round(student.overallPercent)}%`}
                    </td>
                    {assignments.map((a) => {
                      const result = byAssignment.get(a.id);
                      return (
                        <td key={a.id} className="px-3 py-2">
                          {result?.missing ? <Chip tone="dashed">Missing · 0</Chip> : <Grade result={result} maxPoints={a.maxPoints} />}
                        </td>
                      );
                    })}
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

function useEscape(onClose: () => void) {
  useEffect(() => {
    const onKey = (e: globalThis.KeyboardEvent) => {
      if (e.key === "Escape") {
        onClose();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);
}

function Meter({ percent, className = "" }: { percent: number | null | undefined; className?: string }) {
  return (
    <span className={`block h-1.5 shrink-0 overflow-hidden rounded-full bg-paper-dark ${className}`}>
      <span className="block h-full rounded-full bg-accent" style={{ width: `${Math.min(100, Math.max(0, percent ?? 0))}%` }} />
    </span>
  );
}

function points(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(1);
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

function Tile({ label, value, note, strong }: { label: string; value: ReactNode; note: string; strong?: boolean }) {
  return (
    <div className="min-w-0 bg-white px-4 py-3">
      <div className="text-xs text-ink-muted">{label}</div>
      <div className={`text-[24px] font-bold leading-tight tracking-tight tabular-nums ${strong ? "text-ink" : ""}`}>{value}</div>
      <div className="truncate text-[11px] font-semibold text-accent">{note}</div>
    </div>
  );
}

function SyncBanner({ summary, onDismiss }: { summary: CourseSyncSummary; onDismiss: () => void }) {
  const parts = [
    `${summary.students} students`,
    summary.newStudents > 0 ? `${summary.newStudents} new` : null,
    summary.leftStudents > 0 ? `${summary.leftStudents} left the course` : null,
    `${summary.assignments} assignments`,
    `${summary.submissions} submissions checked`,
    `${summary.graded} graded`,
  ].filter(Boolean);

  return (
    <div className="flex shrink-0 items-center gap-3 rounded-[10px] border border-line bg-white px-4 py-2.5 text-xs">
      <b className="shrink-0">Sync finished</b>
      <span className="min-w-0 flex-1 truncate text-ink-muted">{parts.join(" · ")}</span>
      <button onClick={onDismiss} aria-label="Dismiss" className="shrink-0 px-1 text-ink-muted hover:text-ink">
        ✕
      </button>
    </div>
  );
}
