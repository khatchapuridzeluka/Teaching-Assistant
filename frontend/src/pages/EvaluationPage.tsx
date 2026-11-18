import { useState } from "react";
import { Link, useSearchParams } from "react-router";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  ReferenceLine,
  ResponsiveContainer,
  Scatter,
  ScatterChart,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { ArrowRight, Gauge, ListChecks, Scale, Sparkles, Target, ThumbsUp } from "lucide-react";
import type { Evaluation, EvaluationRow } from "../api/client";
import { useCourses, useEvaluation } from "../api/hooks";
import { CourseName } from "../components/Layout";
import { categoryLabels } from "../components/StudentMistakes";
import { Card, CardHeader, Chip, EmptyState, ErrorPanel, PageHeader, Segmented, Spinner, Stat, StatStrip } from "../components/ui";
import { formatDate } from "../format";
import { chartText, palette, severityColor } from "../theme";

const decisionColor = { kept: "#1c8a4e", edited: "#e0921a", rejected: "#cf3a2b" } as const;

const round = (value: number, digits = 1) => {
  const factor = 10 ** digits;
  return Math.round(value * factor) / factor;
};

const percent = (part: number, whole: number) => (whole > 0 ? Math.round((part / whole) * 100) : null);

const signed = (value: number) => `${value > 0 ? "+" : value < 0 ? "−" : "±"}${round(Math.abs(value))}`;

export function EvaluationPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const courseId = searchParams.get("course") ?? undefined;
  const courses = useCourses();
  const evaluation = useEvaluation(courseId);
  const data = evaluation.data;

  const courseOptions = [
    { value: "all", label: "All courses" },
    ...(courses.data ?? []).map((c) => ({ value: c.id, label: c.name })),
  ];

  return (
    <>
      <PageHeader
        title="Evaluation"
        subtitle="How the AI's drafts compare with the reports you approved from them · only approved reports count, they are your final word"
        actions={
          courseOptions.length > 2 && (
            <Segmented
              label="Course"
              options={courseOptions}
              value={courseId ?? "all"}
              onChange={(value) =>
                setSearchParams(
                  (params) => {
                    if (value === "all") {
                      params.delete("course");
                    } else {
                      params.set("course", value);
                    }
                    return params;
                  },
                  { replace: true },
                )
              }
            />
          )
        }
      />

      {evaluation.isError && <ErrorPanel error={evaluation.error} />}
      {evaluation.isLoading && <Spinner />}

      {data && data.reports === 0 && (
        <Card>
          <EmptyState icon={<Scale />}>
            <p className="font-semibold text-ink">No approved reports yet</p>
            <p className="mt-1 max-w-[60ch] text-sm">
              Review a draft and press <b>Approve</b>: each approved report is compared with the AI draft it was made from, its score and
              every mistake and strength you kept, edited or rejected. About 15 to 20 approved reports give a number worth showing.
            </p>
          </EmptyState>
        </Card>
      )}

      {data && data.reports > 0 && <EvaluationBody data={data} />}
    </>
  );
}

