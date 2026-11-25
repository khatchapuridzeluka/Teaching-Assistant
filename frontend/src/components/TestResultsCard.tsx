import { useEffect, useRef, useState } from "react";
import type { JobInfo, TestCase, TestOutcome, TestRun } from "../api/client";
import { useRunTests, useTestRuns } from "../api/hooks";
import { formatDate } from "../format";
import { Play, Table2 } from "lucide-react";
import { Button, Card, CardHeader, Chip, ErrorPanel, Notice, SkeletonRows, Spinner } from "./ui";

const outcomeStyle: Record<TestOutcome, { cell: string; mark: string; label: string }> = {
  Passed: { cell: "bg-success-tint text-success-ink", mark: "✓", label: "passed" },
  Failed: { cell: "bg-danger-tint text-danger-ink", mark: "✕", label: "failed" },
  NotImplemented: { cell: "bg-paper-dark text-ink-muted", mark: "–", label: "not implemented" },
  Missing: { cell: "bg-warn-tint text-warn-ink", mark: "?", label: "did not run" },
};

const runStatus: Record<Exclude<TestRun["status"], "Completed">, { label: string; tone: "danger" | "warn" | "muted" }> = {
  StudentCompileFailed: { label: "The submission doesn't compile", tone: "danger" },
  HarnessFailed: { label: "Adapter problem, not counted", tone: "warn" },
  NotRun: { label: "Not run", tone: "muted" },
};

