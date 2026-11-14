import type { ReactNode } from "react";
import { Link, useParams } from "react-router";
import { Bar, CartesianGrid, ComposedChart, Line, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { OverviewAssignment, OverviewCategory } from "../api/client";
import { useCourseStudents, useStudentOverview } from "../api/hooks";
import { strengthColor } from "../components/CodeBrowser";
import { CourseName } from "../components/Layout";
import { StudentPageNav } from "../components/StudentPageNav";
import { StudentOverviewCard } from "../components/StudentOverview";
import { categoryLabels, SourceChip } from "../components/StudentMistakes";
import { Avatar, Card, CardHeader, EmptyState, ErrorPanel, PageHeader, Spinner, Stat, StatStrip } from "../components/ui";
import { formatDay, formatPoints, initials } from "../format";

const mistakeColor = "#9a3412";
const scoreColor = "#16203a";

export function StudentOverviewPage() {
  const { courseId = "", userId = "" } = useParams();
  const overview = useStudentOverview(courseId, userId);
  const student = useCourseStudents(courseId).data?.students.find((s) => s.externalUserId === userId);
  const data = overview.data;
  const base = `/courses/${courseId}/students/${userId}`;

  return (
    <>
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            {student && <Avatar text={initials(student.name)} userId={student.externalUserId} />}
            <span>Overview · {student?.name ?? "Student"}</span>
          </span>
        }
        subtitle={
          <>
            <CourseName courseId={courseId} /> · scores, mistakes and strengths together · your report where you reviewed it, otherwise
            the newest AI draft
          </>
        }
        actions={<StudentPageNav courseId={courseId} userId={userId} studentId={student?.id} active="overview" />}
      />

      {overview.isError && <ErrorPanel error={overview.error} />}
      {overview.isLoading && <Spinner />}

      {data && data.gradedAssignments === 0 && (
        <Card>
          <EmptyState>
            <div className="font-semibold text-ink">Nothing graded yet</div>
            The overview is built from each graded assignment: your report where you reviewed it, otherwise the AI draft. Grade an
            assignment first.
          </EmptyState>
        </Card>
      )}

      {data && data.gradedAssignments > 0 && (
        <>
          <StudentOverviewCard
            courseId={courseId}
            userId={userId}
            studentName={student?.name ?? "this student"}
            returnTo={`${base}/overview`}
            kind="Overview"
          />

          <StatStrip columns={6}>
            <Stat label="Graded" value={data.gradedAssignments} note={data.gradedAssignments === 1 ? "assignment" : "assignments"} />
            <Stat label="Average" value={percent(data.averagePercent)} note="of the graded ones" />
            <Stat
              label="Direction"
              value={direction(data.firstPercent, data.latestPercent, data.gradedAssignments)}
              note={data.gradedAssignments > 1 ? `${percent(data.firstPercent)} → ${percent(data.latestPercent)}` : "needs two assignments"}
              tone={trendTone(data.firstPercent, data.latestPercent, data.gradedAssignments)}
            />
            <Stat label="Mistakes" value={data.mistakes} note={`${data.repeatedMistakes} repeated · −${formatPoints(data.pointsLost)} pts`} />
            <Stat label="Strengths" value={data.strengths} note={`${data.consistentStrengths} consistent`} />
            <Stat
              label="Improved"
              value={data.improvedStrengths}
              note="former mistake now a strength"
              tone={data.improvedStrengths > 0 ? "good" : undefined}
            />
          </StatStrip>

          <Card>
            <CardHeader title="Progress" subtitle="Score per assignment (line, right axis) with its strengths and mistakes, in due-date order" />
            <div className="flex flex-wrap gap-x-4 px-[18px] pt-3 text-[11px] text-ink-muted">
              <Legend color={scoreColor} label="Score %" />
              <Legend color={strengthColor} label="Strengths" />
              <Legend color={mistakeColor} label="Mistakes" />
            </div>
            <div className="h-[240px] px-2 pb-3 pt-1">
              <ResponsiveContainer width="100%" height="100%">
                <ComposedChart data={data.assignments.map((a) => ({ ...a, name: a.title ?? "Assignment" }))} margin={{ top: 8, right: 4, bottom: 0, left: 0 }}>
                  <CartesianGrid vertical={false} stroke="#e9ecf3" />
                  <XAxis dataKey="name" tick={{ fontSize: 11, fill: "#5d6985" }} tickLine={false} axisLine={{ stroke: "#dcdfea" }} />
                  <YAxis yAxisId="count" allowDecimals={false} tick={{ fontSize: 11, fill: "#5d6985" }} tickLine={false} axisLine={false} width={28} />
                  <YAxis
                    yAxisId="percent"
                    orientation="right"
                    domain={[0, 100]}
                    tick={{ fontSize: 11, fill: "#5d6985" }}
                    tickFormatter={(v: number) => `${v}%`}
                    tickLine={false}
                    axisLine={false}
                    width={40}
                  />
                  <Tooltip cursor={{ fill: "rgba(22,32,58,0.06)" }} content={<AssignmentTooltip />} />
                  <Bar yAxisId="count" dataKey="strengths" fill={strengthColor} radius={[4, 4, 0, 0]} maxBarSize={22} isAnimationActive={false} />
                  <Bar yAxisId="count" dataKey="mistakes" fill={mistakeColor} radius={[4, 4, 0, 0]} maxBarSize={22} isAnimationActive={false} />
                  <Line
                    yAxisId="percent"
                    dataKey="percent"
                    stroke={scoreColor}
                    strokeWidth={2}
                    dot={{ r: 3.5, fill: scoreColor }}
                    connectNulls
                    isAnimationActive={false}
                  />
                </ComposedChart>
              </ResponsiveContainer>
            </div>
          </Card>

          <div className="grid gap-4 xl:grid-cols-2">
            <Card>
              <CardHeader
                title="Holding them back"
                subtitle="Mistake areas that cost the most points"
                actions={
                  <Link to={`${base}/mistakes`} className="text-xs font-semibold text-accent hover:underline">
                    All mistakes →
                  </Link>
                }
              />
              {data.topMistakes.length === 0 ? (
                <p className="px-[18px] py-3 text-xs text-ink-muted">No mistakes recorded.</p>
              ) : (
                <ul className="divide-y divide-paper-dark">
                  {data.topMistakes.map((m) => (
                    <TopRow
                      key={m.category}
                      to={`${base}/mistakes?category=${m.category}`}
                      color={mistakeColor}
                      title={categoryLabels[m.category] ?? m.category}
                      detail={`${m.count} in ${m.assignments} ${m.assignments === 1 ? "assignment" : "assignments"}${m.repeated ? " · repeated" : ""}`}
                      value={`−${formatPoints(m.pointsLost)} pts`}
                    />
                  ))}
                </ul>
              )}
            </Card>

            <Card>
              <CardHeader
                title="Strong at"
                subtitle="Areas the student shows in the most assignments"
                actions={
                  <Link to={`${base}/strengths`} className="text-xs font-semibold text-accent hover:underline">
                    All strengths →
                  </Link>
                }
              />
              {data.topStrengths.length === 0 ? (
                <p className="px-[18px] py-3 text-xs text-ink-muted">No strengths recorded.</p>
              ) : (
                <ul className="divide-y divide-paper-dark">
                  {data.topStrengths.map((s) => (
                    <TopRow
                      key={s.category}
                      to={`${base}/strengths?category=${s.category}`}
                      color={strengthColor}
                      title={categoryLabels[s.category] ?? s.category}
                      detail={`${s.count} in ${s.assignments} ${s.assignments === 1 ? "assignment" : "assignments"}${
                        s.improvedSinceAssignmentTitle ? ` · improved since ${s.improvedSinceAssignmentTitle}` : s.consistent ? " · consistent" : ""
                      }`}
                      value={String(s.count)}
                    />
                  ))}
                </ul>
              )}
            </Card>
          </div>

          <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)]">
            <Card>
              <CardHeader title="Balance by area" subtitle="Strengths and mistakes per category" />
              <ul className="flex flex-col gap-2 px-[18px] py-3">
                {data.categories.map((c) => (
                  <BalanceRow key={c.category} category={c} max={Math.max(...data.categories.map((x) => Math.max(x.strengths, x.mistakes)))} />
                ))}
              </ul>
            </Card>

            <Card>
              <CardHeader title="Assignments" subtitle="Where each score comes from" />
              <ul className="divide-y divide-paper-dark">
                {data.assignments.map((a) => (
                  <AssignmentRow key={a.assignmentId} assignment={a} />
                ))}
              </ul>
            </Card>
          </div>
        </>
      )}
    </>
  );
}

