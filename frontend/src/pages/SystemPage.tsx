import { createContext, type ReactNode, useContext, useState } from "react";
import { Link, useSearchParams } from "react-router";
import type { JobOverviewItem, JobStatus, JobType } from "../api/client";
import { useCourses, useJobAction, useJobChildren, useJobOverview } from "../api/hooks";
import { Avatar, Button, Card, EmptyState, ErrorPanel, PageHeader, SkeletonPage, SkeletonRows, Stat, StatStrip, Tabs } from "../components/ui";
import { FilterChips } from "../components/FilterChips";
import { JobDetailsModal, JobIcon, JobStatusChip } from "../components/JobDetails";
import { Ban, CircleCheck, CircleX, Clock, Cpu, ListChecks, Play } from "lucide-react";
import { UsageView } from "../components/UsageView";
import { formatAge, formatDate, initials } from "../format";

const filters: { key: JobStatus | undefined; label: string }[] = [
  { key: undefined, label: "All" },
  { key: "Running", label: "Running" },
  { key: "Queued", label: "Queued" },
  { key: "Failed", label: "Failed" },
  { key: "Succeeded", label: "Succeeded" },
  { key: "Canceled", label: "Canceled" },
];

const workOptions: { value: JobType | undefined; label: string }[] = [
  { value: undefined, label: "All work" },
  { value: "GradeVersion", label: "Single grading" },
  { value: "GradeAssignment", label: "Grade all" },
  { value: "GradeStudent", label: "Grade student" },
  { value: "FetchStudentWork", label: "Fetch student's work" },
  { value: "AnalyzeMistakes", label: "Common mistakes" },
  { value: "AnalyzeStudent", label: "Student analysis" },
  { value: "GenerateTests", label: "Test cases" },
  { value: "GenerateTestCode", label: "Test code" },
  { value: "AgentRun", label: "Agent" },
  { value: "EvaluationRun", label: "Evaluation run" },
  { value: "EvaluationGrade", label: "Evaluation grading" },
  { value: "FetchAssignment", label: "Fetch" },
  { value: "SyncCourse", label: "Course sync" },
  { value: "ReExtract", label: "Re-extract" },
];

const jobTypeLabels: Record<string, string> = {
  GradeVersion: "Single grading",
  GradeAssignment: "Grade all",
  GradeStudent: "Grade student",
  FetchStudentWork: "Fetch student's work",
  AnalyzeMistakes: "Common mistakes",
  AnalyzeStudent: "Student analysis",
  GenerateTests: "Test cases",
  GenerateTestCode: "Test code",
  AgentRun: "Agent",
  EvaluationRun: "Evaluation run",
  EvaluationGrade: "Evaluation grading",
  FetchAssignment: "Fetch",
  SyncCourse: "Course sync",
  ReExtract: "Re-extract",
};

export function SystemPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const tab = searchParams.get("tab") === "usage" ? "usage" : "queue";

  return (
    <>
      <PageHeader title="System" subtitle="What runs in the background and what it costs in model tokens" />
      <Tabs
        label="System sections"
        value={tab}
        onChange={(key) => setSearchParams(key === "queue" ? {} : { tab: key }, { replace: true })}
        tabs={[
          { value: "queue", label: "Queue", icon: <ListChecks /> },
          { value: "usage", label: "Model usage", icon: <Cpu /> },
        ]}
      />
      {tab === "queue" ? <QueueView /> : <UsageView />}
    </>
  );
}

const OpenJobContext = createContext<(item: JobOverviewItem) => void>(() => {});

