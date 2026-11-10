import type { ReactNode } from "react";
import { Link, useParams, useSearchParams } from "react-router";
import { Bar, BarChart, CartesianGrid, LabelList, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { MistakeCategory, MistakeLabel, MistakeSeverity, StudentMistakeFilter } from "../api/client";
import { useCourseStudents, useStudentMistakes } from "../api/hooks";
import { FilterChips } from "../components/FilterChips";
import { CourseName } from "../components/Layout";
import { StudentOverviewCard } from "../components/StudentOverview";
import { categoryLabels, MistakeRow } from "../components/StudentMistakes";
import { Avatar, Button, Card, CardHeader, EmptyState, ErrorPanel, PageHeader, Spinner } from "../components/ui";
import { formatDay, initials } from "../format";

const severityShades: Record<MistakeSeverity, string> = {
  Major: "#1f4a3d",
  Minor: "#4f8c76",
  Style: "#a8d1c1",
};

const severities: MistakeSeverity[] = ["Major", "Minor", "Style"];

const severityKeys: Record<MistakeSeverity, "major" | "minor" | "styleCount"> = {
  Major: "major",
  Minor: "minor",
  Style: "styleCount",
};

export function StudentMistakesPage() {
  const { courseId = "", userId = "" } = useParams();
  const [searchParams, setSearchParams] = useSearchParams();
  const filter: StudentMistakeFilter = {
    assignmentId: searchParams.get("assignment") ?? undefined,
    category: (searchParams.get("category") as MistakeCategory | null) ?? undefined,
    severity: (searchParams.get("severity") as MistakeSeverity | null) ?? undefined,
    label: (searchParams.get("label") as MistakeLabel | null) ?? undefined,
  };
  const filtered = Object.values(filter).some((v) => v !== undefined);
  const mistakes = useStudentMistakes(courseId, userId, filter);
  const student = useCourseStudents(courseId).data?.students.find((s) => s.externalUserId === userId);
  const data = mistakes.data;

  function setFilter(key: string, value: string | undefined) {
    setSearchParams(
      (params) => {
        if (value) {
          params.set(key, value);
        } else {
          params.delete(key);
        }
        return params;
      },
      { replace: true },
    );
  }

  const repeated = data?.labels.find((l) => l.key === "Repeated");
  const major = data?.severities.find((s) => s.key === "Major");

  return (
    <>
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            {student && <Avatar text={initials(student.name)} userId={student.externalUserId} />}
            <span>Mistakes · {student?.name ?? "Student"}</span>
          </span>
        }
        subtitle={
          <>
            <CourseName courseId={courseId} /> · from approved reports · labels compare with assignments due earlier
          </>
        }
        actions={
          <span className="flex flex-wrap gap-2">
            <Link to={`/courses/${courseId}/students/${userId}/strengths`}>
              <Button>Strengths →</Button>
            </Link>
            <Link to={`/courses/${courseId}/students${student ? `?student=${student.id}` : ""}`}>
              <Button>← Profile</Button>
            </Link>
          </span>
        }
      />

      {mistakes.isError && <ErrorPanel error={mistakes.error} />}
      {mistakes.isLoading && <Spinner />}

      {data && data.recorded === 0 && (
        <Card>
          <EmptyState>
            <div className="font-semibold text-ink">No recorded mistakes yet</div>
            Approve a report for this student (Edit draft → Approve) and its kept mistakes are analyzed here.
          </EmptyState>
        </Card>
      )}

      {data && data.recorded > 0 && (
        <>
          <StudentOverviewCard
            courseId={courseId}
            userId={userId}
            studentName={student?.name ?? "this student"}
            returnTo={`/courses/${courseId}/students/${userId}/mistakes`}
            kind="Mistakes"
          />
          <Card className="flex flex-col gap-3 px-[18px] py-3">
            <div className="flex flex-wrap items-center gap-2">
              <span className="mr-1 text-[11px] font-semibold uppercase tracking-[0.06em] text-ink-muted">Assignment</span>
              <select
                value={filter.assignmentId ?? ""}
                onChange={(e) => setFilter("assignment", e.target.value || undefined)}
                className="h-8 rounded-lg border border-line bg-white px-2 text-xs"
              >
                <option value="">All assignments</option>
                {data.assignmentOptions.map((a) => (
                  <option key={a.assignmentId} value={a.assignmentId}>
                    {a.title ?? a.assignmentId} · {a.count}
                  </option>
                ))}
              </select>
              {filtered && (
                <button onClick={() => setSearchParams({}, { replace: true })} className="ml-auto text-xs font-semibold text-accent hover:underline">
                  Clear filters
                </button>
              )}
            </div>
            <FilterChips
              label="Severity"
              options={[{ value: undefined, label: "All" }, ...severities.map((s) => ({ value: s, label: s }))]}
              value={filter.severity}
              onChange={(v) => setFilter("severity", v)}
            />
            <FilterChips
              label="Label"
              options={[
                { value: undefined, label: "All" },
                { value: "New" as MistakeLabel, label: "New" },
                { value: "Repeated" as MistakeLabel, label: "Repeated" },
              ]}
              value={filter.label}
              onChange={(v) => setFilter("label", v)}
            />
            <FilterChips
              label="Category"
              options={[
                { value: undefined, label: "All" },
                ...Object.keys(categoryLabels).map((c) => ({ value: c as MistakeCategory, label: categoryLabels[c]! })),
              ]}
              value={filter.category}
              onChange={(v) => setFilter("category", v)}
            />
          </Card>

          <div className="grid grid-cols-2 border-t border-line md:grid-cols-3 xl:grid-cols-6">
            <Stat label="Mistakes" value={data.total} note={filtered ? `of ${data.recorded} recorded` : "recorded"} />
            <Stat
              label="Points lost · Classroom"
              value={data.classroomPointsLost == null ? "–" : round(data.classroomPointsLost)}
              note={
                data.classroomGraded > 0
                  ? `max − returned grade, ${data.classroomGraded} ${data.classroomGraded === 1 ? "assignment" : "assignments"}`
                  : "no grade returned in Classroom yet"
              }
            />
            <Stat label="Noted in reports" value={round(data.pointsLost)} note="per-mistake deductions you approved" />
            <Stat label="Major" value={major ? `${round(major.percent)}%` : "–"} note={`${major?.count ?? 0} of ${data.total}`} />
            <Stat label="Repeated" value={repeated ? `${round(repeated.percent)}%` : "–"} note="same category, earlier assignment" />
            <Stat label="Categories" value={data.categories.length} note="with at least one mistake" />
          </div>

          <ScoresCard assignments={data.assignments} />

          {data.total === 0 ? (
            <Card>
              <EmptyState>No mistakes match these filters.</EmptyState>
            </Card>
          ) : (
            <>
              <div className="grid gap-4 xl:grid-cols-2">
                <Card>
                  <CardHeader title="Share by category" subtitle="Count and share of the mistakes shown · click a bar to filter" />
                  <div className="px-2 py-3" style={{ height: Math.max(140, data.categories.length * 38 + 30) }}>
                    <ResponsiveContainer width="100%" height="100%">
                      <BarChart data={data.categories.map((c) => ({ ...c, name: categoryLabels[c.category] ?? c.category }))} layout="vertical" margin={{ top: 0, right: 72, bottom: 0, left: 8 }}>
                        <XAxis type="number" hide allowDecimals={false} />
                        <YAxis type="category" dataKey="name" width={130} tick={{ fontSize: 12, fill: "#1f2326" }} tickLine={false} axisLine={false} />
                        <Tooltip cursor={{ fill: "rgba(31,35,38,0.06)" }} content={<CategoryTooltip />} />
                        <Bar
                          dataKey="count"
                          fill="#2f5e4e"
                          radius={[0, 4, 4, 0]}
                          maxBarSize={18}
                          isAnimationActive={false}
                          cursor="pointer"
                          onClick={(item) => setFilter("category", (item.payload as { category?: MistakeCategory } | undefined)?.category)}
                        >
                          <LabelList
                            dataKey="percent"
                            position="right"
                            formatter={(value: unknown) => `${round(Number(value))}%`}
                            style={{ fontSize: 11, fill: "#6b6e70" }}
                          />
                        </Bar>
                      </BarChart>
                    </ResponsiveContainer>
                  </div>
                </Card>

                <Card>
                  <CardHeader title="Per assignment" subtitle="By severity, in due-date order · click a bar to filter" />
                  <div className="px-[18px] pt-3">
                    <SeverityLegend />
                  </div>
                  <div className="h-[220px] px-2 pb-3 pt-1">
                    <ResponsiveContainer width="100%" height="100%">
                      <BarChart
                        data={data.assignments.map((a) => ({
                          assignmentId: a.assignmentId,
                          name: a.title ?? "Assignment",
                          total: a.total,
                          major: a.major,
                          minor: a.minor,
                          styleCount: a.style,
                          repeated: a.repeated,
                          pointsLost: a.pointsLost,
                        }))}
                        margin={{ top: 8, right: 12, bottom: 0, left: 0 }}
                      >
                        <CartesianGrid vertical={false} stroke="#ecebe7" />
                        <XAxis dataKey="name" tick={{ fontSize: 11, fill: "#6b6e70" }} tickLine={false} axisLine={{ stroke: "#dedcd7" }} />
                        <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: "#6b6e70" }} tickLine={false} axisLine={false} width={28} />
                        <Tooltip cursor={{ fill: "rgba(31,35,38,0.06)" }} content={<AssignmentTooltip />} />
                        {severities.map((s, i) => (
                          <Bar
                            key={s}
                            dataKey={severityKeys[s]}
                            name={s}
                            stackId="severity"
                            fill={severityShades[s]}
                            stroke="#ffffff"
                            strokeWidth={2}
                            maxBarSize={36}
                            radius={i === severities.length - 1 ? [4, 4, 0, 0] : 0}
                            isAnimationActive={false}
                            cursor="pointer"
                            onClick={(item) => setFilter("assignment", (item.payload as { assignmentId?: string } | undefined)?.assignmentId)}
                          />
                        ))}
                      </BarChart>
                    </ResponsiveContainer>
                  </div>
                </Card>
              </div>

              <div className="grid gap-4 md:grid-cols-2">
                <ShareCard
                  title="Severity"
                  rows={data.severities.map((s) => ({ key: s.key, label: s.key, count: s.count, percent: s.percent, color: severityShades[s.key] }))}
                  onPick={(key) => setFilter("severity", key)}
                />
                <ShareCard
                  title="New or repeated"
                  rows={data.labels.map((l) => ({
                    key: l.key,
                    label: l.key === "Repeated" ? "Repeated from an earlier assignment" : "New",
                    count: l.count,
                    percent: l.percent,
                    color: l.key === "Repeated" ? "#1f2326" : "#9fc7b8",
                  }))}
                  onPick={(key) => setFilter("label", key)}
                />
              </div>

              <Card>
                <CardHeader
                  title="Mistakes"
                  subtitle={`${data.total} shown · newest assignment first${data.assignments.length > 1 ? "" : ""}`}
                />
                <MistakeList mistakes={data.mistakes} />
              </Card>
            </>
          )}
        </>
      )}
    </>
  );
}