function EvaluationBody({ data }: { data: Evaluation }) {
  const { scores, mistakes, strengths } = data;
  const agreement = percent(scores.withinTolerance, scores.compared);
  const confirmed = percent(mistakes.kept + mistakes.edited, mistakes.total);
  const strengthsKept = percent(strengths.kept + strengths.edited, strengths.total);
  const leaning = scores.meanDifference;

  return (
    <div className="flex flex-col gap-[18px]">
      <StatStrip columns={6}>
        <Stat
          icon={<Target />}
          label="Score agreement"
          value={agreement == null ? "–" : `${agreement}%`}
          note={`${scores.withinTolerance} of ${scores.compared} within ±${scores.tolerancePercent} of yours`}
          tone="mark"
        />
        <Stat
          icon={<Gauge />}
          label="Average gap"
          value={scores.meanAbsoluteDifference == null ? "–" : round(scores.meanAbsoluteDifference)}
          note="points per 100, either way"
        />
        <Stat
          icon={<Scale />}
          label="Leaning"
          value={leaning == null ? "–" : signed(leaning)}
          note={leaning == null || Math.abs(leaning) < 0.05 ? "no lean" : leaning > 0 ? "AI more lenient" : "AI stricter"}
        />
        <Stat
          icon={<ListChecks />}
          label="Mistakes confirmed"
          value={confirmed == null ? "–" : `${confirmed}%`}
          note={`${mistakes.rejected} of ${mistakes.total} rejected`}
          tone={confirmed != null && confirmed >= 80 ? "good" : undefined}
        />
        <Stat
          icon={<ThumbsUp />}
          label="Strengths kept"
          value={strengthsKept == null ? "–" : `${strengthsKept}%`}
          note={`${strengths.total} proposed`}
        />
        <Stat icon={<Sparkles />} label="Reports" value={data.reports} note="approved and compared" />
      </StatStrip>

      <div className="grid gap-[18px] xl:grid-cols-2">
        <ScoreScatter rows={data.rows} tolerance={scores.tolerancePercent} />
        <DifferenceHistogram data={data} />
      </div>

      <div className="grid gap-[18px] xl:grid-cols-2">
        <DecisionBars
          title="Mistakes by category"
          subtitle="The category the AI chose · what you did with them"
          rows={mistakes.byCategory.map((c) => ({ name: categoryLabels[c.category] ?? c.category, ...c }))}
        />
        <DecisionBars
          title="Mistakes by severity"
          subtitle={
            mistakes.pointsChanged > 0 && mistakes.meanPointsChange != null
              ? `You changed the points of ${mistakes.pointsChanged} kept mistakes, by ${round(mistakes.meanPointsChange)} on average`
              : "The severity the AI chose · what you did with them"
          }
          rows={mistakes.bySeverity.map((s) => ({ name: s.severity, ...s }))}
          colorBySeverity
        />
      </div>

      <div className="grid gap-[18px] xl:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)]">
        <ClassroomCard data={data} />
        <ModelsCard data={data} />
      </div>

      <Disagreements rows={data.rows} tolerance={scores.tolerancePercent} />
    </div>
  );
}

function ScoreScatter({ rows, tolerance }: { rows: EvaluationRow[]; tolerance: number }) {
  const points = rows
    .filter((r) => r.aiPercent != null && r.teacherPercent != null)
    .map((r) => ({ x: r.teacherPercent!, y: r.aiPercent!, row: r }));
  const top = Math.max(100, ...points.map((p) => Math.max(p.x, p.y)));
  // Start the axes a little below the lowest score, so a class scoring 60-100 fills the chart instead of its top corner.
  const lowest = Math.min(...points.map((p) => Math.min(p.x, p.y)));
  const bottom = points.length === 0 ? 0 : Math.max(0, Math.floor((lowest - 5) / 10) * 10);
  const inside = (p: { x: number; y: number }) => Math.abs(p.y - p.x) <= tolerance + 1e-9;

  return (
    <Card>
      <CardHeader title="AI score vs. yours" subtitle={`Each dot is one report, in % of the maximum · on the line = same score, green = within ±${tolerance}`} />
      <div className="h-[280px] px-2 pb-2 pt-3">
        <ResponsiveContainer width="100%" height="100%">
          <ScatterChart margin={{ top: 8, right: 16, bottom: 18, left: 0 }}>
            <CartesianGrid stroke={palette.grid} />
            <XAxis
              type="number"
              dataKey="x"
              domain={[bottom, top]}
              tick={chartText}
              tickLine={false}
              label={{ value: "Your score (%)", position: "insideBottom", offset: -10, ...chartText }}
            />
            <YAxis
              type="number"
              dataKey="y"
              domain={[bottom, top]}
              tick={chartText}
              tickLine={false}
              width={44}
              label={{ value: "AI (%)", angle: -90, position: "insideLeft", offset: 12, ...chartText }}
            />
            <ReferenceLine
              segment={[
                { x: bottom, y: bottom },
                { x: top, y: top },
              ]}
              stroke={palette.faint}
              strokeDasharray="4 4"
            />
            <Tooltip cursor={false} content={<ScatterTip />} />
            <Scatter data={points} isAnimationActive={false}>
              {points.map((p) => (
                <Cell key={p.row.reportId} fill={inside(p) ? decisionColor.kept : decisionColor.rejected} fillOpacity={0.8} />
              ))}
            </Scatter>
          </ScatterChart>
        </ResponsiveContainer>
      </div>
    </Card>
  );
}

