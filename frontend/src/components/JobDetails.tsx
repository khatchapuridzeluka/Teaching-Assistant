import { type ReactNode, useEffect } from "react";
import { Link } from "react-router";
import type { JobOverviewItem, JobType } from "../api/client";
import { useJob, useJobAction, useJobChildren } from "../api/hooks";
import { formatDate, initials } from "../format";
import { Avatar, Button, Chip, Spinner } from "./ui";

export const jobTypeInfo: Record<JobType, { label: string; description: string }> = {
  GradeVersion: {
    label: "Single grading",
    description:
      "An AI grading of one student's submitted version. It sends the assignment's instructions and the student's files (no name) to the grading model and saves the answer as a draft for you to review.",
  },
  GradeAssignment: {
    label: "Grade all",
    description:
      "Grade all for one assignment. It queued one grading for every student whose latest version had no draft yet and makes no AI call itself; it finishes when the last of its gradings finishes.",
  },
  GradeStudent: {
    label: "Grade student",
    description:
      "Grades all assignments of one student in two steps: first it fetches everything the student turned in from Google Classroom, then it queues one grading for every assignment that has no draft yet. It makes no AI call itself and finishes when the last of its gradings finishes.",
  },
  FetchStudentWork: {
    label: "Fetch student's work",
    description:
      "The first step of Grade student. It downloads the new and changed submissions of every assignment the student turned in from Google Classroom (read-only), then queues the gradings under the same Grade student job. It makes no AI call.",
  },
  AnalyzeMistakes: {
    label: "Common mistakes",
    description:
      "An analysis of one assignment's mistakes. It sends the instructions, the draft scores (as S1, S2, …) and every mistake (no names) to the model in one call, which writes the class overview and groups the mistakes; the result is shown on the Common mistakes page.",
  },
  FetchAssignment: {
    label: "Fetch",
    description: "Downloads an assignment's new and changed submissions from Google Classroom (planned on the queue).",
  },
  SyncCourse: {
    label: "Course sync",
    description: "Syncs a course's students and returned grades from Google Classroom (planned on the queue).",
  },
  ReExtract: {
    label: "Re-extract",
    description: "Rebuilds the extracted code of stored submissions without downloading anything (planned on the queue).",
  },
};

const statusText: Record<string, string> = {
  Queued: "Waiting for a free slot in the background worker.",
  Running: "Running in the background now.",
  Succeeded: "Finished successfully.",
  Failed: "Finished with an error; nothing more will be tried unless you retry it.",
  Canceled: "Canceled before it finished.",
};

export function JobIcon({ type }: { type: JobType }) {
  return (
    <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-accent text-white" aria-hidden>
      <svg viewBox="0 0 24 24" className="h-[17px] w-[17px]" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
        {iconPaths[type]}
      </svg>
    </span>
  );
}

const iconPaths: Record<JobType, ReactNode> = {
  GradeVersion: (
    <>
      <path d="M14 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9z" />
      <path d="M14 3v6h6" />
      <path d="m9 15 2 2 4-4" />
    </>
  ),
  GradeAssignment: (
    <>
      <circle cx="9" cy="8" r="3" />
      <path d="M3 20a6 6 0 0 1 12 0" />
      <circle cx="17" cy="9" r="2.5" />
      <path d="M15.5 14.3A5 5 0 0 1 21 19" />
    </>
  ),
  GradeStudent: (
    <>
      <circle cx="12" cy="8" r="3.5" />
      <path d="M5 20a7 7 0 0 1 14 0" />
    </>
  ),
  FetchStudentWork: (
    <>
      <circle cx="9" cy="7" r="3" />
      <path d="M3 19a6 6 0 0 1 9-5.2" />
      <path d="M18 11v8" />
      <path d="m15 16 3 3 3-3" />
    </>
  ),
  AnalyzeMistakes: (
    <>
      <path d="M4 20V10" />
      <path d="M10 20V4" />
      <path d="M16 20v-7" />
      <path d="M22 20H2" />
    </>
  ),
  FetchAssignment: (
    <>
      <path d="M12 3v12" />
      <path d="m7 10 5 5 5-5" />
      <path d="M4 21h16" />
    </>
  ),
  SyncCourse: (
    <>
      <path d="M20 11a8 8 0 0 0-14.6-4.5L4 8" />
      <path d="M4 3v5h5" />
      <path d="M4 13a8 8 0 0 0 14.6 4.5L20 16" />
      <path d="M20 21v-5h-5" />
    </>
  ),
  ReExtract: (
    <>
      <path d="M3 7h18v13H3z" />
      <path d="M8 7V4h8v3" />
      <path d="M10 12h4" />
    </>
  ),
};

