import { createContext, type ReactNode, useCallback, useContext, useEffect, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router";
import type { JobInfo } from "../api/client";
import { useJob } from "../api/hooks";

export type JobContext = { label: string; to: string };

type TrackedJob = JobContext & { jobId: string; status: JobInfo["status"]; attempts: number };

type ToastTone = "info" | "success" | "error";

type ToastMessage = { tone: ToastTone; title: string; detail: string; link?: { to: string; label: string } };

type Toast = ToastMessage & { id: number; leaving: boolean };

const leaveDuration = 300;

const storageKey = "ta.trackedJobs";

const TrackJobContext = createContext<(job: JobInfo, context: JobContext) => void>(() => {});

export const useTrackJob = () => useContext(TrackJobContext);

export function JobToastProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [tracked, setTracked] = useState<TrackedJob[]>(loadTracked);
  const [toasts, setToasts] = useState<Toast[]>([]);
  const nextId = useRef(1);

  useEffect(() => {
    try {
      sessionStorage.setItem(storageKey, JSON.stringify(tracked));
    } catch {
      return;
    }
  }, [tracked]);

  const dismiss = useCallback((id: number) => {
    setToasts((list) => list.map((toast) => (toast.id === id ? { ...toast, leaving: true } : toast)));
    window.setTimeout(() => setToasts((list) => list.filter((toast) => toast.id !== id)), leaveDuration);
  }, []);

  const show = useCallback(
    (message: ToastMessage | null) => {
      if (!message) {
        return;
      }

      const id = nextId.current++;
      setToasts((list) => [...list.slice(-2), { ...message, id, leaving: false }]);
      window.setTimeout(() => dismiss(id), message.tone === "info" ? 7000 : 12000);
    },
    [dismiss],
  );

  const track = useCallback(
    (job: JobInfo, context: JobContext) => {
      if (tracked.some((entry) => entry.jobId === job.id)) {
        return;
      }

      const entry: TrackedJob = { ...context, jobId: job.id, status: job.status, attempts: job.attempts };
      show(messageFor(entry, job));
      if (!isFinished(job.status)) {
        setTracked((list) => [...list, entry]);
      }
    },
    [tracked, show],
  );

  const reported = useRef(new Set<string>());

  const onChange = useCallback(
    (entry: TrackedJob, job: JobInfo) => {
      const key = `${job.id}:${job.status}:${job.attempts}`;
      if (reported.current.has(key)) {
        return;
      }

      reported.current.add(key);
      show(messageFor(entry, job));
      if (isFinished(job.status)) {
        setTracked((list) => list.filter((item) => item.jobId !== job.id));
        if (job.status === "Succeeded") {
          void queryClient.invalidateQueries({ queryKey: ["reviews"] });
          void queryClient.invalidateQueries({ predicate: (query) => query.queryKey.includes("gradings") });
        }
      } else {
        setTracked((list) =>
          list.map((item) => (item.jobId === job.id ? { ...item, status: job.status, attempts: job.attempts } : item)),
        );
      }
    },
    [show, queryClient],
  );

  return (
    <TrackJobContext.Provider value={track}>
      {children}
      {tracked.map((entry) => (
        <JobWatcher key={entry.jobId} entry={entry} onChange={onChange} />
      ))}
      <div className="pointer-events-none fixed inset-x-0 top-3 z-[60] flex flex-col items-center gap-2 px-4">
        {toasts.map((toast) => (
          <ToastCard key={toast.id} toast={toast} onClose={() => dismiss(toast.id)} />
        ))}
      </div>
    </TrackJobContext.Provider>
  );
}

function JobWatcher({ entry, onChange }: { entry: TrackedJob; onChange: (entry: TrackedJob, job: JobInfo) => void }) {
  const job = useJob(entry.jobId).data;

  useEffect(() => {
    if (job && (job.status !== entry.status || job.attempts !== entry.attempts)) {
      onChange(entry, job);
    }
  }, [job, entry, onChange]);

  return null;
}

const toneStyles: Record<ToastTone, string> = {
  info: "bg-accent text-white",
  success: "bg-accent text-white",
  error: "bg-[#8f2d2d] text-white",
};