type AssignmentScores = NonNullable<ReturnType<typeof useStudentMistakes>["data"]>["assignments"][number];

function ScoresCard({ assignments }: { assignments: AssignmentScores[] }) {
  if (assignments.length === 0) {
    return null;
  }

  return (
    <Card>
      <CardHeader
        title="Scores"
        subtitle="Classroom is the real grade, as returned to the student · your report and the AI draft are shown for comparison"
      />
      <div className="overflow-x-auto">
        <table className="w-full min-w-[640px] text-left text-xs tabular-nums">
          <thead className="text-ink-muted">
            <tr className="border-b border-line">
              <th className="px-[18px] py-2 font-semibold">Assignment</th>
              <th className="px-3 py-2 font-semibold">Classroom (real)</th>
              <th className="px-3 py-2 font-semibold">Your report</th>
              <th className="px-3 py-2 font-semibold">AI draft</th>
              <th className="px-3 py-2 font-semibold">Report vs Classroom</th>
            </tr>
          </thead>
          <tbody>
            {assignments.map((a) => (
              <ScoreRow key={a.assignmentId} scores={a} />
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  );
}

function ScoreRow({ scores }: { scores: AssignmentScores }) {
  const max = scores.maxPoints != null ? ` / ${round(scores.maxPoints)}` : "";
  const reportTotal = scores.reportScore != null ? scores.reportScore + (scores.reportBonus ?? 0) : null;
  const difference = reportTotal != null && scores.classroomFinal != null ? scores.classroomFinal - reportTotal : null;
  const improved = scores.classroomInitial != null && scores.classroomFinal != null && scores.classroomInitial !== scores.classroomFinal;

  return (
    <tr className="border-b border-line last:border-b-0">
      <td className="px-[18px] py-2 font-semibold">{scores.title ?? "Assignment"}</td>
      <td className="px-3 py-2">
        {scores.classroomFinal == null ? (
          <span className="text-ink-muted">not returned</span>
        ) : (
          <>
            {improved && <span className="text-ink-muted">{round(scores.classroomInitial!)} → </span>}
            <b className="text-[13px]">{round(scores.classroomFinal)}</b>
            <span className="text-ink-muted">{max}</span>
          </>
        )}
      </td>
      <td className="px-3 py-2">
        {scores.reportScore == null ? (
          <span className="text-ink-muted">–</span>
        ) : (
          <>
            {round(scores.reportScore)}
            {scores.reportBonus ? <span className="text-ink-muted"> +{round(scores.reportBonus)}</span> : null}
          </>
        )}
      </td>
      <td className="px-3 py-2 text-ink-muted">
        {scores.aiScore == null ? (
          "–"
        ) : (
          <>
            {round(scores.aiScore)}
            {scores.aiBonus ? ` +${round(scores.aiBonus)}` : ""}
          </>
        )}
      </td>
      <td className="px-3 py-2">
        {difference == null ? (
          <span className="text-ink-muted">–</span>
        ) : difference === 0 ? (
          <span className="text-ink-muted">same</span>
        ) : (
          <span className="font-semibold text-[#8f2d2d]" title="Classroom grade minus your report's score (with bonus)">
            Classroom {difference > 0 ? "+" : "−"}
            {round(Math.abs(difference))}
          </span>
        )}
      </td>
    </tr>
  );
}

function MistakeList({ mistakes }: { mistakes: NonNullable<ReturnType<typeof useStudentMistakes>["data"]>["mistakes"] }) {
  const groups: { id: string; title?: string | null; dueAt?: string | null; items: typeof mistakes }[] = [];
  for (const mistake of mistakes) {
    const last = groups[groups.length - 1];
    if (last && last.id === mistake.assignmentId) {
      last.items.push(mistake);
    } else {
      groups.push({ id: mistake.assignmentId, title: mistake.assignmentTitle, dueAt: mistake.assignmentDueAt, items: [mistake] });
    }
  }

  return (
    <div>
      {groups.map((group) => (
        <div key={group.id} className="border-b border-paper-dark last:border-b-0">
          <div className="flex items-center justify-between gap-2 bg-paper/60 px-4 py-1.5 text-[11px]">
            <b>{group.title ?? "Assignment"}</b>
            <span className="text-ink-muted">
              {group.items.length} · {group.dueAt ? `due ${formatDay(group.dueAt)}` : ""}
            </span>
          </div>
          <ul className="divide-y divide-paper-dark">
            {group.items.map((m) => (
              <MistakeRow key={m.id} mistake={m} />
            ))}
          </ul>
        </div>
      ))}
    </div>
  );
}

function ShareCard<K extends string>({
  title,
  rows,
  onPick,
}: {
  title: string;
  rows: { key: K; label: string; count: number; percent: number; color: string }[];
  onPick: (key: K) => void;
}) {
  return (
    <Card>
      <CardHeader title={title} subtitle="Share of the mistakes shown · click to filter" />
      <div className="flex flex-col gap-2 px-[18px] py-3">
        <div className="flex h-3 overflow-hidden rounded-full bg-paper-dark" aria-hidden>
          {rows
            .filter((r) => r.count > 0)
            .map((r) => (
              <div key={r.key} className="h-full border-r-2 border-white last:border-r-0" style={{ width: `${r.percent}%`, backgroundColor: r.color }} />
            ))}
        </div>
        <ul className="flex flex-col gap-1">
          {rows.map((r) => (
            <li key={r.key}>
              <button onClick={() => onPick(r.key)} className="flex w-full items-center gap-2 rounded-md px-1 py-1 text-left text-xs hover:bg-paper">
                <span className="h-2.5 w-2.5 shrink-0 rounded-[3px]" style={{ backgroundColor: r.color }} />
                <span className="flex-1">{r.label}</span>
                <b className="tabular-nums">{r.count}</b>
                <span className="w-12 text-right tabular-nums text-ink-muted">{round(r.percent)}%</span>
              </button>
            </li>
          ))}
        </ul>
      </div>
    </Card>
  );
}

function SeverityLegend() {
  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-[11px] text-ink-muted">
      {severities.map((s) => (
        <span key={s} className="inline-flex items-center gap-1.5">
          <span className="h-2.5 w-2.5 rounded-[3px]" style={{ backgroundColor: severityShades[s] }} />
          {s}
        </span>
      ))}
    </div>
  );
}

type TooltipProps<T> = { active?: boolean; payload?: { payload?: T }[] };

function CategoryTooltip({ active, payload }: TooltipProps<{ name: string; count: number; percent: number; pointsLost: number; assignments: number }>) {
  const row = active ? payload?.[0]?.payload : undefined;
  if (!row) {
    return null;
  }
  return (
    <TooltipBox title={row.name}>
      <TooltipLine label="Mistakes" value={`${row.count} · ${round(row.percent)}%`} />
      <TooltipLine label="Assignments" value={String(row.assignments)} />
      {row.pointsLost > 0 && <TooltipLine label="Points lost" value={round(row.pointsLost)} />}
    </TooltipBox>
  );
}

function AssignmentTooltip({
  active,
  payload,
}: TooltipProps<{ name: string; major: number; minor: number; styleCount: number; total: number; repeated: number; pointsLost: number }>) {
  const row = active ? payload?.[0]?.payload : undefined;
  if (!row) {
    return null;
  }
  return (
    <TooltipBox title={`${row.name} · ${row.total} mistakes`}>
      {severities.map((s) => (
        <TooltipLine key={s} label={s} value={String(row[severityKeys[s]])} color={severityShades[s]} />
      ))}
      <TooltipLine label="Repeated" value={String(row.repeated)} />
      {row.pointsLost > 0 && <TooltipLine label="Points lost" value={round(row.pointsLost)} />}
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
