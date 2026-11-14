import { type KeyboardEvent, useEffect, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router";
import type { CourseStudent, CourseSyncSummary, StudentAssignmentColumn, StudentAssignmentResult } from "../api/client";
import { useCourseStudents, useGradeStudent, useSyncCourse } from "../api/hooks";
import { ConfirmDialog } from "../components/ConfirmDialog";
import { useTrackJob } from "../components/JobToasts";
import { CourseName } from "../components/Layout";
import { MistakeCategoriesCard, MistakeHistoryCard } from "../components/StudentMistakes";
import { StrengthsCard } from "../components/StudentStrengths";
import { StudentOverviewCard } from "../components/StudentOverview";
import { stateChip } from "../components/SubmissionDetail";
import { Avatar, Button, buttonClass, Card, Chip, CountTabs, EmptyState, ErrorPanel, Meter, Modal, Notice, PageHeader, SearchField, Spinner, Stat, StatStrip } from "../components/ui";
import { ArrowLeft, ArrowRight, Award, CalendarX, CircleAlert, Clock, GraduationCap, LayoutDashboard, RefreshCw, Send, Sparkles, Table2, TrendingUp, Users } from "lucide-react";
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
  const selected = all.find((s) => s.id === searchParams.get("student") || s.externalUserId === searchParams.get("student")) ?? rows[0];

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
    <div className="flex flex-col gap-3 lg:h-[calc(100dvh-72px)] lg:min-h-[640px]">
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
            {synced && (
              <Button icon={<Table2 />} onClick={() => setTableOpen(true)}>
                Grade table
              </Button>
            )}
            <Button
              variant="primary"
              icon={<RefreshCw className={sync.isPending ? "animate-spin" : ""} />}
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
        <Notice>
          <Spinner label="Syncing… reading the roster, assignments and grades from Classroom." />
        </Notice>
      )}
      {sync.isError && <ErrorPanel error={sync.error} />}
      {sync.data && <SyncBanner summary={sync.data} onDismiss={() => sync.reset()} />}

      {students.isLoading && <Spinner />}
      {students.isError && <ErrorPanel error={students.error} />}

      {data && !synced && !sync.isPending && (
        <Card className="flex flex-1 items-center justify-center">
          <EmptyState icon={<Users />}>
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
          <StatStrip>
            <Stat
              compact
              icon={<Users />}
              label="Students"
              value={active.length}
              note={all.length > active.length ? `${all.length - active.length} left the course` : "on the roster"}
            />
            <Stat compact icon={<GraduationCap />} label="Class average" value={average == null ? "–" : `${Math.round(average)}%`} note="overall, final grades" />
            <Stat compact icon={<CalendarX />} label="Missing work" value={active.reduce((sum, s) => sum + s.missing, 0)} note="past due, counts as 0" />
            <Stat compact icon={<TrendingUp />} label="Improved" value={active.reduce((sum, s) => sum + s.improved, 0)} note="grades raised after a fix" />
          </StatStrip>

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
      <div className="flex shrink-0 flex-col gap-2 border-b border-line p-2.5">
        <CountTabs
          label="Filter students"
          value={filter}
          onChange={onFilter}
          options={(Object.keys(filters) as Filter[]).map((key) => ({ value: key, label: filters[key].label, count: counts[key] }))}
        />
        <div className="flex gap-2">
          <SearchField value={query} onChange={onQuery} placeholder="Search students" className="flex-1" />
          <select
            value={sort}
            onChange={(e) => onSort(e.target.value as SortOption)}
            aria-label="Sort students"
            className="field px-1.5 font-medium"
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
                    isSelected ? "border-l-accent bg-accent-tint/60" : "border-l-transparent hover:bg-paper"
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
                    <b className="num text-[13px]">{student.overallPercent == null ? "–" : `${Math.round(student.overallPercent)}%`}</b>
                    <Meter percent={student.overallPercent} className="w-14" tone={student.overallPercent != null && student.overallPercent < 50 ? "danger" : "accent"} />
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
  const gradeStudent = useGradeStudent(courseId, student.externalUserId);
  const trackJob = useTrackJob();

  const [confirmGrade, setConfirmGrade] = useState(false);
  const [regradeDrafted, setRegradeDrafted] = useState(false);

  useEffect(() => {
    gradeStudent.reset();
    setConfirmGrade(false);
  }, [student.id]);

  function openGradeAll() {
    setRegradeDrafted(false);
    setConfirmGrade(true);
  }

  function gradeAll() {
    setConfirmGrade(false);
    gradeStudent.mutate(regradeDrafted, {
      onSuccess: (job) => {
        trackJob(job, {
          label: `${student.name} · fetching, then grading`,
          to: `/courses/${courseId}/students?student=${student.id}`,
        });
      },
    });
  }

  return (
    <section className="@container flex min-h-0 min-w-0 flex-1 flex-col">
      <header className="flex flex-wrap items-center justify-between gap-x-5 gap-y-3 border-b border-line bg-white px-4 py-2.5 sm:px-5">
        <button onClick={onBack} className="flex w-full items-center gap-1 text-left text-xs font-semibold text-accent @2xl/workspace:hidden">
          <ArrowLeft className="h-3.5 w-3.5" /> All students
        </button>
        <div className="flex min-w-0 items-center gap-3">
          <Avatar text={initials(student.name)} userId={student.externalUserId} size="lg" />
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <b className="truncate font-display text-[19px] font-semibold leading-tight">{student.name}</b>
              {student.active ? <Chip tone="success">On the roster</Chip> : <Chip tone="dashed">Left course</Chip>}
            </div>
            <div className="text-xs text-ink-muted">
              {student.turnedIn} of {assignments.length} assignments turned in
            </div>
          </div>
          <div className="flex flex-wrap items-center gap-1.5">
            <Link
              to={`/courses/${courseId}/students/${student.externalUserId}/overview`}
              className={buttonClass("outline", true)}
              title="The whole picture: scores, mistakes and strengths together, with its own AI analysis"
            >
              <LayoutDashboard className="h-3.5 w-3.5" /> Overview
            </Link>
            <Link
              to={`/courses/${courseId}/students/${student.externalUserId}/mistakes`}
              className={buttonClass("outline", true)}
              title="Every mistake of this student's graded assignments, with filters and charts"
            >
              <CircleAlert className="h-3.5 w-3.5" /> Mistakes
            </Link>
            <Link
              to={`/courses/${courseId}/students/${student.externalUserId}/strengths`}
              className={buttonClass("outline", true)}
              title="Every strength of this student's graded assignments, with filters and charts"
            >
              <Award className="h-3.5 w-3.5" /> Strengths
            </Link>
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-x-5 gap-y-2">
          <div className="text-right">
            <div className="num text-[26px] leading-none">{student.overallPercent == null ? "–" : `${Math.round(student.overallPercent)}%`}</div>
            <div className="mt-1 text-[11px] text-ink-muted">
              {student.possible > 0 ? `${points(student.earned)} of ${points(student.possible)} points overall` : "nothing counted yet"}
            </div>
          </div>
          <Button
            small
            icon={<Sparkles />}
            onClick={openGradeAll}
            disabled={gradeStudent.isPending}
            title="Fetch this student's turned-in work from Classroom, then queue an AI grading for every assignment without a draft"
          >
            {gradeStudent.isPending ? "Queuing…" : "Grade all assignments"}
          </Button>
        </div>
      </header>

      {confirmGrade && (
        <ConfirmDialog
          title={`Grade all assignments of ${student.name}?`}
          confirmLabel="Grade assignments"
          onConfirm={gradeAll}
          onCancel={() => setConfirmGrade(false)}
        >
          {regradeDrafted ? (
            <p>
              First everything {student.name} turned in is fetched from Google Classroom, then every assignment with stored work gets a new
              AI grading, including those that already have a draft: the new draft becomes current and the earlier one stays in the
              history. Assignments that were not turned in are left out.
            </p>
          ) : (
            <p>
              First everything {student.name} turned in is fetched from Google Classroom, then every assignment without a draft gets an
              AI grading. Assignments with a draft that is not discarded are skipped, and assignments that were not turned in are left
              out.
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
              <span className="block text-[11px] text-ink-muted">Each assignment with a draft is graded again</span>
            </span>
          </label>
          <p className="mt-1.5">
            Each grading is one paid AI call. They run in the background, 3 at a time; you can follow them on the System page.
          </p>
        </ConfirmDialog>
      )}

      <div className="min-h-0 flex-1 overflow-y-auto bg-paper/60">
        <div className="flex flex-col gap-4 p-4 sm:p-5">
          {gradeStudent.isError && <ErrorPanel error={gradeStudent.error} />}
          {gradeStudent.data && (
            <Notice
              tone="success"
              title="Queued"
              onDismiss={() => gradeStudent.reset()}
              action={
                <Link to="/system" className="shrink-0 font-semibold text-accent hover:underline">
                  Follow on System →
                </Link>
              }
            >
              First {student.name}'s turned-in work is fetched from Classroom, then every assignment without a draft is graded.
            </Notice>
          )}
          <StatStrip>
            <Stat compact icon={<Send />} label="Turned in" value={`${student.turnedIn} / ${assignments.length}`} note="handed in" />
            <Stat compact icon={<CalendarX />} label="Missing" value={student.missing} note="count as 0" tone={student.missing > 0 ? "bad" : undefined} />
            <Stat compact icon={<Clock />} label="Late" value={student.late} note="after deadline" />
            <Stat compact icon={<TrendingUp />} label="Improved" value={student.improved} note="after a fix" tone={student.improved > 0 ? "good" : undefined} />
          </StatStrip>

          <div className="grid gap-4 @3xl:grid-cols-2">
            <StudentOverviewCard
              courseId={courseId}
              userId={student.externalUserId}
              studentName={student.name}
              returnTo={`/courses/${courseId}/students?student=${student.id}`}
              kind="Mistakes"
            />
            <StudentOverviewCard
              courseId={courseId}
              userId={student.externalUserId}
              studentName={student.name}
              returnTo={`/courses/${courseId}/students?student=${student.id}`}
              kind="Strengths"
            />
          </div>

          <div className="grid gap-4 @3xl:grid-cols-[minmax(0,1fr)_280px]">
            <div className="flex min-w-0 flex-col gap-4">
              <ScoreChart assignments={assignments} results={byAssignment} />

              <div className="surface">
                <div className="flex items-center justify-between border-b border-line px-4 py-2.5">
                  <h3 className="eyebrow">Assignments</h3>
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

              <MistakeHistoryCard courseId={courseId} userId={student.externalUserId} />
            </div>

            <aside className="flex flex-col gap-4">
              <StrengthsCard courseId={courseId} userId={student.externalUserId} />
              <MistakeCategoriesCard courseId={courseId} userId={student.externalUserId} />
              <ComingNext />
            </aside>
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
    <div className="surface">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-line px-4 py-2.5">
        <h3 className="eyebrow">Score by assignment</h3>
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
                      className="w-1/3 origin-bottom animate-[grow-y_700ms_cubic-bezier(0.22,1,0.36,1)_both] rounded-t bg-accent-tint ring-1 ring-accent/30"
                      style={{ height: `${(initial / top) * 100}%` }}
                      title={`First returned: ${points(result!.initialGrade!)}`}
                    />
                  )}
                  <span
                    className="w-1/2 origin-bottom animate-[grow-y_700ms_cubic-bezier(0.22,1,0.36,1)_both] rounded-t bg-accent"
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
      <button onClick={onOpen} className="flex w-full items-center gap-3 px-4 py-2.5 text-left transition-colors hover:bg-paper">
        <span className="min-w-0 flex-1">
          <span className="block truncate font-semibold">{assignment.title}</span>
          <span className="block text-[11px] text-ink-muted">
            {assignment.dueAt ? `Due ${formatDay(assignment.dueAt)}` : "No deadline"} · {assignment.maxPoints ?? "–"} points
          </span>
        </span>
        <span className="hidden shrink-0 gap-1.5 @lg:flex">
          {result?.missing ? <Chip tone="danger">Missing</Chip> : state && <Chip tone={state.tone}>{state.label}</Chip>}
          {result?.turnedIn && result.late && <Chip tone="warn">Late</Chip>}
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
      <span className="whitespace-nowrap font-mono text-[12px]">
        {changed && <span className="text-ink-muted">{points(result.initialGrade!)} → </span>}
        <b>{points(result.finalGrade)}</b>
        <span className="text-ink-muted"> / {maxPoints ?? "–"}</span>
      </span>
    );
  }
  if (result?.missing) {
    return <b className="font-mono text-[12px] text-danger">0</b>;
  }
  return <span className="text-xs text-ink-muted">{result?.turnedIn ? "No grade yet" : "–"}</span>;
}