export function TestResultsCard({
  courseId,
  assignmentId,
  testSetId,
  cases,
  jobs,
}: {
  courseId: string;
  assignmentId: string;
  testSetId: string;
  cases: TestCase[];
  jobs: JobInfo[];
}) {
  const active = jobs.filter((j) => j.type === "RunTests" && (j.status === "Queued" || j.status === "Running"));
  const runs = useTestRuns(courseId, assignmentId, testSetId, active.length > 0);
  const start = useRunTests(courseId, assignmentId);
  const [selected, setSelected] = useState<{ runId: string; caseId?: string } | null>(null);
  const wasRunning = useRef(false);
  const refetchRuns = runs.refetch;

  useEffect(() => {
    if (wasRunning.current && active.length === 0) {
      void refetchRuns();
    }
    wasRunning.current = active.length > 0;
  }, [active.length, refetchRuns]);

  const list = runs.data ?? [];
  const completed = list.filter((r) => r.status === "Completed");
  const allPassed = completed.filter((r) => r.results.length > 0 && r.results.every((x) => x.outcome === "Passed")).length;
  const caseFailures = cases.map((c) => ({
    testCase: c,
    failed: completed.filter((r) => r.results.some((x) => x.caseId === c.id && x.outcome !== "Passed")).length,
  }));
  const hardest = caseFailures.reduce<{ testCase: TestCase; failed: number } | null>(
    (best, item) => (item.failed > 0 && (!best || item.failed > best.failed) ? item : best),
    null,
  );
  const selectedRun = list.find((r) => r.id === selected?.runId);

  return (
    <Card>
      <CardHeader
        icon={<Table2 />}
        title="Test results"
        subtitle="Each student's latest version against the approved tests"
        actions={
          <Button small variant="primary" icon={<Play />} disabled={start.isPending || active.length > 0} onClick={() => start.mutate(testSetId)}>
            {start.isPending ? "Queuing…" : list.length > 0 ? "Run tests again" : "Run tests on all submissions"}
          </Button>
        }
      />
      <div className="flex flex-col gap-3 px-[18px] py-3">
        <p className="text-xs text-ink-muted">
          Runs in the sandbox. A version without an adapter yet gets one written first (one small AI call each); running again with the same
          adapters is free.
        </p>
        {active.length > 0 && <Spinner label={`Running tests… ${active.length} ${active.length === 1 ? "student" : "students"} still in the queue.`} />}
        {start.isError && <ErrorPanel error={start.error} />}
        {start.data && start.data.skipped.length > 0 && (
          <Notice tone="info" title={`Queued ${start.data.queued}.`} onDismiss={() => start.reset()}>
            Skipped: {start.data.skipped.join("; ")}
          </Notice>
        )}
        {runs.isLoading && <SkeletonRows rows={4} />}
        {runs.isError && <ErrorPanel error={runs.error} />}

        {list.length > 0 && (
          <>
            <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
              <Summary label="Tested" value={list.length} />
              <Summary label="All passed" value={allPassed} tone="text-success" />
              <Summary label="Some failing" value={completed.length - allPassed} tone="text-danger" />
              <Summary label="Hardest case" value={hardest ? `${hardest.testCase.id}` : "–"} note={hardest ? `${hardest.failed} not passing` : undefined} />
            </div>

            <div className="overflow-x-auto rounded-lg border border-line">
              <table className="w-full border-collapse text-left">
                <thead>
                  <tr className="border-b border-line bg-paper text-[11px] text-ink-muted">
                    <th className="px-3 py-2 font-semibold">Student</th>
                    {cases.map((c) => (
                      <th key={c.id} title={c.title} className="px-1 py-2 text-center font-semibold">
                        {c.id}
                      </th>
                    ))}
                    <th className="px-3 py-2 text-right font-semibold">Passed</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-paper-dark">
                  {list.map((run) => (
                    <tr key={run.id} className={selected?.runId === run.id ? "bg-accent-tint/30" : ""}>
                      <td className="whitespace-nowrap px-3 py-1.5">
                        <button className="font-semibold hover:text-accent" onClick={() => setSelected({ runId: run.id })}>
                          {run.studentName}
                        </button>
                        <span className="ml-1.5 text-xs text-ink-faint">v{run.versionNumber}</span>
                      </td>
                      {run.status === "Completed" ? (
                        cases.map((c) => {
                          const result = run.results.find((r) => r.caseId === c.id);
                          const style = outcomeStyle[result?.outcome ?? "Missing"];
                          return (
                            <td key={c.id} className="px-1 py-1.5 text-center">
                              <button
                                title={`${c.id} ${c.title}: ${style.label}`}
                                onClick={() => setSelected({ runId: run.id, caseId: c.id })}
                                className={`inline-flex h-[22px] w-[26px] items-center justify-center rounded-[5px] text-[11px] font-bold ${style.cell}`}
                              >
                                {style.mark}
                              </button>
                            </td>
                          );
                        })
                      ) : (
                        <td colSpan={cases.length} className="px-2 py-1.5">
                          <button onClick={() => setSelected({ runId: run.id })}>
                            <Chip tone={runStatus[run.status].tone}>{runStatus[run.status].label}</Chip>
                          </button>
                        </td>
                      )}
                      <td className="px-3 py-1.5 text-right text-ink-muted">
                        {run.status === "Completed" ? `${run.passed} / ${cases.length}` : "–"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="flex flex-wrap gap-3 text-[11.5px] text-ink-muted">
              {(Object.keys(outcomeStyle) as TestOutcome[]).map((o) => (
                <span key={o} className="flex items-center gap-1">
                  <span className={`inline-flex h-[16px] w-[18px] items-center justify-center rounded-[4px] text-[10px] font-bold ${outcomeStyle[o].cell}`}>
                    {outcomeStyle[o].mark}
                  </span>
                  {outcomeStyle[o].label}
                </span>
              ))}
            </div>
          </>
        )}

        {selectedRun && <RunDetail run={selectedRun} caseId={selected?.caseId} cases={cases} />}
      </div>
    </Card>
  );
}

function Summary({ label, value, tone = "text-ink", note }: { label: string; value: number | string; tone?: string; note?: string }) {
  return (
    <div className="rounded-lg border border-line px-3 py-2">
      <div className="text-xs text-ink-muted">{label}</div>
      <div className={`text-[18px] font-bold ${tone}`}>{value}</div>
      {note && <div className="text-xs text-ink-muted">{note}</div>}
    </div>
  );
}

function RunDetail({ run, caseId, cases }: { run: TestRun; caseId?: string; cases: TestCase[] }) {
  const shown = caseId ? run.results.filter((r) => r.caseId === caseId) : run.results.filter((r) => r.outcome !== "Passed");
  const title = (id: string) => cases.find((c) => c.id === id)?.title ?? id;

  return (
    <div className="flex flex-col gap-2.5 rounded-lg border border-line bg-paper/60 px-3.5 py-3">
      <div>
        <b>{run.studentName}</b>
        <span className="ml-1.5 text-xs text-ink-muted">
          version {run.versionNumber} · tests version {run.testSetVersion} · run {formatDate(run.createdAt)}
        </span>
      </div>
      {run.status === "Completed" && shown.length === 0 && <div className="text-success-ink">Every case passed.</div>}
      {shown.map((result) => (
        <div key={result.caseId} className={`rounded-md px-3 py-2 text-[12.5px] ${outcomeStyle[result.outcome].cell}`}>
          <b>
            {result.caseId} · {title(result.caseId)}
          </b>{" "}
          {outcomeStyle[result.outcome].label}
          {result.message && <div className="mt-0.5 whitespace-pre-wrap font-mono text-[11.5px]">{result.message}</div>}
        </div>
      ))}
      {run.output && (
        <pre className="max-h-60 overflow-auto whitespace-pre-wrap rounded-md border border-line bg-white px-3 py-2 font-mono text-[11px] leading-relaxed text-ink-soft">
          {run.output}
        </pre>
      )}
      {run.adapter && (
        <details>
          <summary className="cursor-pointer text-xs font-semibold text-ink-soft">
            Adapter for this submission
            {run.unmapped.length > 0 && <span className="ml-1.5 font-normal text-ink-muted">(not found: {run.unmapped.join(", ")})</span>}
          </summary>
          {run.notes && <p className="mt-1.5 text-xs text-ink-muted">{run.notes}</p>}
          <pre className="mt-1.5 max-h-[420px] overflow-auto rounded-md border border-line bg-white px-3 py-2 font-mono text-[11px] leading-relaxed text-ink-soft">
            {run.adapter}
          </pre>
        </details>
      )}
    </div>
  );
}