function QueueView() {
  const [opened, setOpened] = useState<JobOverviewItem | null>(null);
  const [status, setStatus] = useState<JobStatus | undefined>(undefined);
  const [work, setWork] = useState<JobType | undefined>(undefined);
  const overview = useJobOverview(status, work);
  const action = useJobAction();
  const courses = useCourses();
  const courseNames = new Map((courses.data ?? []).map((c) => [c.id, c.name]));

  const counts = overview.data?.counts;
  const jobs = overview.data?.jobs ?? [];
  const active = counts ? counts.queued + counts.running : 0;

  function countFor(key: JobStatus | undefined) {
    if (!counts || key === undefined) {
      return null;
    }
    return { Queued: counts.queued, Running: counts.running, Succeeded: counts.succeeded, Failed: counts.failed, Canceled: counts.canceled }[key];
  }

  return (
    <>
      <FilterChips label="Work" options={workOptions} value={work} onChange={setWork} />
      <div className="text-xs text-ink-muted">
        {active > 0
          ? "Background jobs · updating every 2 seconds while something is queued or running"
          : "Background jobs · what was queued, ran, finished or failed"}
      </div>

      {overview.isError && <ErrorPanel error={overview.error} />}
      {action.isError && <ErrorPanel error={action.error} />}
      {overview.isLoading && <SkeletonPage stats={5} rows={6} />}

      {counts && (
        <StatStrip columns={5}>
          <Stat label="Running" value={counts.running} note="right now" icon={<Play />} tone={counts.running > 0 ? "mark" : undefined} />
          <Stat label="Queued" value={counts.queued} note="waiting for a free slot" icon={<Clock />} />
          <Stat label="Succeeded" value={counts.succeeded} note="last 24 hours" icon={<CircleCheck />} />
          <Stat label="Failed" value={counts.failed} note="last 24 hours" icon={<CircleX />} tone={counts.failed > 0 ? "bad" : undefined} />
          <Stat label="Canceled" value={counts.canceled} note="last 24 hours" icon={<Ban />} />
        </StatStrip>
      )}

      {opened && (
        <JobDetailsModal
          key={opened.job.id}
          item={opened}
          courseName={opened.courseId ? courseNames.get(opened.courseId) : undefined}
          onClose={() => setOpened(null)}
          onOpen={setOpened}
        />
      )}

      {overview.data && (
        <OpenJobContext.Provider value={setOpened}>
        <Card>
          <div className="flex flex-wrap items-center gap-1.5 border-b border-line px-[18px] py-3">
            {filters.map((filter) => {
              const count = countFor(filter.key);
              const selected = filter.key === status;
              return (
                <button
                  key={filter.label}
                  onClick={() => setStatus(filter.key)}
                  aria-pressed={selected}
                  className={`inline-flex h-7 items-center gap-1.5 rounded-md border px-2.5 text-xs font-medium transition-colors ${
                    selected
                      ? "border-accent bg-accent-tint font-semibold text-accent-strong shadow-[inset_0_0_0_1px_var(--color-accent)]"
                      : "border-line bg-white text-ink-soft hover:border-ink-faint hover:bg-paper"
                  }`}
                >
                  {filter.label}
                  {count != null && <span className={`font-mono text-[10.5px] ${selected ? "text-accent" : "text-ink-faint"}`}>{count}</span>}
                </button>
              );
            })}
            <span className="ml-auto text-[11px] text-ink-muted">newest first · up to 200</span>
          </div>

          {jobs.length === 0 ? (
            <EmptyState>
              {status || work
                ? `No ${status ? `${status.toLowerCase()} ` : ""}${work ? `${jobTypeLabels[work] ?? work} ` : ""}jobs.`
                : "No background jobs yet. Grading with AI queues one."}
            </EmptyState>
          ) : (
            <ul className="@container divide-y divide-line">
              {jobs.map((item) => (
                <JobRow
                  key={item.job.id}
                  item={item}
                  courseName={item.courseId ? courseNames.get(item.courseId) : undefined}
                  busy={action.isPending && action.variables?.jobId === item.job.id}
                  onCancel={() => action.mutate({ jobId: item.job.id, action: "cancel" })}
                  onRetry={() => action.mutate({ jobId: item.job.id, action: "retry" })}
                />
              ))}
            </ul>
          )}
        </Card>
        </OpenJobContext.Provider>
      )}
    </>
  );
}