function TopRow({ to, color, title, detail, value }: { to: string; color: string; title: string; detail: string; value: string }) {
  return (
    <li>
      <Link to={to} className="flex items-center gap-3 px-[18px] py-2.5 hover:bg-paper">
        <span className="h-2.5 w-2.5 shrink-0 rounded-[3px]" style={{ backgroundColor: color }} />
        <span className="min-w-0 flex-1">
          <b className="block text-[13px]">{title}</b>
          <span className="block truncate text-[11px] text-ink-muted">{detail}</span>
        </span>
        <span className="shrink-0 text-xs font-semibold tabular-nums">{value}</span>
      </Link>
    </li>
  );
}

function BalanceRow({ category, max }: { category: OverviewCategory; max: number }) {
  const width = (n: number) => `${max === 0 ? 0 : (n / max) * 100}%`;
  return (
    <li className="grid grid-cols-[minmax(0,1fr)_150px_minmax(0,1fr)] items-center gap-2 text-xs">
      <div className="flex justify-end">
        <div className="h-2.5 rounded-l-[3px]" style={{ width: width(category.mistakes), backgroundColor: mistakeColor }} title={`${category.mistakes} mistakes`} />
      </div>
      <div className="truncate text-center">
        <span className="tabular-nums text-ink-muted">{category.mistakes}</span> {categoryLabels[category.category] ?? category.category}{" "}
        <span className="tabular-nums text-ink-muted">{category.strengths}</span>
      </div>
      <div>
        <div className="h-2.5 rounded-r-[3px]" style={{ width: width(category.strengths), backgroundColor: strengthColor }} title={`${category.strengths} strengths`} />
      </div>
    </li>
  );
}

