import { useCallback, useEffect, useState } from "react";
import type { StudentAnalysisKind, StudentStrengthGroup } from "../api/client";
import { useAnalyzeStudent, useJob, useStudentAnalysis } from "../api/hooks";
import { formatDate } from "../format";
import { strengthColor } from "./CodeBrowser";
import { ConfirmDialog } from "./ConfirmDialog";
import { useTrackJob } from "./JobToasts";
import { categoryLabels } from "./StudentMistakes";
import { Sparkles } from "lucide-react";
import { Button, Chip, ErrorPanel } from "./ui";

const texts: Record<StudentAnalysisKind, { title: string; empty: string; confirm: string; job: string; records: string }> = {
  Mistakes: {
    title: "AI feedback on mistakes",
    empty:
      "Not analyzed yet. The AI gets only the mistakes you kept in approved reports, with their points and new / repeated labels, and writes feedback and what to work on next.",
    confirm:
      "The AI reads every mistake you kept in the approved reports, with its points and label, and writes feedback of at most 200 words and what to work on next. The name is never sent.",
    job: "mistakes feedback",
    records: "mistakes",
  },
  Strengths: {
    title: "AI strengths analysis",
    empty:
      "Not analyzed yet. The AI gets only the strengths you kept in approved reports, groups similar ones into skills and writes what the student is good at. It gets better as more assignments are approved.",
    confirm:
      "The AI reads every strength you kept in the approved reports, groups similar ones into skills and writes an overview of at most 150 words. The name is never sent.",
    job: "strengths analysis",
    records: "strengths",
  },
};

export function StudentOverviewCard({
  courseId,
  userId,
  studentName,
  returnTo,
  kind,
}: {
  courseId: string;
  userId: string;
  studentName: string;
  returnTo: string;
  kind: StudentAnalysisKind;
}) {
  const analysis = useStudentAnalysis(courseId, userId, kind);
  const analyze = useAnalyzeStudent(courseId, userId, kind);
  const trackJob = useTrackJob();
  const [confirming, setConfirming] = useState(false);
  const [jobId, setJobId] = useState<string | null>(null);
  const data = analysis.data;
  const text = texts[kind];
  const { refetch } = analysis;
  const clearJob = useCallback(() => {
    setJobId(null);
    void refetch();
  }, [refetch]);

  function start() {
    setConfirming(false);
    analyze.mutate(undefined, {
      onSuccess: (job) => {
        setJobId(job.id);
        trackJob(job, { label: `${studentName} · ${text.job}`, to: returnTo });
      },
    });
  }

  return (
    <section className="surface">
      <div className="flex flex-wrap items-start justify-between gap-2 border-b border-line px-4 py-2.5">
        <div className="min-w-0">
          <h3 className="flex items-center gap-1.5 text-[13px] font-semibold">
            <Sparkles className="h-3.5 w-3.5 text-warn" />
            {text.title}
          </h3>
          <div className="text-[11px] text-ink-muted">
            {data
              ? `From ${data.records} ${text.records} in ${data.assignments} approved ${data.assignments === 1 ? "assignment" : "assignments"} · ${formatDate(data.createdAt)} · ${data.model}`
              : "Written by the AI from the approved reports; the name is never sent"}
          </div>
        </div>
        <span className="flex items-center gap-2">
          {data?.outdated && <Chip tone="warn">Reports changed since</Chip>}
          <Button small icon={<Sparkles />} onClick={() => setConfirming(true)} disabled={analyze.isPending || jobId !== null}>
            {jobId ? "Analyzing…" : analyze.isPending ? "Queuing…" : data ? "Analyze again" : "Analyze with AI"}
          </Button>
        </span>
      </div>

      {jobId && <JobWatch jobId={jobId} onDone={clearJob} />}
      {analyze.isError && (
        <div className="p-3">
          <ErrorPanel error={analyze.error} />
        </div>
      )}
      {analysis.isError && (
        <div className="p-3">
          <ErrorPanel error={analysis.error} />
        </div>
      )}

      {data ? (
        <div className="flex flex-col gap-3 px-4 py-3">
          <p className="whitespace-pre-line text-[13px] leading-relaxed">{data.summary}</p>
          {data.nextSteps && (
            <div className="rounded-lg border-l-4 border-mark bg-mark-tint/50 px-3 py-2">
              <div className="eyebrow text-mark-ink">What to work on next</div>
              <p className="mt-1 whitespace-pre-line text-[13px] leading-relaxed">{data.nextSteps}</p>
            </div>
          )}
          {data.groups.length > 0 && (
            <div className="flex flex-col gap-2">
              <div className="eyebrow">Skills · {data.groups.filter((g) => !g.ungrouped).length}</div>
              {data.groups.map((group, i) => (
                <SkillGroup key={i} group={group} />
              ))}
            </div>
          )}
        </div>
      ) : (
        analysis.isSuccess && !jobId && <p className="px-4 py-3 text-xs text-ink-muted">{text.empty}</p>
      )}

      {confirming && (
        <ConfirmDialog
          title={`Analyze ${studentName}'s ${text.records} with AI?`}
          confirmLabel="Analyze"
          onConfirm={start}
          onCancel={() => setConfirming(false)}
        >
          <p>{text.confirm}</p>
          <p className="mt-1.5">It is one paid AI call and runs in the background; the result appears here when it is done.</p>
        </ConfirmDialog>
      )}
    </section>
  );
}