function ToastCard({ toast, onClose }: { toast: Toast; onClose: () => void }) {
  return (
    <div
      role="status"
      className={`pointer-events-auto flex w-full max-w-sm items-start gap-3 rounded-[10px] px-4 py-3 text-xs shadow-lg ${toneStyles[toast.tone]} ${
        toast.leaving ? "toast-leave" : "toast-enter"
      }`}
    >
      <span className="mt-0.5 text-sm leading-none">{toast.tone === "success" ? "✓" : toast.tone === "error" ? "!" : "•"}</span>
      <div className="min-w-0 flex-1">
        <b className="block text-[13px]">{toast.title}</b>
        <span className="block break-words opacity-90">{toast.detail}</span>
        {toast.link && (
          <Link to={toast.link.to} onClick={onClose} className="mt-1 inline-block font-semibold underline">
            {toast.link.label}
          </Link>
        )}
      </div>
      <button onClick={onClose} aria-label="Dismiss" className="shrink-0 text-sm leading-none opacity-80 hover:opacity-100">
        ×
      </button>
    </div>
  );
}

function messageFor(entry: TrackedJob, job: JobInfo): ToastMessage | null {
  if (job.type === "GradeAssignment" || job.type === "GradeStudent") {
    return groupMessage(entry, job);
  }

  if (job.type === "AnalyzeMistakes") {
    return analysisMessage(entry, job);
  }

  if (job.type === "AnalyzeStudent") {
    return analysisMessage(entry, job, "Student analysis ready");
  }

  switch (job.status) {
    case "Queued":
      if (job.attempts > 0) {
        return {
          tone: "error",
          title: "Grading will retry",
          detail: `${entry.label} · attempt ${job.attempts + 1} of ${job.maxAttempts} soon${job.error ? `: ${job.error}` : ""}`,
        };
      }
      return { tone: "info", title: "Grading queued", detail: entry.label };
    case "Running":
      return null;
    case "Succeeded":
      return { tone: "success", title: "Grading finished", detail: entry.label, link: { to: entry.to, label: "Open draft" } };
    case "Failed":
      return { tone: "error", title: "Grading failed", detail: `${entry.label}: ${job.error ?? "unknown error"}` };
    case "Canceled":
      return { tone: "info", title: "Grading canceled", detail: entry.label };
  }
}

function groupMessage(entry: TrackedJob, job: JobInfo): ToastMessage {
  const forStudent = job.type === "GradeStudent";
  const fetchSteps = forStudent ? 1 : 0;
  const total = Math.max((job.progressTotal ?? 0) - fetchSteps, 0);
  const done = Math.max((job.progressDone ?? 0) - fetchSteps, 0);
  const fetching = forStudent && (job.progressDone ?? 0) === 0 && total === 0;
  switch (job.status) {
    case "Queued":
    case "Running":
      return {
        tone: "info",
        title: fetching ? "Fetching from Classroom" : forStudent ? "Grading all assignments" : "Grading all students",
        detail: `${entry.label}${total > 0 ? ` · ${done} of ${total} done` : ""}`,
      };
    case "Succeeded":
      if (forStudent && total === 0) {
        return { tone: "info", title: "Nothing to grade", detail: entry.label, link: { to: "/system", label: "See why" } };
      }
      return {
        tone: "success",
        title: "All gradings finished",
        detail: entry.label,
        link: { to: entry.to, label: forStudent ? "Open student" : "Open assignment" },
      };
    case "Failed":
      return {
        tone: "error",
        title: "Grade all finished with errors",
        detail: `${entry.label}: ${job.error ?? "some gradings failed"}`,
        link: { to: "/system", label: "See which failed" },
      };
    case "Canceled":
      return { tone: "info", title: "Grade all canceled", detail: entry.label };
  }
}

function analysisMessage(entry: TrackedJob, job: JobInfo, readyTitle = "Common mistakes ready"): ToastMessage | null {
  switch (job.status) {
    case "Queued":
      return job.attempts > 0
        ? { tone: "error", title: "Analysis will retry", detail: `${entry.label}${job.error ? `: ${job.error}` : ""}` }
        : { tone: "info", title: "Analysis queued", detail: entry.label };
    case "Running":
      return null;
    case "Succeeded":
      return { tone: "success", title: readyTitle, detail: entry.label, link: { to: entry.to, label: "Open" } };
    case "Failed":
      return { tone: "error", title: "Analysis failed", detail: `${entry.label}: ${job.error ?? "unknown error"}` };
    case "Canceled":
      return { tone: "info", title: "Analysis canceled", detail: entry.label };
  }
}

function isFinished(status: JobInfo["status"]) {
  return status === "Succeeded" || status === "Failed" || status === "Canceled";
}

function loadTracked(): TrackedJob[] {
  try {
    const raw = sessionStorage.getItem(storageKey);
    return raw ? (JSON.parse(raw) as TrackedJob[]) : [];
  } catch {
    return [];
  }
}
