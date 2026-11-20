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
import { ArrowRight, FlaskConical, Gauge, ListChecks, Scale, Target, ThumbsUp, Trash2 } from "lucide-react";
import type { DiscardedRow, Evaluation, EvaluationRow, ModelComparison } from "../api/client";
import { useCourses, useEvaluation, useModelComparison, useStartEvaluationRun } from "../api/hooks";
import { CourseName } from "../components/Layout";
import { discardReasonLabel } from "../components/DiscardDraftButton";
import { categoryLabels } from "../components/StudentMistakes";
import { Button, Card, CardHeader, Chip, EmptyState, ErrorPanel, Meter, Modal, PageHeader, Segmented, Spinner, Stat, StatStrip } from "../components/ui";
import { formatDate } from "../format";
import { chartText, palette, severityColor } from "../theme";

// An average lean under one point per 100 is noise, not a tendency worth naming.
const noLeanBelow = 1;

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
  const model = searchParams.get("model") ?? undefined;
  const courses = useCourses();
  const evaluation = useEvaluation(courseId, model);
  const comparison = useModelComparison(courseId);
  const data = evaluation.data;
  const runModels = (comparison.data?.models ?? []).filter((m) => m.graded > 0).map((m) => m.model);
  const modelOptions = [
    { value: "drafts", label: "Your reviewed drafts" },
    ...[...new Set([...runModels, ...(model ? [model] : [])])].map((m) => ({ value: m, label: m })),
  ];
  const setParam = (key: string, value: string | undefined) =>
    setSearchParams(
      (params) => {
        if (value === undefined) {
          params.delete(key);
        } else {
          params.set(key, value);
        }
        return params;
      },
      { replace: true },
    );

  const courseOptions = [
    { value: "all", label: "All courses" },
    ...(courses.data ?? []).map((c) => ({ value: c.id, label: c.name })),
  ];

  return (
    <>
      <PageHeader
        title="Evaluation"
        subtitle={
          model
            ? `How ${model}'s side gradings compare with the reports you approved · only approved reports count, they are your final word`
            : "How the AI's drafts compare with the reports you approved from them · only approved reports count, they are your final word"
        }
        actions={
          courseOptions.length > 2 && (
            <Segmented
              label="Course"
              options={courseOptions}
              value={courseId ?? "all"}
              onChange={(value) => setParam("course", value === "all" ? undefined : value)}
            />
          )
        }
      />

      {modelOptions.length > 1 && (
        <div className="flex flex-wrap items-center gap-3">
          <span className="eyebrow">Graded by</span>
          <Segmented
            label="Graded by"
            options={modelOptions}
            value={model ?? "drafts"}
            onChange={(value) => setParam("model", value === "drafts" ? undefined : value)}
          />
          <span className="text-xs text-ink-muted">
            {model
              ? `${model}'s side gradings from evaluation runs, against your approved reports; your drafts and reports are not involved.`
              : "The drafts you reviewed, against the reports you made from them. Pick a model to see its evaluation-run gradings instead."}
          </span>
        </div>
      )}

      {evaluation.isError && <ErrorPanel error={evaluation.error} />}
      {evaluation.isLoading && <Spinner />}

      {data && data.reports === 0 && data.model != null && (
        <Card>
          <EmptyState icon={<FlaskConical />}>
            <p className="font-semibold text-ink">No side gradings by {data.model} yet</p>
            <p className="mt-1 max-w-[60ch] text-sm">Press Run evaluation below and choose this model: it grades your approved reports again on the side.</p>
          </EmptyState>
        </Card>
      )}

      {data && data.reports === 0 && data.model == null && (
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

      {data && (data.reports > 0 || data.model != null) && <ModelComparisonCard courseId={courseId} />}
    </>
  );
}

function EvaluationBody({ data }: { data: Evaluation }) {
  const { scores, mistakes, strengths, found } = data;
  const confirmed = percent(mistakes.kept + mistakes.edited, mistakes.total);
  const strengthsKept = percent(strengths.kept + strengths.edited, strengths.total);
  const leaning = scores.meanDifference;
  const who = data.model ?? "AI";

  return (
    <div className="flex flex-col gap-[18px]">
      {found && found.approvedReports > data.reports && (
        <p className="text-xs text-ink-muted">
          {data.model} has graded {data.reports} of your {found.approvedReports} approved reports so far; the numbers cover only those.
        </p>
      )}
      <StatStrip columns={6}>
        <Stat
          icon={<Target />}
          label="Score accuracy"
          value={scores.accuracy == null ? "–" : `${round(scores.accuracy)}%`}
          note={scores.meanAbsoluteDifference == null ? "no scores to compare" : `off by ${round(scores.meanAbsoluteDifference)} points per 100 on average`}
          tone="mark"
        />
        <Stat
          icon={<Gauge />}
          label={`Within ±${scores.tolerancePercent}`}
          value={`${scores.withinTolerance} / ${scores.compared}`}
          note={`reports within ${scores.tolerancePercent} points per 100 of yours`}
        />
        <Stat
          icon={<Scale />}
          label="Leaning"
          value={leaning == null ? "–" : signed(leaning)}
          note={
            leaning == null || Math.abs(leaning) < noLeanBelow
              ? `no real lean (under ${noLeanBelow} point per 100)`
              : leaning > 0
                ? `${who} more lenient`
                : `${who} stricter`
          }
        />
        {found ? (
          <>
            <Stat
              icon={<ListChecks />}
              label="Your mistakes found"
              value={found.teacherMistakes === 0 ? "–" : `${percent(found.found, found.teacherMistakes)}%`}
              note={`${found.found} of the ${found.teacherMistakes} you kept`}
              tone={found.teacherMistakes > 0 && found.found / found.teacherMistakes >= 0.8 ? "good" : undefined}
            />
            <Stat
              icon={<ThumbsUp />}
              label="Rejected ones repeated"
              value={found.repeatedFalseAlarms}
              note="mistakes you had rejected, flagged again"
              tone={found.repeatedFalseAlarms > 0 ? "bad" : undefined}
            />
            <Stat icon={<Trash2 />} label="Not in your report" value={found.unreviewed} note="flagged, but nobody has judged them" />
          </>
        ) : (
          <>
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
            <Stat
              icon={<Trash2 />}
              label="Discarded as poor"
              value={data.discards.poorGrading}
              note={`of ${data.reports + data.discards.poorGrading} drafts you decided on`}
              tone={data.discards.poorGrading > 0 ? "bad" : undefined}
            />
          </>
        )}
      </StatStrip>

      <div className="grid gap-[18px] xl:grid-cols-2">
        <ScoreScatter rows={data.rows} tolerance={scores.tolerancePercent} who={who} />
        <DifferenceHistogram data={data} />
      </div>

      {found ? (
        <div className="grid gap-[18px] xl:grid-cols-2">
          <DecisionBars
            title="Your mistakes by category"
            subtitle={`The mistakes you kept, by the category you gave them · how many ${who} found`}
            rows={found.byCategory.map((c) => ({ name: categoryLabels[c.category] ?? c.category, kept: c.found, edited: 0, rejected: c.teacherMistakes - c.found }))}
            legend={{ kept: "found", rejected: "missed" }}
          />
          <DecisionBars
            title="Your mistakes by severity"
            subtitle={`The mistakes you kept, by the severity you gave them · how many ${who} found`}
            rows={found.bySeverity.map((s) => ({ name: s.severity, kept: s.found, edited: 0, rejected: s.teacherMistakes - s.found }))}
            legend={{ kept: "found", rejected: "missed" }}
            colorBySeverity
          />
        </div>
      ) : (
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
      )}

      {found ? (
        <ClassroomCard data={data} />
      ) : (
        <div className="grid gap-[18px] xl:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)]">
          <ClassroomCard data={data} />
          <ModelsCard data={data} />
        </div>
      )}

      <Disagreements rows={data.rows} tolerance={scores.tolerancePercent} model={data.model ?? undefined} />

      {!found && <DiscardedCard data={data} />}
    </div>
  );
}

function ScoreScatter({ rows, tolerance, who }: { rows: EvaluationRow[]; tolerance: number; who: string }) {
  const points = rows
    .filter((r) => r.aiPercent != null && r.teacherPercent != null)
    .map((r) => ({ x: r.teacherPercent!, y: r.aiPercent!, row: r }));
  // Whole numbers, so the top tick reads 110 rather than 110.00000000001.
  const top = Math.ceil(Math.max(100, ...points.map((p) => Math.max(p.x, p.y))));
  // Start the axes a little below the lowest score, so a class scoring 60-100 fills the chart instead of its top corner.
  const lowest = Math.min(...points.map((p) => Math.min(p.x, p.y)));
  const bottom = points.length === 0 ? 0 : Math.max(0, Math.floor((lowest - 5) / 10) * 10);
  const inside = (p: { x: number; y: number }) => Math.abs(p.y - p.x) <= tolerance + 1e-9;

  return (
    <Card>
      <CardHeader title={`${who} score vs. yours`} subtitle={`Each dot is one report, in % of the maximum · on the line = same score, green = within ±${tolerance}`} />
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
        subtitle={`${data.model ?? "AI"} score minus yours, in points per 100 · left = stricter, right = more lenient · ${data.scores.exact} exact`}
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

/** Names for the green and red parts when they mean something else than kept and rejected (found and missed); edited is then left out. */
type LegendNames = { kept: string; rejected: string };

function DecisionBars({
  title,
  subtitle,
  rows,
  colorBySeverity,
  legend,
}: {
  title: string;
  subtitle: string;
  rows: DecisionRow[];
  colorBySeverity?: boolean;
  legend?: LegendNames;
}) {
  const height = Math.max(160, rows.length * 34 + 40);
  const keptName = legend?.kept ?? "kept";
  const rejectedName = legend?.rejected ?? "rejected";

  return (
    <Card>
      <CardHeader title={title} subtitle={subtitle} actions={<DecisionLegend names={legend} />} />
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
              <Bar dataKey="kept" name={capitalize(keptName)} stackId="d" fill={decisionColor.kept} maxBarSize={18} isAnimationActive={false} />
              {!legend && <Bar dataKey="edited" name="Edited" stackId="d" fill={decisionColor.edited} maxBarSize={18} isAnimationActive={false} />}
              <Bar
                dataKey="rejected"
                name={capitalize(rejectedName)}
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

function capitalize(text: string) {
  return text.charAt(0).toUpperCase() + text.slice(1);
}

function DecisionLegend({ names }: { names?: LegendNames }) {
  const keys = names ? (["kept", "rejected"] as const) : (["kept", "edited", "rejected"] as const);
  return (
    <span className="flex items-center gap-3 text-[11px] text-ink-muted">
      {keys.map((key) => (
        <span key={key} className="flex items-center gap-1">
          <span className="h-2 w-2 rounded-sm" style={{ background: decisionColor[key] }} />{" "}
          {key === "kept" ? (names?.kept ?? "kept") : key === "rejected" ? (names?.rejected ?? "rejected") : "edited"}
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
            <div className="eyebrow">{data.model ?? "AI draft"}</div>
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
              <th className="px-3 py-2 font-semibold">Accuracy</th>
              <th className="px-3 py-2 font-semibold">Within ±{data.scores.tolerancePercent}</th>
              <th className="px-3 py-2 font-semibold">Average gap</th>
              <th className="px-3 py-2 font-semibold">Mistakes rejected</th>
              <th className="px-3 py-2 font-semibold">Discarded as poor</th>
            </tr>
          </thead>
          <tbody>
            {data.models.map((m) => (
              <tr key={m.model} className="border-b border-line last:border-b-0">
                <td className="px-[18px] py-2 font-mono text-[12px]">{m.model}</td>
                <td className="px-3 py-2 tabular-nums">{m.reports}</td>
                <td className="px-3 py-2 font-semibold tabular-nums">{m.accuracy == null ? "–" : `${round(m.accuracy)}%`}</td>
                <td className="px-3 py-2 tabular-nums">
                  {m.scoresCompared === 0 ? "–" : `${percent(m.withinTolerance, m.scoresCompared)}% (${m.withinTolerance}/${m.scoresCompared})`}
                </td>
                <td className="px-3 py-2 tabular-nums">{m.meanAbsoluteDifference == null ? "–" : round(m.meanAbsoluteDifference)}</td>
                <td className="px-3 py-2 tabular-nums">
                  {m.mistakes === 0 ? "–" : `${percent(m.mistakesRejected, m.mistakes)}% (${m.mistakesRejected}/${m.mistakes})`}
                </td>
                <td className="px-3 py-2 tabular-nums">{m.discardedPoorGrading}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  );
}

function Disagreements({ rows, tolerance, model }: { rows: EvaluationRow[]; tolerance: number; model?: string }) {
  const [showAll, setShowAll] = useState(false);
  const shown = showAll ? rows : rows.slice(0, 12);

  return (
    <Card className="overflow-hidden">
      <CardHeader
        title="Report by report"
        subtitle={model ? `Largest disagreement first · ${model}'s grading against your report · Open shows the draft you reviewed` : "Largest disagreement first · open one to see why"}
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
              <th className="px-3 py-2 font-semibold">{model ? "Model" : "AI"}</th>
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
                      {r.mistakesRejected > 0 && (
                        <span className="text-danger">
                          {" "}
                          · {r.mistakesRejected} {model ? "you had rejected" : "rejected"}
                        </span>
                      )}
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

function DiscardedCard({ data }: { data: Evaluation }) {
  const { discards } = data;
  const [showAutomatic, setShowAutomatic] = useState(false);
  const automatic = discards.rows.filter((r) => r.reason === "Superseded").length;
  const shown = showAutomatic ? discards.rows : discards.rows.filter((r) => r.reason !== "Superseded");

  return (
    <Card className="overflow-hidden">
      <CardHeader
        icon={<Trash2 />}
        title="Discarded drafts"
        subtitle="Drafts you threw away and why · Poor grading counts against the AI; drafts set aside because another draft was approved do not"
        actions={
          automatic > 0 && (
            <button type="button" className="text-xs font-semibold text-accent hover:underline" onClick={() => setShowAutomatic(!showAutomatic)}>
              {showAutomatic ? "Hide the ones set aside automatically" : `Also show ${automatic} set aside automatically`}
            </button>
          )
        }
      />
      {discards.total === 0 ? (
        <p className="px-[18px] py-6 text-xs text-ink-muted">No draft has been discarded.</p>
      ) : (
        <>
          <div className="flex flex-wrap gap-2 px-[18px] pt-3">
            {discards.byReason.map((r) => (
              <Chip key={r.reason ?? "none"} tone={r.reason === "PoorGrading" ? "danger" : "muted"}>
                {r.reason ? discardReasonLabel[r.reason] : "No reason recorded"} · {r.count}
              </Chip>
            ))}
          </div>
          {shown.length === 0 ? (
            <p className="px-[18px] py-6 text-xs text-ink-muted">Only drafts set aside automatically; none discarded by you.</p>
          ) : (
            <div className="mt-2 overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-muted">
                    <th className="px-[18px] py-2 font-semibold">Student</th>
                    <th className="px-3 py-2 font-semibold">Assignment</th>
                    <th className="px-3 py-2 font-semibold">AI score</th>
                    <th className="px-3 py-2 font-semibold">Mistakes</th>
                    <th className="px-3 py-2 font-semibold">Why</th>
                    <th className="px-3 py-2 font-semibold">Discarded</th>
                    <th className="px-3 py-2" />
                  </tr>
                </thead>
                <tbody>
                  {shown.map((r) => (
                    <DiscardedLine key={r.gradingId} row={r} />
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </Card>
  );
}

function DiscardedLine({ row }: { row: DiscardedRow }) {
  return (
    <tr className="border-b border-line last:border-b-0 align-top">
      <td className="px-[18px] py-2 font-semibold">{row.studentName}</td>
      <td className="px-3 py-2">
        <div>{row.assignmentTitle ?? "Assignment"}</div>
        <div className="text-[11px] text-ink-muted">
          <CourseName courseId={row.courseId} /> · <span className="font-mono">{row.model}</span>
        </div>
      </td>
      <td className="px-3 py-2 tabular-nums">
        {row.aiScore ?? "–"}
        <span className="text-ink-muted"> / {row.maxScore ?? "–"}</span>
      </td>
      <td className="px-3 py-2 text-xs">
        {row.major + row.minor + row.style === 0 ? (
          <span className="text-ink-muted">none</span>
        ) : (
          <span className="flex gap-2">
            <span style={{ color: severityColor.Major }}>{row.major} major</span>
            <span style={{ color: severityColor.Minor }}>{row.minor} minor</span>
            <span style={{ color: severityColor.Style }}>{row.style} style</span>
          </span>
        )}
      </td>
      <td className="max-w-[360px] px-3 py-2">
        <Chip tone={row.reason === "PoorGrading" ? "danger" : "muted"}>{row.reason ? discardReasonLabel[row.reason] : "No reason recorded"}</Chip>
        {row.note && <div className="mt-1 text-xs text-ink-muted">{row.note}</div>}
        {row.reportStatus === "Approved" && <div className="mt-1 text-[11px] text-ink-muted">Its report had been approved.</div>}
      </td>
      <td className="px-3 py-2 text-xs text-ink-muted">{row.discardedAt ? formatDate(row.discardedAt) : "–"}</td>
      <td className="px-3 py-2 text-right">
        <Link
          to={`/courses/${row.courseId}/drafts?view=all&assignment=${row.assignmentId}&draft=${row.gradingId}&earlier=1`}
          className="inline-flex items-center gap-1 text-xs font-semibold text-accent hover:underline"
        >
          Open <ArrowRight className="h-3 w-3" />
        </Link>
      </td>
    </tr>
  );
}

function ModelComparisonCard({ courseId }: { courseId?: string }) {
  const comparison = useModelComparison(courseId);
  const [choosing, setChoosing] = useState(false);
  const data = comparison.data;
  const run = data?.latestRun;
  const running = (run?.pending ?? 0) > 0;

  return (
    <Card className="overflow-hidden">
      <CardHeader
        icon={<FlaskConical />}
        title="Model comparison"
        subtitle="Your approved reports graded again on the side by other models: the same homework, prompt, rules and compile check, against your same final word. Nothing becomes a draft."
        actions={
          data && (
            <Button small variant="primary" icon={<FlaskConical />} onClick={() => setChoosing(true)} disabled={running || data.approvedReports === 0}>
              Run evaluation
            </Button>
          )
        }
      />
      {comparison.isError && (
        <div className="px-[18px] py-3">
          <ErrorPanel error={comparison.error} />
        </div>
      )}
      {run && (
        <div className="flex flex-wrap items-center gap-3 border-b border-line bg-paper/50 px-[18px] py-2.5 text-xs text-ink-muted">
          <span className="font-semibold text-ink">{running ? "Running" : "Latest run"}</span>
          <span>{formatDate(run.createdAt)}</span>
          <span className="font-mono text-[11px]">{run.models.join(", ")}</span>
          <Meter percent={run.total > 0 ? ((run.done + run.failed) / run.total) * 100 : 0} className="w-28" />
          <span className="tabular-nums">
            {run.done} of {run.total} graded{run.failed > 0 && ` · ${run.failed} failed`}
          </span>
        </div>
      )}
      {data && data.models.length === 0 ? (
        <p className="px-[18px] py-6 text-xs text-ink-muted">
          {running
            ? "The first results appear here as the side gradings finish."
            : `Press Run evaluation and choose the models: each of your ${data.approvedReports} approved reports is graded again with every model you choose.`}
        </p>
      ) : (
        data && <ModelTable data={data} />
      )}
      {choosing && data && <RunDialog data={data} courseId={courseId} onClose={() => setChoosing(false)} />}
    </Card>
  );
}

function ModelTable({ data }: { data: ModelComparison }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-muted">
            <th className="px-[18px] py-2 font-semibold">Model</th>
            <th className="px-3 py-2 font-semibold">Graded</th>
            <th className="px-3 py-2 font-semibold">Score accuracy</th>
            <th className="px-3 py-2 font-semibold">Within ±5</th>
            <th className="px-3 py-2 font-semibold">Leaning</th>
            <th className="px-3 py-2 font-semibold" title="Of the mistakes you kept, how many this model found again">
              Your mistakes found
            </th>
            <th className="px-3 py-2 font-semibold" title="Mistakes it flagged that you had rejected">
              Rejected ones repeated
            </th>
            <th className="px-3 py-2 font-semibold" title="Mistakes it flagged that match nothing in your report, so nobody has judged them">
              Not in your report
            </th>
            <th className="px-3 py-2 font-semibold">Time / grading</th>
          </tr>
        </thead>
        <tbody>
          {data.models.map((m) => (
            <tr key={m.model} className="border-b border-line last:border-b-0">
              <td className="px-[18px] py-2">
                <span className="whitespace-nowrap font-mono text-[12px]">{m.model}</span>
                {m.model === data.gradingModel && (
                  <Chip tone="info" className="ml-2">
                    grades now
                  </Chip>
                )}
              </td>
              <td className="px-3 py-2 tabular-nums">
                {m.graded}
                {m.failed > 0 && <span className="text-danger"> · {m.failed} failed</span>}
              </td>
              <td className="px-3 py-2 font-semibold tabular-nums">{m.accuracy == null ? "–" : `${round(m.accuracy)}%`}</td>
              <td className="px-3 py-2 tabular-nums">
                {m.withinTolerance} / {m.scoresCompared}
              </td>
              <td className="px-3 py-2 tabular-nums">{m.meanDifference == null ? "–" : signed(m.meanDifference)}</td>
              <td className="px-3 py-2 tabular-nums">
                {m.foundPercent == null ? "–" : `${Math.round(m.foundPercent)}% (${m.found}/${m.teacherMistakes})`}
              </td>
              <td className="px-3 py-2 tabular-nums">{m.repeatedFalseAlarms}</td>
              <td className="px-3 py-2 tabular-nums">{m.unreviewed}</td>
              <td className="px-3 py-2 tabular-nums">{m.averageSeconds == null ? "–" : `${Math.round(m.averageSeconds)} s`}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function RunDialog({ data, courseId, onClose }: { data: ModelComparison; courseId?: string; onClose: () => void }) {
  const start = useStartEvaluationRun();
  const [models, setModels] = useState<string[]>(data.availableModels.filter((m) => m !== "claude-fable-5-2"));
  const toggle = (model: string) => setModels((current) => (current.includes(model) ? current.filter((m) => m !== model) : [...current, model]));

  return (
    <Modal
      title="Run evaluation"
      subtitle={`Grades your ${data.approvedReports} approved reports again with each chosen model, in the background`}
      onClose={onClose}
      busy={start.isPending}
      size="max-w-lg"
      footer={
        <>
          <Button onClick={onClose} disabled={start.isPending}>
            Cancel
          </Button>
          <Button
            variant="primary"
            disabled={models.length === 0 || start.isPending}
            onClick={() => start.mutate({ models, courseId }, { onSuccess: onClose })}
          >
            {start.isPending ? "Starting…" : `Grade ${data.approvedReports * models.length} times`}
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-2 px-5 py-4">
        {data.availableModels.map((model) => (
          <label
            key={model}
            className={`flex cursor-pointer items-center gap-3 rounded-lg border px-3 py-2 ${models.includes(model) ? "border-accent bg-accent-tint" : "border-line hover:bg-paper"}`}
          >
            <input type="checkbox" checked={models.includes(model)} onChange={() => toggle(model)} />
            <span className="font-mono text-[13px]">{model}</span>
            {model === data.gradingModel && <Chip tone="info">grades now</Chip>}
          </label>
        ))}
        <p className="text-xs text-ink-muted">
          Include the model that grades now: its fresh gradings start from scratch, like the others, while your reports were made by editing its
          drafts. Each grading is a paid AI call; nothing changes in your drafts, reports or student profiles.
        </p>
        {start.isError && <ErrorPanel error={start.error} />}
      </div>
    </Modal>
  );
}
