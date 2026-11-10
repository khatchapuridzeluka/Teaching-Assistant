import { type ReactNode, useEffect, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router";
import { Bar, BarChart, CartesianGrid, Cell, LabelList, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { CommonMistakes, MistakeGroup } from "../api/client";
import { useAnalyzeMistakes, useApproveExplanation, useAssignmentJobs, useAssignments, useCommonMistakes } from "../api/hooks";
import { severityColor } from "../components/CodeBrowser";
import { useTrackJob } from "../components/JobToasts";
import { CourseName } from "../components/Layout";
import { categoryLabels } from "../components/StudentMistakes";
import { Button, Card, CardHeader, Chip, EmptyState, ErrorPanel, PageHeader, Spinner } from "../components/ui";
import { formatAge } from "../format";

export function CommonMistakesPage() {
  const { courseId = "" } = useParams();
  const [searchParams, setSearchParams] = useSearchParams();
  const assignments = useAssignments(courseId);
  const assignmentId = searchParams.get("assignment") ?? assignments.data?.[0]?.id ?? "";
  const assignment = assignments.data?.find((a) => a.id === assignmentId);
  const analysis = useCommonMistakes(courseId, assignmentId);
  const analyze = useAnalyzeMistakes(courseId, assignmentId);
  const jobs = useAssignmentJobs(courseId, assignmentId);
  const trackJob = useTrackJob();

  const running = (jobs.data ?? []).find((j) => j.type === "AnalyzeMistakes" && (j.status === "Queued" || j.status === "Running"));
  const failed = (jobs.data ?? []).find((j) => j.type === "AnalyzeMistakes");
  const lastFailed = failed?.status === "Failed" ? failed : undefined;
  const data = analysis.data;
  const groups = data?.groups ?? [];

  function start() {
    analyze.mutate(undefined, {
      onSuccess: (job) =>
        trackJob(job, {
          label: `Common mistakes · ${assignment?.title ?? "Assignment"}`,
          to: `/courses/${courseId}/mistakes?assignment=${assignmentId}`,
        }),
    });
  }

  return (
    <>
      <PageHeader
        title="Common mistakes"
        subtitle={
          <>
            <CourseName courseId={courseId} /> · the AI groups one assignment's mistakes by meaning, so you explain each problem once
          </>
        }
        actions={
          <>
            <select
              value={assignmentId}
              onChange={(e) => setSearchParams({ assignment: e.target.value }, { replace: true })}
              aria-label="Assignment"
              className="h-[34px] max-w-[260px] rounded-lg border border-line bg-white px-2 text-[13px] font-semibold"
            >
              {(assignments.data ?? []).map((a) => (
                <option key={a.id} value={a.id}>
                  {a.title}
                </option>
              ))}
            </select>
            <Button
              variant="primary"
              onClick={start}
              disabled={!assignmentId || analyze.isPending || running !== undefined}
              title="Send this assignment's current draft mistakes (no names) to the AI to group them; one model call"
            >
              {running ? "Analyzing…" : analyze.isPending ? "Queuing…" : data ? "Analyze again" : "Analyze"}
            </Button>
          </>
        }
      />

      {analysis.isError && <ErrorPanel error={analysis.error} />}
      {analyze.isError && <ErrorPanel error={analyze.error} />}
      {lastFailed && !running && <ErrorPanel error={new Error(`The last analysis failed: ${lastFailed.error ?? "unknown error"}`)} />}
      {running && (
        <Card className="px-[18px] py-3">
          <Spinner label="Analyzing… the AI is grouping this assignment's mistakes; you can leave this page." />
        </Card>
      )}
      {analysis.isLoading && <Spinner />}

      {analysis.data === null && !running && (
        <Card>
          <EmptyState>
            <div className="font-semibold text-ink">Not analyzed yet</div>
            Grade the students first (Grade all on the assignment page), then click Analyze. Your report's version of a mistake is used
            where you edited one; rejected mistakes are left out.
          </EmptyState>
        </Card>
      )}

      {data && (
        <>
          <Card>
            <CardHeader
              title="How the class did"
              subtitle={`AI overview of ${data.students} ${data.students === 1 ? "student" : "students"}, measured against the instructions · analyzed ${formatAge(data.analyzedAt)} ago`}
            />
            <p className="px-[18px] py-3 text-[14px] leading-relaxed">
              {data.summary || "This analysis has no overview; it was made before overviews existed. Analyze again to get one."}
            </p>
            {data.nextLecture && (
              <div className="mx-[18px] mb-4 rounded-[10px] border-l-4 border-accent bg-accent-tint/50 px-4 py-3">
                <h4 className="text-[11px] font-bold uppercase tracking-[0.06em] text-accent">For the next lecture</h4>
                <p className="mt-1 text-[14px] leading-relaxed">{data.nextLecture}</p>
              </div>
            )}
          </Card>

          <div className="grid grid-cols-2 border-t border-line md:grid-cols-5">
            <Stat label="Mistakes" value={data.mistakes} note={`${data.students} students, sent without names`} />
            <Stat label="Major" value={data.major} note={share(data.major, data.mistakes)} />
            <Stat label="Minor" value={data.minor} note={share(data.minor, data.mistakes)} />
            <Stat label="Style" value={data.style} note={`${share(data.style, data.mistakes)} · cost no points`} />
            <Stat label="Groups" value={groups.filter((g) => !g.ungrouped).length} note="problems found across the class" />
          </div>

          <Charts data={data} />

          <h2 className="mt-1 text-[15px] font-bold">All mistakes, by group</h2>
          <div className="flex flex-col gap-3">
            {groups.map((group, i) => (
              <GroupCard key={group.id} rank={i + 1} group={group} courseId={courseId} assignmentId={assignmentId} />
            ))}
          </div>
        </>
      )}
    </>
  );
}

const severityShades = { Major: "#1f4a3d", Minor: "#4f8c76", Style: "#a8d1c1" } as const;

function Charts({ data }: { data: CommonMistakes }) {
  const levels = (["Major", "Minor", "Style"] as const).map((level) => ({
    level,
    count: data[level.toLowerCase() as "major" | "minor" | "style"],
    percent: data.mistakes === 0 ? 0 : (data[level.toLowerCase() as "major" | "minor" | "style"] / data.mistakes) * 100,
  }));
  const top = data.groups
    .filter((g) => !g.ungrouped)
    .slice(0, 8)
    .map((g) => ({ name: g.title.length > 34 ? `${g.title.slice(0, 33)}…` : g.title, title: g.title, students: g.students, percent: g.studentPercent }));

  return (
    <div className="grid gap-4 xl:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
      <Card>
        <CardHeader title="Mistakes by level" subtitle={`${data.mistakes} mistakes in total`} />
        <div className="h-[200px] px-2 py-3">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={levels} margin={{ top: 16, right: 12, bottom: 0, left: 0 }}>
              <CartesianGrid vertical={false} stroke="#ecebe7" />
              <XAxis dataKey="level" tick={{ fontSize: 12, fill: "#1f2326" }} tickLine={false} axisLine={{ stroke: "#dedcd7" }} />
              <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: "#6b6e70" }} tickLine={false} axisLine={false} width={28} />
              <Tooltip cursor={{ fill: "rgba(31,35,38,0.06)" }} content={<LevelTooltip />} />
              <Bar dataKey="count" maxBarSize={48} radius={[4, 4, 0, 0]} isAnimationActive={false}>
                <LabelList dataKey="count" position="top" style={{ fontSize: 11, fill: "#1f2326" }} />
                {levels.map((l) => (
                  <Cell key={l.level} fill={severityShades[l.level]} />
                ))}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </div>
      </Card>

      <Card>
        <CardHeader title="Students affected per group" subtitle="Top groups · share of the class with the problem" />
        {top.length === 0 ? (
          <EmptyState>No groups yet.</EmptyState>
        ) : (
          <div className="px-2 py-3" style={{ height: Math.max(140, top.length * 30 + 24) }}>
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={top} layout="vertical" margin={{ top: 0, right: 56, bottom: 0, left: 8 }}>
                <XAxis type="number" hide domain={[0, data.students]} allowDecimals={false} />
                <YAxis type="category" dataKey="name" width={220} tick={{ fontSize: 12, fill: "#1f2326" }} tickLine={false} axisLine={false} />
                <Tooltip cursor={{ fill: "rgba(31,35,38,0.06)" }} content={<GroupTooltip />} />
                <Bar dataKey="students" fill="#2f5e4e" radius={[0, 4, 4, 0]} maxBarSize={16} isAnimationActive={false}>
                  <LabelList dataKey="percent" position="right" formatter={(v: unknown) => `${round(Number(v))}%`} style={{ fontSize: 11, fill: "#6b6e70" }} />
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </div>
        )}
      </Card>
    </div>
  );
}

