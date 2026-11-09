import { type ReactNode, useState } from "react";
import { Link, useSearchParams } from "react-router";
import type { JobOverviewItem, JobStatus, JobType } from "../api/client";
import { useCourses, useJobAction, useJobOverview } from "../api/hooks";
import { Avatar, Button, Card, Chip, EmptyState, ErrorPanel, PageHeader, Spinner } from "../components/ui";
import { FilterChips } from "../components/FilterChips";
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
  { value: "GradeVersion", label: "AI grading" },
  { value: "GradeAssignment", label: "Grade all" },
  { value: "FetchAssignment", label: "Fetch" },
  { value: "SyncCourse", label: "Course sync" },
  { value: "ReExtract", label: "Re-extract" },
];

const jobTypeLabels: Record<string, string> = {
  GradeVersion: "AI grading",
  GradeAssignment: "Grade all",
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
      <nav className="flex gap-1 border-b border-line" aria-label="System sections">
        {(["queue", "usage"] as const).map((key) => (
          <button
            key={key}
            onClick={() => setSearchParams(key === "queue" ? {} : { tab: key }, { replace: true })}
            aria-pressed={tab === key}
            className={`-mb-px border-b-2 px-3 py-2 text-[13px] font-semibold ${
              tab === key ? "border-accent text-ink" : "border-transparent text-ink-muted hover:text-ink"
            }`}
          >
            {key === "queue" ? "Queue" : "Model usage"}
          </button>
        ))}
      </nav>
      {tab === "queue" ? <QueueView /> : <UsageView />}
    </>
  );
}

function QueueView() {
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
      {overview.isLoading && <Spinner />}

      {counts && (
        <div className="grid grid-cols-2 border-t border-line md:grid-cols-5">
          <Stat label="Running" value={counts.running} note="right now" />
          <Stat label="Queued" value={counts.queued} note="waiting for a free slot" />
          <Stat label="Succeeded" value={counts.succeeded} note="last 24 hours" />
          <Stat label="Failed" value={counts.failed} note="last 24 hours" tone={counts.failed > 0 ? "bad" : undefined} />
          <Stat label="Canceled" value={counts.canceled} note="last 24 hours" />
        </div>
      )}

      {overview.data && (
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
                  className={`inline-flex h-7 items-center gap-1.5 rounded-full border px-3 text-xs font-semibold ${
                    selected ? "border-accent bg-accent text-white" : "border-line bg-white text-ink hover:border-accent"
                  }`}
                >
                  {filter.label}
                  {count != null && <span className={selected ? "text-white/80" : "text-ink-muted"}>{count}</span>}
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
  const { job } = item;
  const active = job.status === "Queued" || job.status === "Running";
  const canRetry = job.status === "Failed" || job.status === "Canceled";
  const link =
    item.courseId && item.assignmentId && item.submissionId
      ? `/courses/${item.courseId}/assignments/${item.assignmentId}?student=${encodeURIComponent(item.submissionId)}`
      : undefined;

  return (
    <li className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-x-4 gap-y-2 px-[18px] py-3 @4xl:grid-cols-[110px_minmax(0,1fr)_150px_170px_auto]">
      <div className="@4xl:order-none order-3 col-span-2 flex flex-wrap items-center gap-1.5 @4xl:col-span-1">
        <StatusChip item={item} />
      </div>

      <div className="flex min-w-0 items-center gap-3">
        {item.studentName ? (
          <Avatar text={initials(item.studentName)} userId={item.studentId ?? undefined} />
        ) : (
          <span className="h-8 w-8 shrink-0 rounded-full bg-paper-dark" />
        )}
        <div className="min-w-0">
          <div className="truncate font-semibold">
            {jobTypeLabels[job.type] ?? job.type}
            {item.studentName && <span className="font-normal"> · {item.studentName}</span>}
          </div>
          <div className="truncate text-xs text-ink-muted">
            {[item.assignmentTitle, item.versionNumber != null ? `v${item.versionNumber}` : null, courseName].filter(Boolean).join(" · ") ||
              "—"}
          </div>
          {job.error && (
            <div className="mt-0.5 line-clamp-2 text-xs text-[#8f2d2d]" title={job.error}>
              {job.error}
            </div>
          )}
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
    </li>
  );
}

function StatusChip({ item }: { item: JobOverviewItem }) {
  const { job } = item;
  if (job.status === "Failed") {
    return <span className="inline-flex h-[22px] items-center rounded-full bg-[#8f2d2d] px-2.5 text-[11px] font-semibold text-white">Failed</span>;
  }
  if (job.status === "Running") {
    return <Chip tone="accent">{job.cancelRequested ? "Canceling…" : "Running"}</Chip>;
  }
  if (job.status === "Queued") {
    return <Chip tone="tint">{job.attempts > 0 ? "Retry queued" : "Queued"}</Chip>;
  }
  if (job.status === "Canceled") {
    return <Chip tone="muted">Canceled</Chip>;
  }
  return <Chip tone="outline">Succeeded</Chip>;
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

function Stat({ label, value, note, tone }: { label: string; value: ReactNode; note: string; tone?: "bad" }) {
  return (
    <div className="border-b border-line px-4 py-3 md:border-b-0 md:border-r md:last:border-r-0">
      <div className="text-xs text-ink-muted">{label}</div>
      <div className={`text-[22px] font-bold leading-tight ${tone === "bad" ? "text-[#8f2d2d]" : ""}`}>{value}</div>
      <div className="text-[11px] text-ink-muted">{note}</div>
    </div>
  );
}

function formatSeconds(ms: number): string {
  const seconds = Math.max(0, Math.round(ms / 1000));
  if (seconds < 60) {
    return `${seconds} s`;
  }
  return `${Math.floor(seconds / 60)} min ${seconds % 60} s`;
}