function ScatterTip({ active, payload }: { active?: boolean; payload?: { payload?: { row?: EvaluationRow } }[] }) {
  const row = payload?.[0]?.payload?.row;
  if (!active || !row) {
    return null;
  }

  return (
    <div className="rounded-md border border-line bg-white px-3 py-2 text-xs shadow-md">
      <div className="font-semibold">{row.studentName ?? "Student"}</div>
      <div className="text-ink-muted">{row.assignmentTitle ?? "Assignment"}</div>
      <div className="mt-1">
        AI {row.aiScore ?? "–"} · you {row.teacherScore ?? "–"} / {row.maxScore ?? "–"}
      </div>
    </div>
  );
}

function bucketLabel(from: number, to: number) {
  if (from <= -25) {
    return "≤ −20";
  }
  if (to >= 25) {
    return "≥ +20";
  }
  return `${from > 0 ? "+" : from < 0 ? "−" : ""}${Math.abs(from)}…${to > 0 ? "+" : to < 0 ? "−" : ""}${Math.abs(to)}`;
}

function DifferenceHistogram({ data }: { data: Evaluation }) {
  const tolerance = data.scores.tolerancePercent;
  const bars = data.scores.buckets.map((b) => ({
    name: bucketLabel(b.from, b.to),
    count: b.count,
    close: b.from >= -tolerance && b.to <= tolerance,
  }));

  return (
    <Card>
      <CardHeader
        title="How far off"
        subtitle={`AI score minus yours, in points per 100 · left = stricter, right = more lenient · ${data.scores.exact} exact`}
      />
      <div className="h-[280px] px-2 pb-2 pt-3">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={bars} margin={{ top: 8, right: 12, bottom: 0, left: 0 }}>
            <CartesianGrid vertical={false} stroke={palette.grid} />
            <XAxis dataKey="name" tick={chartText} tickLine={false} axisLine={false} interval={0} />
            <YAxis allowDecimals={false} tick={chartText} tickLine={false} axisLine={false} width={32} />
            <Tooltip cursor={{ fill: palette.hover }} formatter={(value: unknown) => [`${value} reports`, "Reports"]} />
            <Bar dataKey="count" radius={[4, 4, 0, 0]} maxBarSize={36} isAnimationActive={false}>
              {bars.map((b) => (
                <Cell key={b.name} fill={b.close ? palette.accent : palette.accentSoft} />
              ))}
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      </div>
    </Card>
  );
}

type DecisionRow = { name: string; kept: number; edited: number; rejected: number };