type ChartTooltip<T> = { active?: boolean; payload?: { payload?: T }[] };

function LevelTooltip({ active, payload }: ChartTooltip<{ level: string; count: number; percent: number }>) {
  const row = active ? payload?.[0]?.payload : undefined;
  if (!row) {
    return null;
  }
  return (
    <div className="rounded-lg border border-line bg-white px-3 py-2 text-xs shadow-md">
      <b>{row.level}</b> · {row.count} mistakes · {round(row.percent)}%
    </div>
  );
}

function GroupTooltip({ active, payload }: ChartTooltip<{ title: string; students: number; percent: number }>) {
  const row = active ? payload?.[0]?.payload : undefined;
  if (!row) {
    return null;
  }
  return (
    <div className="max-w-[280px] rounded-lg border border-line bg-white px-3 py-2 text-xs shadow-md">
      <div className="font-semibold">{row.title}</div>
      {row.students} {row.students === 1 ? "student" : "students"} · {round(row.percent)}% of the class
    </div>
  );
}

function share(count: number, total: number): string {
  return total === 0 ? "–" : `${round((count / total) * 100)}% of all mistakes`;
}

function GroupCard({ rank, group, courseId, assignmentId }: { rank: number; group: MistakeGroup; courseId: string; assignmentId: string }) {
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState(false);
  const [text, setText] = useState(group.approvedExplanation ?? group.explanation);
  const approve = useApproveExplanation(courseId, assignmentId);
  const severities = [
    { key: "Major", count: group.major },
    { key: "Minor", count: group.minor },
    { key: "Style", count: group.style },
  ].filter((s) => s.count > 0);

  useEffect(() => {
    setText(group.approvedExplanation ?? group.explanation);
  }, [group.approvedExplanation, group.explanation]);

  return (
    <Card className={group.ungrouped ? "opacity-80" : ""}>
      <div className="flex flex-wrap items-start gap-x-4 gap-y-2 px-[18px] py-3">
        <span className="mt-0.5 w-6 shrink-0 text-[15px] font-bold text-ink-muted tabular-nums">{group.ungrouped ? "·" : rank}</span>
        <div className="min-w-0 flex-1">
          <h3 className="text-[14px] font-bold">{group.title}</h3>
          <div className="mt-1 flex flex-wrap items-center gap-1.5 text-xs">
            <Chip tone={group.studentPercent >= 50 ? "accent" : "tint"}>
              {group.students} {group.students === 1 ? "student" : "students"} · {round(group.studentPercent)}%
            </Chip>
            {group.averagePoints != null && group.averagePoints > 0 && <Chip tone="outline">avg −{round(group.averagePoints)} pts</Chip>}
            {severities.map((s) => (
              <span key={s.key} className="inline-flex items-center gap-1 text-ink-muted">
                <span className="h-2 w-2 rounded-full" style={{ backgroundColor: severityColor[s.key as keyof typeof severityColor] }} />
                {s.count} {s.key}
              </span>
            ))}
            {group.approvedAt && <Chip tone="ink">Explanation approved</Chip>}
          </div>
        </div>
      </div>

      {!group.ungrouped && (
        <div className="px-[18px] pb-3 pl-[58px]">
          {editing ? (
            <div className="flex flex-col gap-2">
              <textarea
                value={text}
                onChange={(e) => setText(e.target.value)}
                rows={4}
                className="w-full resize-y rounded-[10px] border border-line bg-white px-3 py-2 text-[13px]"
              />
              {approve.isError && <ErrorPanel error={approve.error} />}
              <div className="flex flex-wrap justify-end gap-2">
                <Button small onClick={() => setText(group.explanation)} title="Put the AI's explanation back in the box">
                  Use AI text
                </Button>
                <Button small onClick={() => setEditing(false)}>
                  Cancel
                </Button>
                <Button
                  small
                  variant="primary"
                  disabled={approve.isPending}
                  onClick={() => approve.mutate({ clusterId: group.id, explanation: text }, { onSuccess: () => setEditing(false) })}
                >
                  {approve.isPending ? "Saving…" : "Approve explanation"}
                </Button>
              </div>
            </div>
          ) : (
            <div className="flex flex-wrap items-start gap-3">
              <p className="min-w-0 flex-1 rounded-[10px] border border-line bg-paper/60 px-3 py-2 text-[13px]">
                {group.approvedExplanation ?? group.explanation}
                {!group.approvedExplanation && <span className="ml-1 text-[11px] text-ink-muted">· AI draft</span>}
              </p>
              <Button small onClick={() => setEditing(true)}>
                {group.approvedExplanation ? "Edit" : "Edit & approve"}
              </Button>
            </div>
          )}
        </div>
      )}

      <div className="border-t border-line">
        <button
          onClick={() => setOpen(!open)}
          aria-expanded={open}
          className="w-full px-[18px] py-2 text-left text-xs font-semibold text-accent hover:bg-paper/60"
        >
          {open ? "Hide mistakes ▴" : `Show the ${group.count} ${group.count === 1 ? "mistake" : "mistakes"} ▾`}
        </button>
        {open && (
          <ul className="divide-y divide-paper-dark border-t border-line">
            {group.members.map((m) => {
              const name = m.file?.slice(m.file.lastIndexOf("/") + 1);
              return (
                <li key={m.id} className="flex gap-3 px-[18px] py-2 text-xs">
                  <span className="mt-1 h-2 w-2 shrink-0 rounded-full" style={{ backgroundColor: severityColor[m.severity] }} />
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-x-2">
                      <Link
                        to={`/courses/${courseId}/assignments/${assignmentId}?student=${encodeURIComponent(m.submissionId)}`}
                        className="font-semibold hover:underline"
                      >
                        {m.studentName}
                      </Link>
                      <span className="text-ink-muted">{categoryLabels[m.category] ?? m.category}</span>
                      {m.points != null && m.points > 0 && <b className="text-[#8f2d2d]">−{round(m.points)}</b>}
                      {name && (
                        <span className="font-mono text-ink-muted">
                          {name}
                          {m.startLine != null ? `:${m.startLine}` : ""}
                        </span>
                      )}
                    </div>
                    <p className="mt-0.5 text-[13px]">{m.message}</p>
                  </div>
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </Card>
  );
}

function Stat({ label, value, note }: { label: string; value: ReactNode; note: string }) {
  return (
    <div className="border-b border-line px-4 py-3 md:border-b-0 md:border-r md:last:border-r-0">
      <div className="text-xs text-ink-muted">{label}</div>
      <div className="text-[22px] font-bold leading-tight">{value}</div>
      <div className="text-[11px] text-ink-muted">{note}</div>
    </div>
  );
}

function round(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(1);
}