export function JobDetailsModal({
  item,
  courseName,
  onClose,
  onOpen,
}: {
  item: JobOverviewItem;
  courseName: string | undefined;
  onClose: () => void;
  onOpen: (item: JobOverviewItem) => void;
}) {
  const job = useJob(item.job.id).data ?? item.job;
  const action = useJobAction();
  const info = jobTypeInfo[job.type];
  const isGroup = job.type === "GradeAssignment" || job.type === "GradeStudent";
  const children = useJobChildren(job.id, isGroup);
  const active = job.status === "Queued" || job.status === "Running";
  const canRetry = job.status === "Failed" || job.status === "Canceled";

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        onClose();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  const waited = job.startedAt ? duration(job.createdAt, job.startedAt) : null;
  const ran = job.startedAt ? duration(job.startedAt, job.finishedAt ?? new Date().toISOString()) : null;
  const submissionLink =
    item.courseId && item.assignmentId && item.submissionId
      ? `/courses/${item.courseId}/assignments/${item.assignmentId}?student=${encodeURIComponent(item.submissionId)}`
      : undefined;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-ink/40 p-3 sm:p-6" onClick={(e) => e.target === e.currentTarget && onClose()}>
      <div role="dialog" aria-modal="true" aria-label="Job details" className="flex max-h-[90dvh] w-full max-w-2xl flex-col rounded-[12px] bg-paper shadow-xl">
        <header className="flex items-start gap-3 border-b border-line bg-white px-5 py-4 sm:rounded-t-[12px]">
          {item.studentName && job.type !== "AnalyzeMistakes" ? (
            <Avatar text={initials(item.studentName)} userId={item.studentId ?? undefined} />
          ) : (
            <JobIcon type={job.type} />
          )}
          <div className="min-w-0 flex-1">
            <h2 className="text-[15px] font-bold">
              {job.parentId ? "Grading" : info.label}
              {item.studentName && <span className="font-normal"> · {item.studentName}</span>}
            </h2>
            <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-ink-muted">
              <StatusBadge status={job.status} cancelRequested={job.cancelRequested} />
              <span>{statusText[job.status]}</span>
            </div>
          </div>
          <button onClick={onClose} aria-label="Close" className="shrink-0 text-lg leading-none text-ink-muted hover:text-ink">
            ×
          </button>
        </header>

        <div className="flex min-h-0 flex-1 flex-col gap-4 overflow-y-auto px-5 py-4 text-[13px]">
          <section>
            <SectionTitle>What this job does</SectionTitle>
            <p>{info.description}</p>
            {job.parentId && <p className="mt-1 text-ink-muted">It was started as part of a Grade all or Grade student job.</p>}
          </section>

          <section>
            <SectionTitle>About</SectionTitle>
            <Facts
              rows={[
                ["Student", item.studentName],
                ["Assignment", item.assignmentTitle ? `${item.assignmentTitle}${item.versionNumber != null ? ` · version ${item.versionNumber}` : ""}` : null],
                ["Course", courseName],
                ["Attempts", `${job.attempts} of ${job.maxAttempts}${job.cancelRequested && active ? " · cancel requested" : ""}`],
              ]}
            />
          </section>

          <section>
            <SectionTitle>Timeline</SectionTitle>
            <Facts
              rows={[
                ["Queued", formatDate(job.createdAt)],
                ["Started", job.startedAt ? `${formatDate(job.startedAt)}${waited ? ` · waited ${waited}` : ""}` : "not yet"],
                ["Finished", job.finishedAt ? `${formatDate(job.finishedAt)}${ran ? ` · ran ${ran}` : ""}` : active ? `still ${job.status.toLowerCase()}${ran ? ` · ${ran} so far` : ""}` : "–"],
                ["Next try", job.status === "Queued" && job.attempts > 0 ? formatDate(job.runAfter) : null],
              ]}
            />
          </section>

          {job.error && (
            <section>
              <SectionTitle>Error</SectionTitle>
              <p className="rounded-[10px] border border-[#8f2d2d]/30 bg-[#8f2d2d]/5 px-3 py-2 text-[#8f2d2d]">{job.error}</p>
            </section>
          )}

          <section>
            <SectionTitle>Result</SectionTitle>
            <Result job={job} item={item} submissionLink={submissionLink} children={children.data} />
          </section>

          {isGroup && (
            <section>
              <SectionTitle>
                {job.type === "GradeStudent" ? "Fetch, then gradings per assignment" : "Gradings per student"}
                {job.progressTotal ? ` · ${job.progressDone ?? 0} of ${job.progressTotal} done` : ""}
              </SectionTitle>
              {children.isLoading ? (
                <Spinner label="Loading…" />
              ) : (
                <ul className="divide-y divide-line rounded-[10px] border border-line bg-white">
                  {(children.data ?? []).map((child) => (
                    <li key={child.job.id}>
                      <button onClick={() => onOpen(child)} className="flex w-full items-center gap-3 px-3 py-2 text-left text-xs hover:bg-paper">
                        <StatusBadge status={child.job.status} cancelRequested={child.job.cancelRequested} />
                        <span className="min-w-0 flex-1 truncate">
                          <b>
                            {child.job.type === "FetchStudentWork"
                              ? "Fetch from Classroom"
                              : job.type === "GradeStudent"
                                ? (child.assignmentTitle ?? "Assignment")
                                : (child.studentName ?? "Student")}
                          </b>
                          {child.job.error && <span className="text-[#8f2d2d]"> · {child.job.error}</span>}
                        </span>
                        <span className="shrink-0 text-ink-muted">
                          {child.job.startedAt && child.job.finishedAt ? duration(child.job.startedAt, child.job.finishedAt) : ""}
                        </span>
                        <span className="shrink-0 font-semibold text-accent">Details →</span>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          )}

          <p className="text-[11px] text-ink-muted">Job ID {job.id}</p>
        </div>

        <footer className="flex flex-wrap items-center justify-end gap-2 border-t border-line bg-white px-5 py-3 sm:rounded-b-[12px]">
          {active && !job.cancelRequested && (
            <Button onClick={() => action.mutate({ jobId: job.id, action: "cancel" })} disabled={action.isPending}>
              Cancel job
            </Button>
          )}
          {canRetry && (
            <Button variant="primary" onClick={() => action.mutate({ jobId: job.id, action: "retry" })} disabled={action.isPending}>
              {action.isPending ? "Queuing…" : "Retry"}
            </Button>
          )}
          <Button onClick={onClose}>Close</Button>
        </footer>
      </div>
    </div>
  );
}

function Result({
  job,
  item,
  submissionLink,
  children: childJobs,
}: {
  job: JobOverviewItem["job"];
  item: JobOverviewItem;
  submissionLink: string | undefined;
  children: JobOverviewItem[] | undefined;
}) {
  if (job.status === "Queued" || job.status === "Running") {
    return <p className="text-ink-muted">Not finished yet; this updates on its own.</p>;
  }

  if (job.type === "GradeVersion") {
    if (job.status === "Succeeded") {
      return (
        <p>
          A draft review was saved.{" "}
          {submissionLink && (
            <Link to={submissionLink} className="font-semibold text-accent hover:underline">
              Open the draft →
            </Link>
          )}
        </p>
      );
    }
    return <p className="text-ink-muted">No draft was saved.</p>;
  }

  if (job.type === "FetchStudentWork") {
    return job.summary ? <p>{job.summary}</p> : <p className="text-ink-muted">Nothing was fetched.</p>;
  }

  if (job.type === "AnalyzeMistakes" && item.courseId && item.assignmentId) {
    return job.status === "Succeeded" ? (
      <p>
        The analysis was saved.{" "}
        <Link to={`/courses/${item.courseId}/mistakes?assignment=${item.assignmentId}`} className="font-semibold text-accent hover:underline">
          Open Common mistakes →
        </Link>
      </p>
    ) : (
      <p className="text-ink-muted">No analysis was saved.</p>
    );
  }

  if (job.type === "GradeAssignment" || job.type === "GradeStudent") {
    const gradings = (childJobs ?? []).filter((c) => c.job.type === "GradeVersion");
    const fetch = (childJobs ?? []).find((c) => c.job.type === "FetchStudentWork");
    const count = (status: string) => gradings.filter((c) => c.job.status === status).length;
    return (
      <p>
        {fetch?.job.status === "Failed" && <span className="text-[#8f2d2d]">The fetch from Classroom failed: {fetch.job.error}. </span>}
        {fetch?.job.summary && <span>{fetch.job.summary.replace(/\.?$/, ".")} </span>}
        {childJobs
          ? `${count("Succeeded")} succeeded, ${count("Failed")} failed, ${count("Canceled")} canceled of ${gradings.length} ${gradings.length === 1 ? "grading" : "gradings"}.`
          : "See its gradings below."}{" "}
        {job.type === "GradeAssignment" && item.courseId && item.assignmentId && (
          <Link to={`/courses/${item.courseId}/assignments/${item.assignmentId}`} className="font-semibold text-accent hover:underline">
            Open the assignment →
          </Link>
        )}
      </p>
    );
  }

  return <p className="text-ink-muted">{job.status === "Succeeded" ? "Done." : "Nothing was produced."}</p>;
}

function StatusBadge({ status, cancelRequested }: { status: string; cancelRequested: boolean }) {
  if (status === "Failed") {
    return <span className="inline-flex h-[22px] items-center rounded-full bg-[#8f2d2d] px-2.5 text-[11px] font-semibold text-white">Failed</span>;
  }
  if (status === "Running") {
    return <Chip tone="accent">{cancelRequested ? "Canceling…" : "Running"}</Chip>;
  }
  if (status === "Queued") {
    return <Chip tone="tint">Queued</Chip>;
  }
  if (status === "Canceled") {
    return <Chip tone="muted">Canceled</Chip>;
  }
  return <Chip tone="outline">Succeeded</Chip>;
}

function SectionTitle({ children }: { children: ReactNode }) {
  return <h3 className="mb-1 text-[11px] font-bold uppercase tracking-[0.06em] text-accent">{children}</h3>;
}

function Facts({ rows }: { rows: [string, string | null | undefined][] }) {
  const shown = rows.filter(([, value]) => value);
  return (
    <dl className="grid grid-cols-[110px_minmax(0,1fr)] gap-x-3 gap-y-1">
      {shown.map(([label, value]) => (
        <div key={label} className="contents">
          <dt className="text-ink-muted">{label}</dt>
          <dd className="min-w-0 break-words">{value}</dd>
        </div>
      ))}
    </dl>
  );
}

function duration(from: string, to: string): string {
  const seconds = Math.max(0, Math.round((new Date(to).getTime() - new Date(from).getTime()) / 1000));
  if (seconds < 60) {
    return `${seconds} s`;
  }
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) {
    return `${minutes} min ${seconds % 60} s`;
  }
  return `${Math.floor(minutes / 60)} h ${minutes % 60} min`;
}
