import type { ReactNode } from "react";
import { Link, useParams, useSearchParams } from "react-router";
import { Bar, BarChart, CartesianGrid, LabelList, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { MistakeCategory, StrengthLabel, StudentStrengthFilter, StudentStrengthInfo } from "../api/client";
import { useCourseStudents, useStudentStrengths } from "../api/hooks";
import { strengthColor } from "../components/CodeBrowser";
import { FilterChips } from "../components/FilterChips";
import { CourseName } from "../components/Layout";
import { StudentOverviewCard } from "../components/StudentOverview";
import { categoryLabels } from "../components/StudentMistakes";
import { StrengthRow, summarize } from "../components/StudentStrengths";
import { Avatar, Button, Card, CardHeader, EmptyState, ErrorPanel, PageHeader, Spinner, Stat, StatStrip } from "../components/ui";
import { formatDay, initials } from "../format";

const mistakeColor = "#9a3412";

const labelInfo: Record<StrengthLabel, { label: string; color: string }> = {
  New: { label: "New", color: "#a9c1f4" },
  Consistent: { label: "Consistent · also a strength earlier", color: "#16203a" },
  Improved: { label: "Improved · was a mistake earlier", color: strengthColor },
};

export function StudentStrengthsPage() {
  const { courseId = "", userId = "" } = useParams();
  const [searchParams, setSearchParams] = useSearchParams();
  const filter: StudentStrengthFilter = {
    assignmentId: searchParams.get("assignment") ?? undefined,
    category: (searchParams.get("category") as MistakeCategory | null) ?? undefined,
    label: (searchParams.get("label") as StrengthLabel | null) ?? undefined,
  };
  const filtered = Object.values(filter).some((v) => v !== undefined);
  const strengths = useStudentStrengths(courseId, userId, filter);
  const student = useCourseStudents(courseId).data?.students.find((s) => s.externalUserId === userId);
  const data = strengths.data;

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

  const count = (label: StrengthLabel) => data?.labels.find((l) => l.key === label)?.count ?? 0;
  const assignmentsWithStrengths = data?.assignments.filter((a) => a.strengths > 0).length ?? 0;

  return (
    <>
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            {student && <Avatar text={initials(student.name)} userId={student.externalUserId} />}
            <span>Strengths · {student?.name ?? "Student"}</span>
          </span>
        }
        subtitle={
          <>
            <CourseName courseId={courseId} /> · from approved reports · labels compare with assignments due earlier
          </>
        }
        actions={
          <span className="flex flex-wrap gap-2">
            <Link to={`/courses/${courseId}/students/${userId}/mistakes`}>
              <Button>Mistakes →</Button>
            </Link>
            <Link to={`/courses/${courseId}/students${student ? `?student=${student.id}` : ""}`}>
              <Button>← Profile</Button>
            </Link>
          </span>
        }
      />

      {strengths.isError && <ErrorPanel error={strengths.error} />}
      {strengths.isLoading && <Spinner />}

      {data && data.recorded === 0 && (
        <Card>
          <EmptyState>
            <div className="font-semibold text-ink">No recorded strengths yet</div>
            Strengths are recorded when you approve a report whose AI draft noted some (Edit draft → Approve). Drafts graded before
            strengths existed have none; grade again to get them.
          </EmptyState>
        </Card>
      )}

      {data && data.recorded > 0 && (
        <>
          <StudentOverviewCard
            courseId={courseId}
            userId={userId}
            studentName={student?.name ?? "this student"}
            returnTo={`/courses/${courseId}/students/${userId}/strengths`}
            kind="Strengths"
          />
          <Card className="flex flex-col gap-3 px-[18px] py-3">
            <div className="flex flex-wrap items-center gap-2">
              <span className="mr-1 eyebrow">Assignment</span>
              <select
                value={filter.assignmentId ?? ""}
                onChange={(e) => setFilter("assignment", e.target.value || undefined)}
                className="field"
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
              label="Label"
              options={[
                { value: undefined, label: "All" },
                { value: "New" as StrengthLabel, label: "New" },
                { value: "Consistent" as StrengthLabel, label: "Consistent" },
                { value: "Improved" as StrengthLabel, label: "Improved" },
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

          <Card>
            <CardHeader title="Overall" subtitle="Computed from the recorded strengths and mistakes shown" />
            <p className="px-[18px] py-3 text-[13px] leading-relaxed">{summarize(data)}</p>
          </Card>

          <StatStrip columns={5}>
            <Stat label="Strengths" value={data.total} note={filtered ? `of ${data.recorded} recorded` : "recorded"} />
            <Stat label="Assignments" value={assignmentsWithStrengths} note="with at least one strength" />
            <Stat label="Areas" value={data.categories.length} note="categories with a strength" />
            <Stat label="Consistent" value={count("Consistent")} note="also a strength earlier" />
            <Stat label="Improved" value={count("Improved")} note="was a mistake earlier" />
          </StatStrip>

          {data.total === 0 ? (
            <Card>
              <EmptyState>No strengths match these filters.</EmptyState>
            </Card>
          ) : (
            <>
              <div className="grid gap-4 xl:grid-cols-2">
                <Card>
                  <CardHeader title="By category" subtitle="Strengths shown · click a bar to filter" />
                  <div className="px-2 py-3" style={{ height: Math.max(140, data.categories.length * 38 + 30) }}>
                    <ResponsiveContainer width="100%" height="100%">
                      <BarChart
                        data={data.categories.map((c) => ({ ...c, name: categoryLabels[c.category] ?? c.category }))}
                        layout="vertical"
                        margin={{ top: 0, right: 48, bottom: 0, left: 8 }}
                      >
                        <XAxis type="number" hide allowDecimals={false} />
                        <YAxis type="category" dataKey="name" width={130} tick={{ fontSize: 12, fill: "#16203a" }} tickLine={false} axisLine={false} />
                        <Tooltip cursor={{ fill: "rgba(22,32,58,0.06)" }} content={<CategoryTooltip />} />
                        <Bar
                          dataKey="count"
                          fill={strengthColor}
                          radius={[0, 4, 4, 0]}
                          maxBarSize={18}
                          isAnimationActive={false}
                          cursor="pointer"
                          onClick={(item) => setFilter("category", (item.payload as { category?: MistakeCategory } | undefined)?.category)}
                        >
                          <LabelList dataKey="count" position="right" style={{ fontSize: 11, fill: "#5d6985" }} />
                        </Bar>
                      </BarChart>
                    </ResponsiveContainer>
                  </div>
                </Card>

                <Card>
                  <CardHeader title="Strengths vs. mistakes" subtitle="Per assignment, in due-date order · click a bar to filter" />
                  <div className="flex flex-wrap gap-x-4 px-[18px] pt-3 text-[11px] text-ink-muted">
                    <Legend color={strengthColor} label="Strengths" />
                    <Legend color={mistakeColor} label="Mistakes" />
                  </div>
                  <div className="h-[220px] px-2 pb-3 pt-1">
                    <ResponsiveContainer width="100%" height="100%">
                      <BarChart
                        data={data.assignments.map((a) => ({ ...a, name: a.title ?? "Assignment" }))}
                        margin={{ top: 8, right: 12, bottom: 0, left: 0 }}
                        barGap={2}
                      >
                        <CartesianGrid vertical={false} stroke="#e9ecf3" />
                        <XAxis dataKey="name" tick={{ fontSize: 11, fill: "#5d6985" }} tickLine={false} axisLine={{ stroke: "#dcdfea" }} />
                        <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: "#5d6985" }} tickLine={false} axisLine={false} width={28} />
                        <Tooltip cursor={{ fill: "rgba(22,32,58,0.06)" }} content={<AssignmentTooltip />} />
                        {(["strengths", "mistakes"] as const).map((key) => (
                          <Bar
                            key={key}
                            dataKey={key}
                            fill={key === "strengths" ? strengthColor : mistakeColor}
                            radius={[4, 4, 0, 0]}
                            maxBarSize={24}
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

              <Card>
                <CardHeader title="New, consistent or improved" subtitle="Share of the strengths shown · click to filter" />
                <ul className="flex flex-col gap-1 px-[18px] py-3">
                  {data.labels.map((l) => (
                    <li key={l.key}>
                      <button
                        onClick={() => setFilter("label", l.key)}
                        className="flex w-full items-center gap-2 rounded-md px-1 py-1 text-left text-xs hover:bg-paper"
                      >
                        <span className="h-2.5 w-2.5 shrink-0 rounded-[3px]" style={{ backgroundColor: labelInfo[l.key].color }} />
                        <span className="flex-1">{labelInfo[l.key].label}</span>
                        <b className="tabular-nums">{l.count}</b>
                        <span className="w-12 text-right tabular-nums text-ink-muted">{round(l.percent)}%</span>
                      </button>
                    </li>
                  ))}
                </ul>
              </Card>

              <Card>
                <CardHeader title="Strengths" subtitle={`${data.total} shown · newest assignment first`} />
                <StrengthList strengths={data.strengths} />
              </Card>
            </>
          )}
        </>
      )}
    </>
  );
}

function StrengthList({ strengths }: { strengths: StudentStrengthInfo[] }) {
  const groups: { id: string; title?: string | null; dueAt?: string | null; items: StudentStrengthInfo[] }[] = [];
  for (const strength of strengths) {
    const last = groups[groups.length - 1];
    if (last && last.id === strength.assignmentId) {
      last.items.push(strength);
    } else {
      groups.push({ id: strength.assignmentId, title: strength.assignmentTitle, dueAt: strength.assignmentDueAt, items: [strength] });
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
            {group.items.map((s) => (
              <StrengthRow key={s.id} strength={s} />
            ))}
          </ul>
        </div>
      ))}
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

type TooltipProps<T> = { active?: boolean; payload?: { payload?: T }[] };

function CategoryTooltip({
  active,
  payload,
}: TooltipProps<{ name: string; count: number; assignments: number; mistakes: number; improvedSinceAssignmentTitle?: string | null }>) {
  const row = active ? payload?.[0]?.payload : undefined;
  if (!row) {
    return null;
  }
  return (
    <TooltipBox title={row.name}>
      <TooltipLine label="Strengths" value={String(row.count)} />
      <TooltipLine label="Assignments" value={String(row.assignments)} />
      <TooltipLine label="Mistakes in this area" value={String(row.mistakes)} />
      {row.improvedSinceAssignmentTitle && <TooltipLine label="Improved since" value={row.improvedSinceAssignmentTitle} />}
    </TooltipBox>
  );
}

function AssignmentTooltip({ active, payload }: TooltipProps<{ name: string; strengths: number; mistakes: number }>) {
  const row = active ? payload?.[0]?.payload : undefined;
  if (!row) {
    return null;
  }
  return (
    <TooltipBox title={row.name}>
      <TooltipLine label="Strengths" value={String(row.strengths)} color={strengthColor} />
      <TooltipLine label="Mistakes" value={String(row.mistakes)} color={mistakeColor} />
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


function round(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(1);
}
