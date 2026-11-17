import { useEffect, useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { Link, useParams, useSearchParams } from "react-router";
import type { AssignmentGrading } from "../api/client";
import { useAssignments, useCourseGradings, useGradingReport, useVersionGradings } from "../api/hooks";
import { CodeBrowser, type CodeFocus } from "../components/CodeBrowser";
import { DiscardDraftButton } from "../components/DiscardDraftButton";
import { ExportPdfLink } from "../components/ExportPdfLink";
import { BuildCheck } from "../components/BuildCheck";
import { CourseName } from "../components/Layout";
import { keptMistakes, keptStrengths, ReportChip, ReportEditor } from "../components/ReportEditor";
import { MistakeList, StrengthList } from "../components/SubmissionDetail";
import { Avatar, Button, Card, Chip, EmptyState, ErrorPanel, PageHeader, Spinner, Stat, StatStrip } from "../components/ui";
import { formatDate, initials } from "../format";
import { ArrowLeft, ChevronRight, ClipboardList, ExternalLink, Gauge, Pencil, Sparkles, Users } from "lucide-react";
import { severityColor } from "../theme";

export function DraftsPage() {
  const { courseId = "" } = useParams();
  const [searchParams, setSearchParams] = useSearchParams();
  const assignmentId = searchParams.get("assignment") ?? undefined;
  const studentId = searchParams.get("student") ?? undefined;
  const viewAll = searchParams.get("view") === "all";
  const overview = !assignmentId && !studentId && !viewAll;
  const [showAll, setShowAll] = useState(false);
  const [showDraft, setShowDraft] = useState(false);

  const assignments = useAssignments(courseId);
  const everything = useCourseGradings(courseId);
  const list = useCourseGradings(courseId, { assignmentId, studentId, all: showAll });

  const titles = new Map((assignments.data ?? []).map((a) => [a.id, a.title]));
  const allDrafts = everything.data ?? [];
  const rows = list.data ?? [];
  const selected = rows.find((d) => d.id === searchParams.get("draft")) ?? rows[0];

  function setParam(key: string, value: string | undefined) {
    setSearchParams((params) => {
      if (value) {
        params.set(key, value);
      } else {
        params.delete(key);
      }
      if (!params.get("assignment") && !params.get("student")) {
        params.set("view", "all");
      }
      params.delete("draft");
      return params;
    }, { replace: true });
  }

  function choose(filter: { assignment?: string; student?: string }) {
    setSearchParams(() => {
      const params = new URLSearchParams();
      if (filter.assignment) {
        params.set("assignment", filter.assignment);
      } else if (filter.student) {
        params.set("student", filter.student);
      } else {
        params.set("view", "all");
      }
      return params;
    });
  }

  function open(draft: AssignmentGrading) {
    setSearchParams((params) => {
      params.set("draft", draft.id);
      return params;
    }, { replace: true });
    setShowDraft(true);
  }

  const assignmentCounts = countBy(allDrafts.filter((d) => !studentId || d.studentId === studentId), (d) => d.assignmentId);
  const studentCounts = countBy(allDrafts.filter((d) => !assignmentId || d.assignmentId === assignmentId), (d) => d.studentId);
  const assignmentOptions = (assignments.data ?? []).filter((a) => allDrafts.some((d) => d.assignmentId === a.id));
  const studentOptions = uniqueStudents(allDrafts);
  const scored = allDrafts.filter((d) => d.score != null && d.maxScore);
  const average = scored.length > 0 ? scored.reduce((sum, d) => sum + (d.score! / d.maxScore!) * 100, 0) / scored.length : null;

  return (
    <div className="flex flex-col gap-3 lg:h-[calc(100dvh-72px)] lg:min-h-[640px]">
      <PageHeader
        title="AI drafts"
        subtitle={
          <>
            <CourseName courseId={courseId} /> · reviews drafted by the AI, saved for you to check, edit and approve
          </>
        }
        actions={
          !overview && (
            <Button icon={<ArrowLeft />} onClick={() => setSearchParams(new URLSearchParams())} title="Back to choosing an assignment or a student">
              Overview
            </Button>
          )
        }
      />

      {everything.isError && <ErrorPanel error={everything.error} />}
      {everything.isLoading && <Spinner />}

      {everything.data && allDrafts.length === 0 && (
        <Card className="flex flex-1 items-center justify-center">
          <EmptyState icon={<Sparkles />}>
            <div className="font-semibold text-ink">No drafts yet</div>
            Open an assignment and use "Grade with AI" on a student; every grading is saved here as a draft.
          </EmptyState>
        </Card>
      )}

      {allDrafts.length > 0 && (
        <>
          <StatStrip>
            <Stat compact icon={<Sparkles />} label="Drafts" value={allDrafts.length} note="waiting for your review" />
            <Stat compact icon={<ClipboardList />} label="Assignments" value={assignmentOptions.length} note="with drafts" />
            <Stat compact icon={<Users />} label="Students" value={studentOptions.length} note="with drafts" />
            <Stat compact icon={<Gauge />} label="Average AI score" value={average == null ? "–" : `${Math.round(average)}%`} note="of the maximum, before bonus" />
          </StatStrip>

          {overview ? (
            <DraftsOverview
              drafts={allDrafts}
              assignments={assignmentOptions}
              students={studentOptions}
              onChoose={choose}
            />
          ) : (
          <Card className="@container/workspace flex h-[calc(100dvh-96px)] min-h-[460px] shrink-0 overflow-hidden lg:h-auto lg:min-h-[420px] lg:flex-1 lg:shrink">
            <section
              className={`w-full shrink-0 flex-col bg-white @2xl/workspace:flex @2xl/workspace:w-[300px] @2xl/workspace:border-r @2xl/workspace:border-line @7xl/workspace:w-[340px] ${
                showDraft ? "hidden" : "flex"
              }`}
            >
              <div className="flex shrink-0 flex-col gap-2 border-b border-line p-3">
                <FocusHeader
                  student={studentId ? studentOptions.find((x) => x.id === studentId) : undefined}
                  assignmentTitle={!studentId && assignmentId ? titles.get(assignmentId) : undefined}
                  drafts={allDrafts.filter((d) => (!studentId || d.studentId === studentId) && (studentId || !assignmentId || d.assignmentId === assignmentId))}
                />
                {studentId ? (
                  assignmentOptions.filter((a) => (assignmentCounts.get(a.id) ?? 0) > 0).length > 1 && (
                    <select
                      value={assignmentId ?? ""}
                      onChange={(e) => setParam("assignment", e.target.value || undefined)}
                      aria-label="Filter by assignment"
                      className="field px-1.5 font-medium"
                    >
                      <option value="">All their assignments</option>
                      {assignmentOptions
                        .filter((a) => (assignmentCounts.get(a.id) ?? 0) > 0)
                        .map((a) => (
                          <option key={a.id} value={a.id}>
                            {a.title} ({assignmentCounts.get(a.id)})
                          </option>
                        ))}
                    </select>
                  )
                ) : assignmentId ? null : (
                  <div className="grid grid-cols-1 gap-2">
                    <select
                      value=""
                      onChange={(e) => setParam("assignment", e.target.value || undefined)}
                      aria-label="Filter by assignment"
                      className="field px-1.5 font-medium"
                    >
                      <option value="">All assignments</option>
                      {assignmentOptions.map((a) => (
                        <option key={a.id} value={a.id}>
                          {a.title} ({assignmentCounts.get(a.id) ?? 0})
                        </option>
                      ))}
                    </select>
                    <select
                      value=""
                      onChange={(e) => setParam("student", e.target.value || undefined)}
                      aria-label="Filter by student"
                      className="field px-1.5 font-medium"
                    >
                      <option value="">All students</option>
                      {studentOptions.map((x) => (
                        <option key={x.id} value={x.id}>
                          {x.name} ({studentCounts.get(x.id) ?? 0})
                        </option>
                      ))}
                    </select>
                  </div>
                )}
                <div className="flex items-center justify-between gap-2">
                  <span className="truncate text-xs text-ink-muted">
                    {rows.length} {rows.length === 1 ? "draft" : "drafts"} shown
                  </span>
                  <label className="flex shrink-0 cursor-pointer items-center gap-1.5 text-xs text-ink-muted">
                    <input type="checkbox" checked={showAll} onChange={(e) => setShowAll(e.target.checked)} className="accent-accent" />
                    Earlier & discarded
                  </label>
                </div>
              </div>

              {list.isLoading ? (
                <div className="p-4">
                  <Spinner />
                </div>
              ) : rows.length === 0 ? (
                <div className="px-4 py-8 text-center text-xs text-ink-muted">No drafts match these filters.</div>
              ) : (
                <ul className="min-h-0 flex-1 overflow-y-auto" aria-label="Drafts">
                  {rows.map((draft) => (
                    <DraftRow
                      key={draft.id}
                      draft={draft}
                      leadWithAssignment={studentId !== undefined}
                      assignmentTitle={titles.get(draft.assignmentId) ?? "Assignment"}
                      selected={draft.id === selected?.id}
                      onOpen={() => open(draft)}
                    />
                  ))}
                </ul>
              )}
            </section>

            <div className={`min-w-0 flex-1 ${showDraft ? "flex" : "hidden @2xl/workspace:flex"}`}>
              {selected ? (
                <DraftDetail
                  key={selected.id}
                  courseId={courseId}
                  draft={selected}
                  assignmentTitle={titles.get(selected.assignmentId) ?? "Assignment"}
                  onBack={() => setShowDraft(false)}
                />
              ) : (
                <div className="flex flex-1 items-center justify-center text-ink-muted">Pick a draft.</div>
              )}
            </div>
          </Card>
          )}
        </>
      )}
    </div>
  );
}

type Summary = { count: number; average: number | null; latest: string | null; major: number };

function summarize(drafts: AssignmentGrading[]): Summary {
  const scored = drafts.filter((d) => d.score != null && d.maxScore);
  return {
    count: drafts.length,
    average: scored.length > 0 ? scored.reduce((sum, d) => sum + (d.score! / d.maxScore!) * 100, 0) / scored.length : null,
    latest: drafts.reduce<string | null>((max, d) => (max == null || d.createdAt > max ? d.createdAt : max), null),
    major: drafts.reduce((sum, d) => sum + d.major, 0),
  };
}

function DraftsOverview({
  drafts,
  assignments,
  students,
  onChoose,
}: {
  drafts: AssignmentGrading[];
  assignments: { id: string; title: string; dueAt?: string | null }[];
  students: { id: string; name: string }[];
  onChoose: (filter: { assignment?: string; student?: string }) => void;
}) {
  return (
    <div className="grid min-h-0 flex-1 gap-3 lg:grid-cols-2 lg:grid-rows-[minmax(0,1fr)_auto]">
      <OverviewCard
        title="By assignment"
        subtitle="Grading is done per assignment: open one to go through its drafts student by student."
        rows={assignments.map((a) => {
          const own = drafts.filter((d) => d.assignmentId === a.id);
          return {
            id: a.id,
            label: a.title,
            detail: `${plural(new Set(own.map((d) => d.studentId)).size, "student")}${a.dueAt ? ` · due ${formatDate(a.dueAt)}` : ""}`,
            summary: summarize(own),
            onOpen: () => onChoose({ assignment: a.id }),
          };
        })}
      />
      <OverviewCard
        title="By student"
        subtitle="Everything drafted for one student, across assignments."
        avatars
        rows={students.map((s) => {
          const own = drafts.filter((d) => d.studentId === s.id);
          return {
            id: s.id,
            label: s.name,
            detail: plural(new Set(own.map((d) => d.assignmentId)).size, "assignment"),
            summary: summarize(own),
            onOpen: () => onChoose({ student: s.id }),
          };
        })}
      />
      <button
        onClick={() => onChoose({})}
        className="rounded-[10px] border border-dashed border-line bg-white/60 px-4 py-3 text-left text-xs font-semibold text-accent hover:border-accent lg:col-span-2"
      >
        Or see all {drafts.length} drafts in one list →
      </button>
    </div>
  );
}

function OverviewCard({
  title,
  subtitle,
  rows,
  avatars,
}: {
  title: string;
  subtitle: string;
  rows: { id: string; label: string; detail: string; summary: Summary; onOpen: () => void }[];
  avatars?: boolean;
}) {
  return (
    <Card className="flex min-h-0 flex-col overflow-hidden">
      <div className="shrink-0 border-b border-line px-4 py-3">
        <h3 className="text-sm font-bold">{title}</h3>
        <div className="text-xs text-ink-muted">{subtitle}</div>
      </div>
      <ul className="min-h-0 flex-1 divide-y divide-paper-dark overflow-y-auto">
        {rows.map((row) => (
          <li key={row.id}>
            <button onClick={row.onOpen} className="group flex w-full items-center gap-3 px-4 py-3 text-left transition-colors hover:bg-paper">
              {avatars && <Avatar text={initials(row.label)} userId={row.id} />}
              <span className="min-w-0 flex-1">
                <span className="block truncate font-semibold">{row.label}</span>
                <span className="block truncate text-[11px] text-ink-muted">
                  {row.detail}
                  {row.summary.latest && ` · last ${formatDate(row.summary.latest)}`}
                </span>
              </span>
              <span className="hidden shrink-0 text-right sm:block">
                <b className="num block text-[13px]">{row.summary.average == null ? "–" : `${Math.round(row.summary.average)}%`}</b>
                <span className="text-[11px] text-ink-muted">avg AI score</span>
              </span>
              <span className="flex w-20 shrink-0 flex-col items-end">
                <b className="num text-[13px]">{row.summary.count}</b>
                <span className="text-[11px] text-ink-muted">
                  {row.summary.count === 1 ? "draft" : "drafts"}
                  {row.summary.major > 0 && ` · ${row.summary.major} major`}
                </span>
              </span>
              <ChevronRight className="h-4 w-4 shrink-0 text-ink-faint transition-transform group-hover:translate-x-0.5 group-hover:text-accent" />
            </button>
          </li>
        ))}
      </ul>
    </Card>
  );
}

function FocusHeader({
  student,
  assignmentTitle,
  drafts,
}: {
  student: { id: string; name: string } | undefined;
  assignmentTitle: string | undefined;
  drafts: AssignmentGrading[];
}) {
  const assignments = new Set(drafts.map((d) => d.assignmentId)).size;
  const students = new Set(drafts.map((d) => d.studentId)).size;

  if (student) {
    return (
      <div className="flex items-center gap-2.5">
        <Avatar text={initials(student.name)} userId={student.id} />
        <div className="min-w-0">
          <b className="block truncate text-sm">{student.name}</b>
          <span className="text-[11px] text-ink-muted">
            {plural(drafts.length, "draft")} · {plural(assignments, "assignment")}
          </span>
        </div>
      </div>
    );
  }
  if (assignmentTitle) {
    return (
      <div className="min-w-0">
        <b className="block truncate text-sm">{assignmentTitle}</b>
        <span className="text-[11px] text-ink-muted">
          {plural(drafts.length, "draft")} · {plural(students, "student")}
        </span>
      </div>
    );
  }
  return (
    <div className="min-w-0">
      <b className="block text-sm">All drafts</b>
      <span className="text-[11px] text-ink-muted">
        {plural(drafts.length, "draft")} · {plural(assignments, "assignment")} · {plural(students, "student")}
      </span>
    </div>
  );
}

function DraftRow({
  draft,
  leadWithAssignment,
  assignmentTitle,
  selected,
  onOpen,
}: {
  draft: AssignmentGrading;
  leadWithAssignment: boolean;
  assignmentTitle: string;
  selected: boolean;
  onOpen: () => void;
}) {
  return (
    <li className="border-b border-paper-dark">
      <button
        onClick={onOpen}
        aria-current={selected}
        className={`flex w-full items-start gap-2.5 border-l-[3px] py-2.5 pl-3 pr-3 text-left ${
          selected ? "border-l-accent bg-accent-tint/60" : "border-l-transparent hover:bg-paper"
        } ${draft.status === "Discarded" ? "text-ink-muted" : ""}`}
      >
        {!leadWithAssignment && <Avatar text={initials(draft.studentName)} userId={draft.studentId} />}
        <span className="min-w-0 flex-1">
          <span className="block truncate font-semibold">{leadWithAssignment ? assignmentTitle : draft.studentName}</span>
          <span className="block truncate text-[11px] text-ink-muted">
            {leadWithAssignment ? "" : `${assignmentTitle} · `}v{draft.versionNumber} · {formatDate(draft.createdAt)}
          </span>
          <span className="mt-1 flex">
            <SeverityCounts draft={draft} />
          </span>
        </span>
        <span className="flex shrink-0 flex-col items-end gap-1">
          <Score draft={draft} />
          {draft.status === "Discarded" ? (
            <Chip tone="muted">Discarded</Chip>
          ) : draft.reportStatus === "Approved" ? (
            <Chip tone="success">Approved</Chip>
          ) : draft.reportStatus === "InReview" ? (
            <Chip tone="mark">In review</Chip>
          ) : (
            !draft.current && <Chip tone="dashed">Earlier</Chip>
          )}
        </span>
      </button>
    </li>
  );
}

function DraftDetail({
  courseId,
  draft,
  assignmentTitle,
  onBack,
}: {
  courseId: string;
  draft: AssignmentGrading;
  assignmentTitle: string;
  onBack: () => void;
}) {
  const history = useVersionGradings(draft.versionId);
  const grading = history.data?.find((g) => g.id === draft.id);
  const report = useGradingReport(draft.id).data;
  const [editing, setEditing] = useState(false);
  const [showOriginal, setShowOriginal] = useState(false);
  const showReport = report != null && !showOriginal;
  const mistakes = showReport ? keptMistakes(report) : grading?.mistakes ?? [];
  const strengths = showReport ? keptStrengths(report) : grading?.strengths ?? [];
  const [focus, setFocus] = useState<CodeFocus>();
  const [tab, setTab] = useState<"review" | "code">("review");
  const workspace = `/courses/${courseId}/assignments/${draft.assignmentId}?student=${encodeURIComponent(draft.submissionId)}`;

  useEffect(() => {
    setTab("review");
    setEditing(false);
    setShowOriginal(false);
  }, [draft.id]);

  return (
    <section className="@container flex min-h-0 min-w-0 flex-1 flex-col">
      <header className="flex flex-wrap items-center justify-between gap-3 border-b border-line bg-white px-4 py-2.5 sm:px-5">
        <button onClick={onBack} className="flex w-full items-center gap-1 text-left text-xs font-semibold text-accent @2xl/workspace:hidden">
          <ArrowLeft className="h-3.5 w-3.5" /> All drafts
        </button>
        <div className="flex min-w-0 items-center gap-3">
          <Avatar text={initials(draft.studentName)} userId={draft.studentId} size="lg" />
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <b className="truncate font-display text-[19px] font-semibold leading-tight">{draft.studentName}</b>
              {draft.status === "Discarded" ? (
                <Chip tone="muted">Discarded</Chip>
              ) : draft.current ? (
                <Chip tone="info">Current draft</Chip>
              ) : (
                <Chip tone="dashed">Earlier draft</Chip>
              )}
              {report && draft.status !== "Discarded" && <ReportChip report={report} />}
            </div>
            <div className="truncate text-xs text-ink-muted">
              {assignmentTitle} · <span className="font-mono text-[11px]">v{draft.versionNumber}</span> · saved {formatDate(draft.createdAt)} ·{" "}
              <span className="font-mono text-[11px]">{draft.model}</span>
            </div>
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <span className="mr-1 [&_b]:text-[20px] [&_span]:text-[13px]">
            <Score draft={showReport ? { score: report.score, maxScore: draft.maxScore, bonus: report.bonus } : draft} />
          </span>
          <ExportPdfLink gradingId={draft.id} />
          {draft.status === "Draft" && (
            <DiscardDraftButton gradingId={draft.id} reportStatus={report?.status} title="Throw this draft away; it stays in the history" />
          )}
          <Link to={workspace}>
            <Button small icon={<ExternalLink />} title="Open the student in the assignment workspace (files, versions, grade again)">
              Open in assignment
            </Button>
          </Link>
          {draft.status === "Draft" && grading && (
            <Button
              small
              icon={<Pencil />}
              variant={report?.status === "Approved" ? "outline" : "primary"}
              onClick={() => setEditing(true)}
              title="Correct the score, feedback and mistakes; saved as your report, the AI draft stays as it was"
            >
              {report ? "Edit report" : "Review & approve"}
            </Button>
          )}
        </div>
      </header>

      {history.isLoading || !grading ? (
        <div className="p-5">{history.isError ? <ErrorPanel error={history.error} /> : <Spinner label="Loading the draft…" />}</div>
      ) : (
        <div className="flex min-h-0 flex-1 flex-col @4xl:flex-row">
          <nav className="flex shrink-0 gap-1 border-b border-line bg-white px-4 sm:px-5 @4xl:hidden" aria-label="Review or code">
            {(["review", "code"] as const).map((key) => (
              <button
                key={key}
                onClick={() => setTab(key)}
                aria-pressed={tab === key}
                className={`-mb-px border-b-2 px-3 py-2 text-xs font-semibold ${
                  tab === key ? "border-accent text-accent-strong" : "border-transparent text-ink-muted hover:text-ink"
                }`}
              >
                {key === "review" ? `Review · ${mistakes.length} mistakes` : "Code"}
              </button>
            ))}
          </nav>

          <div
            className={`min-h-0 overflow-y-auto bg-paper/60 @4xl:block @4xl:w-[44%] @4xl:max-w-[560px] @4xl:flex-none @4xl:border-r @4xl:border-line ${
              tab === "review" ? "block flex-1" : "hidden"
            }`}
          >
            <div className="flex flex-col gap-4 p-4 sm:p-5">
              {report && (
                <div className="flex flex-wrap items-center gap-2 text-xs">
                  <h3 className="eyebrow">
                    {showReport ? "Your report" : "AI draft"}
                  </h3>
                  <span className="text-ink-muted">
                    {showReport
                      ? report.status === "Approved"
                        ? `approved ${formatDate(report.approvedAt)}`
                        : `saved ${formatDate(report.updatedAt)}`
                      : "as Claude wrote it"}
                  </span>
                  <button onClick={() => setShowOriginal(!showOriginal)} className="font-semibold text-accent hover:underline">
                    {showOriginal ? "Show your report" : "AI original"}
                  </button>
                </div>
              )}
              <div className="markdown surface px-5 py-4">
                <ReactMarkdown remarkPlugins={[remarkGfm]}>{showReport ? report.review : grading.review}</ReactMarkdown>
              </div>
              <MistakeList
                mistakes={mistakes}
                onShow={(file, line) => {
                  setFocus({ file, line, key: Date.now() });
                  setTab("code");
                }}
              />
              <StrengthList
                strengths={strengths}
                onShow={(file, line) => {
                  setFocus({ file, line, key: Date.now() });
                  setTab("code");
                }}
              />
              <BuildCheck
                status={grading.buildStatus}
                output={grading.buildOutput}
                styleFindings={grading.styleFindings}
                styleOutput={grading.styleOutput}
                pmdFindings={grading.pmdFindings}
                pmdOutput={grading.pmdOutput}
              />
              <details className="text-xs">
                <summary className="cursor-pointer font-semibold text-ink-muted hover:text-ink">
                  Files sent to the model · {grading.sentFiles.length}
                  {grading.skippedFiles.length > 0 && ` (${grading.skippedFiles.length} not sent)`}
                </summary>
                <ul className="mt-1.5 flex flex-col gap-0.5 text-ink-muted">
                  {grading.sentFiles.map((item) => (
                    <li key={item}>{item}</li>
                  ))}
                  {grading.skippedFiles.map((item) => (
                    <li key={item} className="line-through">
                      {item}
                    </li>
                  ))}
                </ul>
              </details>
              <p className="text-[11px] text-ink-muted">
                Instructions v{grading.instructionsVersion} · never sent to Classroom
              </p>
            </div>
          </div>

          <div className={`min-h-0 min-w-0 flex-1 flex-col p-2 sm:p-3 @4xl:flex ${tab === "code" ? "flex" : "hidden"}`}>
            <CodeBrowser versionId={draft.versionId} className="h-full" mistakes={mistakes} strengths={strengths} focus={focus} />
          </div>
        </div>
      )}
      {editing && grading && (
        <ReportEditor grading={grading} report={report} studentName={draft.studentName} onClose={() => setEditing(false)} />
      )}
    </section>
  );
}

function plural(count: number, word: string): string {
  return `${count} ${word}${count === 1 ? "" : "s"}`;
}

function points(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(1);
}

function Score({ draft }: { draft: Pick<AssignmentGrading, "score" | "maxScore" | "bonus"> }) {
  if (draft.score == null) {
    return <span className="text-xs text-ink-muted">score in text</span>;
  }

  return (
    <span className="whitespace-nowrap font-mono text-[12px]">
      <b className="font-semibold">{points(draft.score)}</b>
      <span className="text-ink-faint">/{draft.maxScore ?? "–"}</span>
      {draft.bonus ? <span className="font-semibold text-success"> +{points(draft.bonus)}</span> : null}
    </span>
  );
}

const severityDot: Record<"major" | "minor" | "style", string> = { major: severityColor.Major, minor: severityColor.Minor, style: severityColor.Style };

function SeverityCounts({ draft }: { draft: AssignmentGrading }) {
  const parts = (["major", "minor", "style"] as const).filter((key) => draft[key] > 0);
  if (parts.length === 0) {
    return <span className="text-[11px] text-ink-muted">No mistakes</span>;
  }

  return (
    <span className="flex flex-wrap gap-2 text-[11px] text-ink-muted">
      {parts.map((key) => (
        <span key={key} className="flex items-center gap-1" title={key}>
          <span className="h-2 w-2 rounded-full" style={{ backgroundColor: severityDot[key] }} />
          {draft[key]} {key}
        </span>
      ))}
    </span>
  );
}


function countBy<T>(items: T[], key: (item: T) => string): Map<string, number> {
  const counts = new Map<string, number>();
  for (const item of items) {
    counts.set(key(item), (counts.get(key(item)) ?? 0) + 1);
  }
  return counts;
}

function uniqueStudents(drafts: AssignmentGrading[]): { id: string; name: string }[] {
  const byId = new Map<string, string>();
  for (const draft of drafts) {
    byId.set(draft.studentId, draft.studentName);
  }
  return [...byId.entries()].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
}