function JobRow({
  item,
  courseName,
  busy,
  onCancel,
  onRetry,
}: {
  item: JobOverviewItem;
  courseName: string | undefined;
  busy: boolean;
  onCancel: () => void;
  onRetry: () => void;
}) {
  const [expanded, setExpanded] = useState(false);
  if (item.children) {
    return (
      <>
        <JobRowBody item={item} courseName={courseName} busy={busy} onCancel={onCancel} onRetry={onRetry}>
          <GroupProgress item={item} expanded={expanded} onToggle={() => setExpanded(!expanded)} />
        </JobRowBody>
        {expanded && <ChildJobs parentId={item.job.id} courseName={courseName} />}
      </>
    );
  }

  return <JobRowBody item={item} courseName={courseName} busy={busy} onCancel={onCancel} onRetry={onRetry} />;
}

function GroupProgress({ item, expanded, onToggle }: { item: JobOverviewItem; expanded: boolean; onToggle: () => void }) {
  const counts = item.children!;
  const finished = counts.succeeded + counts.failed + counts.canceled;
  const total = Math.max(counts.total, 1);

  return (
    <div className="mt-1.5 flex flex-col gap-1">
      <div className="flex h-1.5 w-full max-w-[280px] overflow-hidden rounded-full bg-paper-dark" aria-hidden>
        <div className="bar-fill h-full bg-success" style={{ width: `${(counts.succeeded / total) * 100}%` }} />
        <div className="h-full bg-danger" style={{ width: `${(counts.failed / total) * 100}%` }} />
        <div className="h-full bg-ink-muted/40" style={{ width: `${(counts.canceled / total) * 100}%` }} />
      </div>
      <div className="flex flex-wrap items-center gap-x-2 text-[11px] text-ink-muted">
        <span>
          <b className="text-ink">{finished}</b> of {counts.total} done
        </span>
        {counts.running > 0 && <span>· {counts.running} running</span>}
        {counts.queued > 0 && <span>· {counts.queued} waiting</span>}
        {counts.failed > 0 && <span className="font-semibold text-danger">· {counts.failed} failed</span>}
        {counts.canceled > 0 && <span>· {counts.canceled} canceled</span>}
        <button onClick={onToggle} aria-expanded={expanded} className="font-semibold text-accent hover:underline">
          {item.job.type === "GradeStudent"
            ? expanded
              ? "Hide assignments ▴"
              : `Show fetch + ${Math.max(counts.total - 1, 0)} assignments ▾`
            : expanded
              ? "Hide students ▴"
              : `Show ${counts.total} students ▾`}
        </button>
      </div>
    </div>
  );
}

function ChildJobs({ parentId, courseName }: { parentId: string; courseName: string | undefined }) {
  const children = useJobChildren(parentId, true);
  const action = useJobAction();

  if (children.isLoading) {
    return (
      <li className="bg-paper/60 pl-8">
        <SkeletonRows rows={2} />
      </li>
    );
  }

  return (
    <>
      {(children.data ?? []).map((child) => (
        <li key={child.job.id} className="border-l-4 border-accent/30 bg-paper/60">
          <JobRowBody
            item={child}
            courseName={courseName}
            busy={action.isPending && action.variables?.jobId === child.job.id}
            onCancel={() => action.mutate({ jobId: child.job.id, action: "cancel" })}
            onRetry={() => action.mutate({ jobId: child.job.id, action: "retry" })}
            nested
          />
        </li>
      ))}
    </>
  );
}

