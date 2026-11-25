import { useEffect, useRef, useState } from "react";
import type { JobInfo, TestCase, TestOutcome, TestRun } from "../api/client";
import { useRunTests, useRunTestsForVersion, useStoredSubmissions, useTestRuns } from "../api/hooks";
import { formatDate } from "../format";
import { Loader2, Play, Table2 } from "lucide-react";
import { Button, Card, CardHeader, Chip, ErrorPanel, Notice, SkeletonRows } from "./ui";

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

type Row = { key: string; studentName: string; versionId: string; versionNumber: number; run: TestRun | undefined };

function isActive(job: JobInfo) {
  return job.type === "RunTests" && (job.status === "Queued" || job.status === "Running");
}

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
  const active = jobs.filter(isActive);
  const runningVersions = new Set(active.map((j) => j.versionId));
  const runs = useTestRuns(courseId, assignmentId, testSetId, active.length > 0);
  const stored = useStoredSubmissions(courseId, assignmentId);
  const runAll = useRunTests(courseId, assignmentId);
  const runOne = useRunTestsForVersion(courseId, assignmentId);
  const [selected, setSelected] = useState<{ versionId: string; caseId?: string } | null>(null);
  const wasRunning = useRef(false);
  const refetchRuns = runs.refetch;

  useEffect(() => {
    if (wasRunning.current && active.length === 0) {
      void refetchRuns();
    }
    wasRunning.current = active.length > 0;
  }, [active.length, refetchRuns]);

  const runsByVersion = new Map((runs.data ?? []).map((r) => [r.versionId, r]));
  const rows: Row[] = (stored.data ?? [])
    .map((submission) => {
      const latest = [...submission.versions].filter((v) => v.status !== "Failed").sort((a, b) => b.number - a.number)[0];
      return latest
        ? { key: submission.id, studentName: submission.studentName, versionId: latest.id, versionNumber: latest.number, run: runsByVersion.get(latest.id) }
        : null;
    })
    .filter((row): row is Row => row !== null)
    .sort((a, b) => a.studentName.localeCompare(b.studentName));

  const tested = rows.filter((r) => r.run);
  const completed = tested.filter((r) => r.run!.status === "Completed").map((r) => r.run!);
  const allPassed = completed.filter((r) => r.results.length > 0 && r.results.every((x) => x.outcome === "Passed")).length;
  const hardest = cases
    .map((c) => ({ testCase: c, failed: completed.filter((r) => r.results.some((x) => x.caseId === c.id && x.outcome !== "Passed")).length }))
    .reduce<{ testCase: TestCase; failed: number } | null>((best, item) => (item.failed > 0 && (!best || item.failed > best.failed) ? item : best), null);
  const selectedRow = rows.find((r) => r.versionId === selected?.versionId);

  function run(versionId: string) {
    setSelected({ versionId });
    runOne.mutate({ testSetId, versionId });
  }

  return (
    <Card>
      <CardHeader
        icon={<Table2 />}
        title="Test results"
        subtitle="Each student's latest version against the approved tests"
        actions={
          rows.length > 0 && (
            <Button small icon={<Play />} disabled={runAll.isPending || active.length > 0} onClick={() => runAll.mutate(testSetId)}>
              {runAll.isPending ? "Queuing…" : "Run for everyone"}
            </Button>
          )
        }
      />
      <div className="flex flex-col gap-3 px-[18px] py-3">
        <p className="text-xs text-ink-muted">
          <b className="font-semibold text-ink-soft">Run</b> tests one student's version in the sandbox: it compiles their code, writes their
          adapter from their files (one small AI call, only the first time) and runs the test code. Running again reuses the adapter and is free.
        </p>
        {runAll.isError && <ErrorPanel error={runAll.error} />}
        {runOne.isError && <ErrorPanel error={runOne.error} />}
        {runAll.data && runAll.data.skipped.length > 0 && (
          <Notice tone="info" title={`Queued ${runAll.data.queued}.`} onDismiss={() => runAll.reset()}>
            Skipped: {runAll.data.skipped.join("; ")}
          </Notice>
        )}
        {(runs.isLoading || stored.isLoading) && <SkeletonRows rows={4} />}
        {runs.isError && <ErrorPanel error={runs.error} />}
        {stored.isSuccess && rows.length === 0 && <div className="text-ink-muted">No student has stored work yet. Fetch the submissions first.</div>}

        {rows.length > 0 && (
          <>
            {tested.length > 0 && (
              <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
                <Summary label="Tested" value={`${tested.length} / ${rows.length}`} />
                <Summary label="All passed" value={allPassed} tone="text-success" />
                <Summary label="Some failing" value={completed.length - allPassed} tone="text-danger" />
                <Summary label="Hardest case" value={hardest ? hardest.testCase.id : "–"} note={hardest ? `${hardest.failed} not passing` : undefined} />
              </div>
            )}

            <div className="overflow-x-auto rounded-lg border border-line">
              <table className="w-full border-collapse text-left">
                <thead>
                  <tr className="border-b border-line bg-paper text-[11px] text-ink-muted">
                    <th className="px-3 py-2 font-semibold">Student</th>
                    <th className="px-2 py-2" />
                    {cases.map((c) => (
                      <th key={c.id} title={c.title} className="px-1 py-2 text-center font-semibold">
                        {c.id}
                      </th>
                    ))}
                    <th className="px-3 py-2 text-right font-semibold">Passed</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-paper-dark">
                  {rows.map((row) => {
                    const running = runningVersions.has(row.versionId) || (runOne.isPending && runOne.variables?.versionId === row.versionId);
                    return (
                      <tr key={row.key} className={selected?.versionId === row.versionId ? "bg-accent-tint/30" : ""}>
                        <td className="whitespace-nowrap px-3 py-1.5">
                          <button className="font-semibold hover:text-accent" onClick={() => setSelected({ versionId: row.versionId })}>
                            {row.studentName}
                          </button>
                          <span className="ml-1.5 text-xs text-ink-faint">v{row.versionNumber}</span>
                        </td>
                        <td className="px-2 py-1.5">
                          <Button small variant="ghost" icon={running ? <Loader2 className="animate-spin" /> : <Play />} disabled={running}
                            onClick={() => run(row.versionId)}>
                            {running ? "Running" : row.run ? "Run again" : "Run"}
                          </Button>
                        </td>
                        <ResultCells row={row} cases={cases} onSelect={(caseId) => setSelected({ versionId: row.versionId, caseId })} />
                        <td className="px-3 py-1.5 text-right text-ink-muted">
                          {row.run?.status === "Completed" ? `${row.run.passed} / ${cases.length}` : "–"}
                        </td>
                      </tr>
                    );
                  })}
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

        {selectedRow?.run && <RunDetail run={selectedRow.run} caseId={selected?.caseId} cases={cases} />}
      </div>
    </Card>
  );
}

function ResultCells({ row, cases, onSelect }: { row: Row; cases: TestCase[]; onSelect: (caseId: string) => void }) {
  if (!row.run) {
    return (
      <td colSpan={cases.length} className="px-2 py-1.5 text-xs text-ink-faint">
        Not run yet
      </td>
    );
  }

  if (row.run.status !== "Completed") {
    return (
      <td colSpan={cases.length} className="px-2 py-1.5">
        <Chip tone={runStatus[row.run.status].tone}>{runStatus[row.run.status].label}</Chip>
      </td>
    );
  }

  return (
    <>
      {cases.map((c) => {
        const result = row.run!.results.find((r) => r.caseId === c.id);
        const style = outcomeStyle[result?.outcome ?? "Missing"];
        return (
          <td key={c.id} className="px-1 py-1.5 text-center">
            <button
              title={`${c.id} ${c.title}: ${style.label}`}
              onClick={() => onSelect(c.id)}
              className={`inline-flex h-[22px] w-[26px] items-center justify-center rounded-[5px] text-[11px] font-bold ${style.cell}`}
            >
              {style.mark}
            </button>
          </td>
        );
      })}
    </>
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