function AssignmentRow({ assignment: a }: { assignment: OverviewAssignment }) {
  return (
    <li className="flex flex-wrap items-center gap-x-3 gap-y-1 px-[18px] py-2.5 text-xs">
      <span className="min-w-0 flex-1">
        <b className="block truncate text-[13px]">{a.title ?? "Assignment"}</b>
        <span className="text-[11px] text-ink-muted">
          {a.dueAt ? `due ${formatDay(a.dueAt)} · ` : ""}
          {a.mistakes} {a.mistakes === 1 ? "mistake" : "mistakes"}
          {a.repeated > 0 ? ` (${a.repeated} repeated)` : ""} · {a.strengths} {a.strengths === 1 ? "strength" : "strengths"}
        </span>
      </span>
      <SourceChip source={a.source} />
      <span className="w-24 text-right">
        <b className="num text-[15px]">{a.score == null ? "–" : formatPoints(a.score)}</b>
        <span className="text-ink-muted"> / {a.maxPoints == null ? "?" : formatPoints(a.maxPoints)}</span>
        {a.bonus != null && a.bonus > 0 && <span className="block text-[11px] text-success">+{formatPoints(a.bonus)} bonus</span>}
      </span>
    </li>
  );
}

type TooltipProps<T> = { active?: boolean; payload?: { payload?: T }[] };

function AssignmentTooltip({ active, payload }: TooltipProps<OverviewAssignment & { name: string }>) {
  const row = active ? payload?.[0]?.payload : undefined;
  if (!row) {
    return null;
  }
  return (
    <TooltipBox title={row.name}>
      <TooltipLine label="Score" value={row.score == null ? "–" : `${formatPoints(row.score)} / ${row.maxPoints == null ? "?" : formatPoints(row.maxPoints)}`} color={scoreColor} />
      <TooltipLine label="Strengths" value={String(row.strengths)} color={strengthColor} />
      <TooltipLine label="Mistakes" value={`${row.mistakes} · −${formatPoints(row.pointsLost)} pts`} color={mistakeColor} />
    </TooltipBox>
  );
}

function TooltipBox({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="rounded-lg border border-line bg-white px-3 py-2 text-xs shadow-md">
      <div className="mb-1 font-semibold">{title}</div>
      {children}
    </div>
  );
}

function TooltipLine({ label, value, color }: { label: string; value: string; color?: string }) {
  return (
    <div className="flex items-center justify-between gap-4">
      <span className="inline-flex items-center gap-1.5 text-ink-muted">
        {color && <span className="h-2 w-2 rounded-[2px]" style={{ backgroundColor: color }} />}
        {label}
      </span>
      <span className="tabular-nums text-ink">{value}</span>
    </div>
  );
}

function Legend({ color, label }: { color: string; label: string }) {
  return (
    <span className="inline-flex items-center gap-1.5">
      <span className="h-2.5 w-2.5 rounded-[3px]" style={{ backgroundColor: color }} />
      {label}
    </span>
  );
}

function percent(value: number | null | undefined): string {
  return value == null ? "–" : `${Math.round(value)}%`;
}

function direction(first: number | null | undefined, latest: number | null | undefined, graded: number): string {
  if (graded < 2 || first == null || latest == null) {
    return "–";
  }
  const change = Math.round(latest - first);
  if (change === 0) {
    return "Steady";
  }
  return change > 0 ? `Up ${change}%` : `Down ${-change}%`;
}

function trendTone(first: number | null | undefined, latest: number | null | undefined, graded: number): "good" | "bad" | undefined {
  if (graded < 2 || first == null || latest == null || Math.round(latest - first) === 0) {
    return undefined;
  }
  return latest > first ? "good" : "bad";
}