function JobRowBody({
  item,
  courseName,
  busy,
  onCancel,
  onRetry,
  nested,
  children,
}: {
  item: JobOverviewItem;
  courseName: string | undefined;
  busy: boolean;
  onCancel: () => void;
  onRetry: () => void;
  nested?: boolean;
  children?: ReactNode;
}) {
  const { job } = item;
  const openJob = useContext(OpenJobContext);
  const active = job.status === "Queued" || job.status === "Running";
  const canRetry = job.status === "Failed" || job.status === "Canceled";
  const link =
    item.courseId && item.assignmentId && item.submissionId
      ? `/courses/${item.courseId}/assignments/${item.assignmentId}?student=${encodeURIComponent(item.submissionId)}`
      : undefined;

  const Row = nested ? "div" : "li";

  return (
    <Row className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-x-4 gap-y-2 px-[18px] py-3 @4xl:grid-cols-[110px_minmax(0,1fr)_150px_170px_200px]">
      <div className="@4xl:order-none order-3 col-span-2 flex flex-wrap items-center gap-1.5 @4xl:col-span-1">
        <StatusChip item={item} />
      </div>

      <div className="flex min-w-0 items-center gap-3">
        <button onClick={() => openJob(item)} className="shrink-0 rounded-full" title="Show what this job did" aria-label="Show job details">
          {item.studentName ? <Avatar text={initials(item.studentName)} userId={item.studentId ?? undefined} /> : <JobIcon type={job.type} />}
        </button>
        <div className="min-w-0">
          <button onClick={() => openJob(item)} className="block max-w-full truncate text-left font-semibold hover:underline" title="Show what this job did">
            {nested ? "Grading" : (jobTypeLabels[job.type] ?? job.type)}
            {item.studentName && <span className="font-normal"> · {item.studentName}</span>}
          </button>
          <div className="truncate text-xs text-ink-muted">
            {[item.assignmentTitle, item.versionNumber != null ? `v${item.versionNumber}` : null, courseName].filter(Boolean).join(" · ") ||
              "—"}
          </div>
          {job.error && (
            <div className="mt-0.5 line-clamp-2 text-xs text-danger" title={job.error}>
              {job.error}
            </div>
          )}
          {children}
        </div>
      </div>

      <div className="order-4 col-span-2 text-xs text-ink-muted @4xl:order-none @4xl:col-span-1">
        <div>
          attempt {job.attempts} / {job.maxAttempts}
        </div>
        <div title={formatDate(job.createdAt)}>queued {formatAge(job.createdAt)} ago</div>
      </div>

      <div className="order-5 col-span-2 text-xs text-ink-muted @4xl:order-none @4xl:col-span-1">
        <Timing item={item} />
      </div>

      <div className="flex items-center justify-end gap-2">
        <Button small onClick={() => openJob(item)} title="What this job is, what it did and when">
          Details
        </Button>
        {active && !job.cancelRequested && (
          <Button small onClick={onCancel} disabled={busy}>
            Cancel
          </Button>
        )}
        {canRetry && (
          <Button small variant="primary" onClick={onRetry} disabled={busy} title="Queue it again as a new job">
            {busy ? "Queuing…" : "Retry"}
          </Button>
        )}
        {link && (
          <Link to={link} className="whitespace-nowrap text-xs font-semibold text-accent hover:underline">
            Open →
          </Link>
        )}
      </div>
    </Row>
  );
}

function StatusChip({ item }: { item: JobOverviewItem }) {
  const { job } = item;
  return <JobStatusChip status={job.status} cancelRequested={job.cancelRequested} retrying={job.status === "Queued" && job.attempts > 0} />;
}

function Timing({ item }: { item: JobOverviewItem }) {
  const { job } = item;
  if (job.status === "Queued") {
    const waiting = new Date(job.runAfter).getTime() > Date.now();
    return <span>{waiting ? `next try at ${formatDate(job.runAfter)}` : "waiting for a slot"}</span>;
  }
  if (job.status === "Running" && job.startedAt) {
    return <span>running for {formatSeconds(Date.now() - new Date(job.startedAt).getTime())}</span>;
  }
  if (job.startedAt && job.finishedAt) {
    return (
      <span title={formatDate(job.finishedAt)}>
        took {formatSeconds(new Date(job.finishedAt).getTime() - new Date(job.startedAt).getTime())} · finished{" "}
        {formatAge(job.finishedAt)} ago
      </span>
    );
  }
  return <span>{job.finishedAt ? `finished ${formatAge(job.finishedAt)} ago` : "—"}</span>;
}


function formatSeconds(ms: number): string {
  const seconds = Math.max(0, Math.round(ms / 1000));
  if (seconds < 60) {
    return `${seconds} s`;
  }
  return `${Math.floor(seconds / 60)} min ${seconds % 60} s`;
}