function DecisionBars({
  title,
  subtitle,
  rows,
  colorBySeverity,
}: {
  title: string;
  subtitle: string;
  rows: DecisionRow[];
  colorBySeverity?: boolean;
}) {
  const height = Math.max(160, rows.length * 34 + 40);

  return (
    <Card>
      <CardHeader title={title} subtitle={subtitle} actions={<DecisionLegend />} />
      {rows.length === 0 ? (
        <p className="px-[18px] py-6 text-xs text-ink-muted">The approved drafts had no mistakes.</p>
      ) : (
        <div className="px-2 pb-2 pt-3" style={{ height }}>
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={rows} layout="vertical" margin={{ top: 0, right: 24, bottom: 0, left: 8 }}>
              <XAxis type="number" hide allowDecimals={false} />
              <YAxis
                type="category"
                dataKey="name"
                width={130}
                tickLine={false}
                axisLine={false}
                tick={({ x, y, payload }: { x: number; y: number; payload: { value: string } }) => (
                  <text x={x} y={y} dy={4} textAnchor="end" fontSize={12} fill={colorBySeverity ? severityTone(payload.value) : palette.ink}>
                    {payload.value}
                  </text>
                )}
              />
              <Tooltip cursor={{ fill: palette.hover }} />
              <Bar dataKey="kept" name="Kept" stackId="d" fill={decisionColor.kept} maxBarSize={18} isAnimationActive={false} />
              <Bar dataKey="edited" name="Edited" stackId="d" fill={decisionColor.edited} maxBarSize={18} isAnimationActive={false} />
              <Bar
                dataKey="rejected"
                name="Rejected"
                stackId="d"
                fill={decisionColor.rejected}
                maxBarSize={18}
                radius={[0, 4, 4, 0]}
                isAnimationActive={false}
              />
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}
    </Card>
  );
}

function severityTone(severity: string) {
  return severityColor[severity as keyof typeof severityColor] ?? palette.ink;
}

function DecisionLegend() {
  return (
    <span className="flex items-center gap-3 text-[11px] text-ink-muted">
      {(["kept", "edited", "rejected"] as const).map((key) => (
        <span key={key} className="flex items-center gap-1">
          <span className="h-2 w-2 rounded-sm" style={{ background: decisionColor[key] }} /> {key}
        </span>
      ))}
    </span>
  );
}

function ClassroomCard({ data }: { data: Evaluation }) {
  const { classroom } = data;
  const ai = percent(classroom.aiWithinTolerance, classroom.compared);
  const teacher = percent(classroom.teacherWithinTolerance, classroom.compared);

  return (
    <Card>
      <CardHeader title="Against Classroom grades" subtitle="An independent check: the grades you returned in Classroom, made before these drafts" />
      {classroom.compared === 0 ? (
        <p className="px-[18px] py-6 text-xs text-ink-muted">None of the compared submissions has a grade returned in Classroom yet.</p>
      ) : (
        <div className="grid grid-cols-2 gap-3 px-[18px] py-4">
          <div>
            <div className="eyebrow">AI draft</div>
            <div className="text-2xl font-semibold tabular-nums">{ai}%</div>
            <div className="text-xs text-ink-muted">
              within ±{data.scores.tolerancePercent} of the Classroom grade
              {classroom.aiMeanAbsoluteDifference != null && ` · ${round(classroom.aiMeanAbsoluteDifference)} off on average`}
            </div>
          </div>
          <div>
            <div className="eyebrow">Your report</div>
            <div className="text-2xl font-semibold tabular-nums">{teacher}%</div>
            <div className="text-xs text-ink-muted">within ±{data.scores.tolerancePercent} of it, for comparison</div>
          </div>
          <p className="col-span-2 text-[11px] text-ink-muted">{classroom.compared} reports have a Classroom grade.</p>
        </div>
      )}
    </Card>
  );
}

function ModelsCard({ data }: { data: Evaluation }) {
  return (
    <Card className="overflow-hidden">
      <CardHeader title="By model" subtitle="The model that wrote the drafts · regrade the same submissions with another model to compare" />
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-muted">
              <th className="px-[18px] py-2 font-semibold">Model</th>
              <th className="px-3 py-2 font-semibold">Reports</th>
              <th className="px-3 py-2 font-semibold">Within ±{data.scores.tolerancePercent}</th>
              <th className="px-3 py-2 font-semibold">Average gap</th>
              <th className="px-3 py-2 font-semibold">Mistakes rejected</th>
            </tr>
          </thead>
          <tbody>
            {data.models.map((m) => (
              <tr key={m.model} className="border-b border-line last:border-b-0">
                <td className="px-[18px] py-2 font-mono text-[12px]">{m.model}</td>
                <td className="px-3 py-2 tabular-nums">{m.reports}</td>
                <td className="px-3 py-2 tabular-nums">
                  {m.scoresCompared === 0 ? "–" : `${percent(m.withinTolerance, m.scoresCompared)}% (${m.withinTolerance}/${m.scoresCompared})`}
                </td>
                <td className="px-3 py-2 tabular-nums">{m.meanAbsoluteDifference == null ? "–" : round(m.meanAbsoluteDifference)}</td>
                <td className="px-3 py-2 tabular-nums">
                  {m.mistakes === 0 ? "–" : `${percent(m.mistakesRejected, m.mistakes)}% (${m.mistakesRejected}/${m.mistakes})`}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  );
}

function Disagreements({ rows, tolerance }: { rows: EvaluationRow[]; tolerance: number }) {
  const [showAll, setShowAll] = useState(false);
  const shown = showAll ? rows : rows.slice(0, 12);

  return (
    <Card className="overflow-hidden">
      <CardHeader
        title="Report by report"
        subtitle="Largest disagreement first · open one to see why"
        actions={
          rows.length > 12 && (
            <button type="button" className="text-xs font-semibold text-accent hover:underline" onClick={() => setShowAll(!showAll)}>
              {showAll ? "Show the first 12" : `Show all ${rows.length}`}
            </button>
          )
        }
      />
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-muted">
              <th className="px-[18px] py-2 font-semibold">Student</th>
              <th className="px-3 py-2 font-semibold">Assignment</th>
              <th className="px-3 py-2 font-semibold">AI</th>
              <th className="px-3 py-2 font-semibold">You</th>
              <th className="px-3 py-2 font-semibold">Gap</th>
              <th className="px-3 py-2 font-semibold">Mistakes</th>
              <th className="px-3 py-2 font-semibold">Classroom</th>
              <th className="px-3 py-2 font-semibold">Approved</th>
              <th className="px-3 py-2" />
            </tr>
          </thead>
          <tbody>
            {shown.map((r) => (
              <tr key={r.reportId} className="border-b border-line last:border-b-0">
                <td className="px-[18px] py-2 font-semibold">{r.studentName ?? "Student"}</td>
                <td className="px-3 py-2">
                  <div>{r.assignmentTitle ?? "Assignment"}</div>
                  <div className="text-[11px] text-ink-muted">
                    <CourseName courseId={r.courseId} /> · <span className="font-mono">{r.model}</span>
                  </div>
                </td>
                <td className="px-3 py-2 tabular-nums">{r.aiScore ?? "–"}</td>
                <td className="px-3 py-2 tabular-nums">
                  {r.teacherScore ?? "–"}
                  <span className="text-ink-muted"> / {r.maxScore ?? "–"}</span>
                </td>
                <td className="px-3 py-2">
                  {r.difference == null ? (
                    <span className="text-ink-muted">–</span>
                  ) : (
                    <Chip tone={Math.abs(r.difference) <= tolerance + 1e-9 ? "success" : "danger"}>{signed(r.difference)}</Chip>
                  )}
                </td>
                <td className="px-3 py-2 text-xs">
                  {r.mistakes === 0 ? (
                    <span className="text-ink-muted">none</span>
                  ) : (
                    <>
                      {r.mistakes}
                      {r.mistakesRejected > 0 && <span className="text-danger"> · {r.mistakesRejected} rejected</span>}
                      {r.mistakesEdited > 0 && <span className="text-ink-muted"> · {r.mistakesEdited} edited</span>}
                    </>
                  )}
                </td>
                <td className="px-3 py-2 tabular-nums">{r.classroomGrade ?? <span className="text-ink-muted">–</span>}</td>
                <td className="px-3 py-2 text-xs text-ink-muted">{r.approvedAt ? formatDate(r.approvedAt) : "–"}</td>
                <td className="px-3 py-2 text-right">
                  <Link
                    to={`/courses/${r.courseId}/drafts?view=all&assignment=${r.assignmentId}&draft=${r.gradingId}`}
                    className="inline-flex items-center gap-1 text-xs font-semibold text-accent hover:underline"
                  >
                    Open <ArrowRight className="h-3 w-3" />
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  );
}