function SkillGroup({ group }: { group: StudentStrengthGroup }) {
  return (
    <details className={`rounded-lg border px-3 py-2 ${group.ungrouped ? "border-dashed border-line" : "border-line"}`}>
      <summary className="cursor-pointer list-none">
        <div className="flex flex-wrap items-center gap-2 text-xs">
          <span className="h-2.5 w-2.5 shrink-0 rounded-full" style={{ backgroundColor: strengthColor }} />
          <b className="text-[13px]">{group.title}</b>
          <span className="text-ink-muted">
            {group.count} {group.count === 1 ? "strength" : "strengths"} · {group.assignments}{" "}
            {group.assignments === 1 ? "assignment" : "assignments"}
          </span>
          {group.assignments > 1 && <Chip tone="success">Shown again</Chip>}
          <span className="ml-auto text-[11px] text-ink-muted">
            {group.categories.map((c) => categoryLabels[c] ?? c).join(", ")}
          </span>
        </div>
        {group.explanation && <p className="mt-1 text-[13px] leading-relaxed">{group.explanation}</p>}
      </summary>
      <ul className="mt-2 flex flex-col gap-1 border-t border-paper-dark pt-2">
        {group.strengths.map((s) => (
          <li key={s.id} className="text-xs">
            <b>{s.assignmentTitle ?? "Assignment"}</b>
            <span className="text-ink-muted"> · {categoryLabels[s.category] ?? s.category} · </span>
            {s.message}
          </li>
        ))}
      </ul>
    </details>
  );
}

function JobWatch({ jobId, onDone }: { jobId: string; onDone: () => void }) {
  const job = useJob(jobId).data;
  const done = job?.status === "Succeeded" || job?.status === "Canceled";

  useEffect(() => {
    if (done) {
      onDone();
    }
  }, [done, onDone]);

  if (done) {
    return null;
  }

  return (
    <div className="flex items-center justify-between gap-2 border-b border-line px-4 py-2 text-xs">
      {job?.status === "Failed" ? (
        <>
          <span className="text-danger">The analysis failed: {job.error ?? "unknown error"}</span>
          <button onClick={onDone} className="font-semibold text-accent hover:underline">
            Dismiss
          </button>
        </>
      ) : (
        <span className="text-ink-muted">{job?.status === "Running" ? "The AI is writing it…" : "Queued; it starts in a moment…"}</span>
      )}
    </div>
  );
}