function ComingNext() {
  const plans = [
    {
      title: "Fixed after a resubmission",
      text: "Mistakes that disappear when the student fixes the work and you approve the new version.",
    },
  ];

  return (
    <div className="flex flex-col gap-2">
      <h3 className="eyebrow">Coming next</h3>
      {plans.map((plan) => (
        <div key={plan.title} className="rounded-[10px] border border-dashed border-ink-faint/50 bg-white/60 px-4 py-3">
          <div className="flex items-center justify-between gap-2">
            <b className="text-xs">{plan.title}</b>
            <Chip tone="dashed">Planned</Chip>
          </div>
          <p className="mt-1 text-xs text-ink-muted">{plan.text}</p>
        </div>
      ))}
    </div>
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
  const state = result?.state ? stateChip[result.state] : undefined;
  const workspace = `/courses/${courseId}/assignments/${assignment.id}${result?.submissionId ? `?student=${encodeURIComponent(result.submissionId)}` : ""}`;

  return (
    <Modal
      title={assignment.title}
      subtitle={`${student.name} · ${assignment.dueAt ? `due ${formatDate(assignment.dueAt)}` : "no deadline"} · ${assignment.maxPoints ?? "–"} points`}
      label={`${student.name} · ${assignment.title}`}
      size="max-w-[520px]"
      onClose={onClose}
      footer={
        <Link to={workspace}>
          <Button small variant="primary">
            Open in the assignment workspace <ArrowRight className="h-3.5 w-3.5" />
          </Button>
        </Link>
      }
    >
      <div className="flex flex-col gap-4 px-5 py-4">
        <div className="flex flex-wrap items-center gap-2">
          {result?.missing ? <Chip tone="danger">Missing · counts as 0</Chip> : state && <Chip tone={state.tone}>{state.label}</Chip>}
          {result?.turnedIn && result.late && <Chip tone="warn">Late</Chip>}
          <span className="ml-auto text-lg">
            <Grade result={result} maxPoints={assignment.maxPoints} />
          </span>
        </div>

        <div>
          <h3 className="eyebrow mb-2">Grade history</h3>
          {result && result.returns.length > 0 ? (
            <ol className="relative flex flex-col gap-3 border-l-2 border-line pl-4">
              {result.returns.map((r, i) => (
                <li key={i} className="relative">
                  <span
                    className={`absolute -left-[22px] top-1 h-2.5 w-2.5 rounded-full border-2 border-white ${i === result.returns.length - 1 ? "bg-success" : "bg-accent"}`}
                  />
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
    </Modal>
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
  return (
    <Modal
      title="Grade table"
      subtitle={'Final grades; "70 → 95" when a fix raised it. Click a name to open the profile.'}
      size="max-w-[1200px]"
      height="max-h-[94dvh]"
      onClose={onClose}
    >
      <div className="overflow-auto">
        <table className="w-full border-collapse text-left">
          <thead className="sticky top-0 z-20">
            <tr className="border-b border-line bg-paper text-[11px] text-ink-muted">
              <th className="eyebrow sticky left-0 z-10 bg-paper px-4 py-2.5">Student</th>
              <th className="eyebrow px-3 py-2.5">Overall</th>
              {assignments.map((a) => (
                <th key={a.id} className="min-w-[110px] px-3 py-2.5 align-bottom font-semibold" title={a.title}>
                  <span className="line-clamp-2 text-ink">{a.title}</span>
                  <span className="block whitespace-nowrap font-mono text-[10.5px] font-normal">
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
                    <td className="sticky left-0 z-10 bg-white px-4 py-2 group-hover:bg-paper">
                      <button onClick={() => onPick(student)} className="font-semibold hover:text-accent hover:underline">
                        {student.name}
                      </button>
                    </td>
                    <td className="num whitespace-nowrap px-3 py-2 text-[12px]">
                      {student.overallPercent == null ? "–" : `${Math.round(student.overallPercent)}%`}
                    </td>
                    {assignments.map((a) => {
                      const result = byAssignment.get(a.id);
                      return (
                        <td key={a.id} className="px-3 py-2">
                          {result?.missing ? <Chip tone="danger">Missing · 0</Chip> : <Grade result={result} maxPoints={a.maxPoints} />}
                        </td>
                      );
                    })}
                  </tr>
                );
              })}
            </tbody>
        </table>
      </div>
    </Modal>
  );
}

function points(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(1);
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
    <Notice tone="success" title="Sync finished" onDismiss={onDismiss}>
      {parts.join(" · ")}
    </Notice>
  );
}
