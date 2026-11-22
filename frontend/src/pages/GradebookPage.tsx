import { type ReactNode, useState } from "react";
import { Link, useParams } from "react-router";
import type { GradebookColumn, StudentAssignmentResult } from "../api/client";
import { useGradebook, useSyncCourse } from "../api/hooks";
import { CourseName } from "../components/Layout";
import { Download, RefreshCw } from "lucide-react";
import { Avatar, Button, buttonClass, Card, EmptyState, ErrorPanel, PageHeader, SkeletonPage, Stat, StatStrip } from "../components/ui";
import { formatDate, formatDay, initials } from "../format";

type SortKey = "name" | "best" | "worst";

const sortLabels: Record<SortKey, string> = {
  name: "Name",
  best: "Overall, highest first",
  worst: "Overall, lowest first",
};

export function GradebookPage() {
  const { courseId = "" } = useParams();
  const gradebook = useGradebook(courseId);
  const sync = useSyncCourse(courseId);
  const [sort, setSort] = useState<SortKey>("name");

  const data = gradebook.data;
  const synced = data?.syncedAt != null;
  const students = [...(data?.students ?? [])].sort((a, b) => {
    if (sort === "name") {
      return a.name.localeCompare(b.name);
    }
    const left = a.overallPercent ?? -1;
    const right = b.overallPercent ?? -1;
    return sort === "best" ? right - left : left - right;
  });
  const missing = data?.assignments.reduce((sum, a) => sum + a.missing, 0) ?? 0;
  const waiting = data?.assignments.reduce((sum, a) => sum + a.awaitingGrade, 0) ?? 0;

  return (
    <>
      <PageHeader
        title="Gradebook"
        subtitle={
          <>
            <CourseName courseId={courseId} /> · {synced ? `synced ${formatDate(data!.syncedAt)}` : "not synced yet"} · grades you
            returned in Classroom
          </>
        }
        actions={
          <>
            <Button icon={<RefreshCw className={sync.isPending ? "animate-spin" : ""} />} onClick={() => sync.mutate()} disabled={sync.isPending}>
              {sync.isPending ? "Syncing…" : "Sync course"}
            </Button>
            {synced && (
              <a
                href={`/api/courses/${courseId}/gradebook/export`}
                download
                className={buttonClass("primary")}
              >
                <Download className="h-4 w-4" /> Export CSV
              </a>
            )}
          </>
        }
      />

      {gradebook.isError && <ErrorPanel error={gradebook.error} />}
      {sync.isError && <ErrorPanel error={sync.error} />}
      {gradebook.isLoading && <SkeletonPage stats={4} rows={8} table={{ columns: 7 }} />}

      {data && !synced && (
        <Card>
          <EmptyState>
            <div className="font-semibold text-ink">No grades yet</div>
            Sync the course to load its students, assignments and the grades you returned in Classroom.
          </EmptyState>
        </Card>
      )}

      {data && synced && (
        <>
          <StatStrip>
            <Stat label="Students" value={data.students.length} note="enrolled" />
            <Stat
              label="Class average"
              value={data.classAveragePercent == null ? "–" : `${Math.round(data.classAveragePercent)}%`}
              note="overall score, missing work as 0"
            />
            <Stat label="Waiting for a grade" value={waiting} note="turned in, not returned yet" />
            <Stat label="Missing" value={missing} note="past due, nothing turned in" />
          </StatStrip>

          <Card className="overflow-hidden">
            <div className="flex flex-wrap items-center justify-between gap-3 border-b border-line px-[18px] py-3">
              <Legend />
              <label className="flex items-center gap-2 text-xs text-ink-muted">
                Sort
                <select
                  value={sort}
                  onChange={(e) => setSort(e.target.value as SortKey)}
                  className="field"
                >
                  {(Object.keys(sortLabels) as SortKey[]).map((key) => (
                    <option key={key} value={key}>
                      {sortLabels[key]}
                    </option>
                  ))}
                </select>
              </label>
            </div>

            <div className="max-h-[calc(100dvh-300px)] min-h-[320px] overflow-auto">
              <table className="w-max min-w-full border-separate border-spacing-0 text-xs">
                <thead>
                  <tr>
                    <th className="sticky left-0 top-0 z-30 min-w-[200px] border-b border-r border-line bg-paper-dark px-4 py-2.5 text-left font-semibold">
                      Student
                    </th>
                    {data.assignments.map((assignment) => (
                      <th
                        key={assignment.id}
                        className="sticky top-0 z-20 w-[128px] min-w-[128px] border-b border-r border-line bg-paper-dark px-3 py-2.5 text-left align-top font-normal"
                      >
                        <Link
                          to={`/courses/${courseId}/assignments/${assignment.id}`}
                          className="line-clamp-2 font-semibold text-ink hover:underline"
                          title={assignment.title}
                        >
                          {assignment.title}
                        </Link>
                        <div className="text-[11px] text-ink-muted">
                          {assignment.maxPoints != null ? `/${assignment.maxPoints}` : "no points"}
                          {assignment.dueAt ? ` · due ${formatDay(assignment.dueAt)}` : ""}
                        </div>
                      </th>
                    ))}
                    <th className="sticky right-0 top-0 z-30 min-w-[96px] border-b border-l border-line bg-paper-dark px-3 py-2.5 text-right font-semibold">
                      Overall
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {students.map((student) => (
                    <tr key={student.id} className="group">
                      <td className="sticky left-0 z-10 border-b border-r border-line bg-white px-4 py-2 group-hover:bg-paper">
                        <div className="flex items-center gap-2.5">
                          <Avatar text={initials(student.name)} userId={student.externalUserId} />
                          <span className="truncate font-semibold">{student.name}</span>
                        </div>
                      </td>
                      {student.results.map((result, i) => (
                        <td key={result.assignmentId} className="border-b border-r border-line px-3 py-2 group-hover:bg-paper">
                          <Cell result={result} column={data.assignments[i]!} courseId={courseId} />
                        </td>
                      ))}
                      <td className="sticky right-0 z-10 border-b border-l border-line bg-white px-3 py-2 text-right group-hover:bg-paper">
                        <b className="font-mono text-[12.5px] font-semibold">{student.overallPercent == null ? "–" : `${Math.round(student.overallPercent)}%`}</b>
                        {student.possible > 0 && (
                          <div className="text-[11px] text-ink-muted">
                            {round(student.earned)}/{round(student.possible)}
                          </div>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr>
                    <td className="sticky bottom-0 left-0 z-30 border-r border-t border-line bg-paper-dark px-4 py-2.5 font-semibold">
                      Class
                    </td>
                    {data.assignments.map((assignment) => (
                      <td key={assignment.id} className="sticky bottom-0 z-20 border-r border-t border-line bg-paper-dark px-3 py-2.5">
                        <b className="font-mono text-[12.5px] font-semibold">{assignment.averagePercent == null ? "–" : `${Math.round(assignment.averagePercent)}%`}</b>
                        <div className="text-[11px] text-ink-muted">
                          {assignment.graded} graded
                          {assignment.awaitingGrade > 0 && ` · ${assignment.awaitingGrade} waiting`}
                          {assignment.missing > 0 && ` · ${assignment.missing} missing`}
                        </div>
                      </td>
                    ))}
                    <td className="sticky bottom-0 right-0 z-30 border-l border-t border-line bg-paper-dark px-3 py-2.5 text-right">
                      <b className="font-mono text-[12.5px] font-semibold">{data.classAveragePercent == null ? "–" : `${Math.round(data.classAveragePercent)}%`}</b>
                    </td>
                  </tr>
                </tfoot>
              </table>
            </div>
          </Card>
        </>
      )}
    </>
  );
}

function Cell({ result, column, courseId }: { result: StudentAssignmentResult; column: GradebookColumn; courseId: string }) {
  const content = cellContent(result);
  if (!result.submissionId) {
    return content;
  }

  return (
    <Link
      to={`/courses/${courseId}/assignments/${column.id}?student=${result.submissionId}`}
      className="block rounded-md hover:underline"
      title="Open this submission"
    >
      {content}
    </Link>
  );
}

function cellContent(result: StudentAssignmentResult): ReactNode {
  switch (result.gradeStatus) {
    case "Graded": {
      return (
        <span className="whitespace-nowrap">
          <b className="font-mono text-[12.5px] font-semibold">{round(result.grade!)}</b>
          {result.late && <span className="ml-1 text-[10px] font-semibold uppercase text-ink-muted">late</span>}
        </span>
      );
    }
    case "AwaitingGrade":
      return <span className="rounded-full bg-accent-tint px-2 py-0.5 text-[11px] font-semibold text-accent">turned in</span>;
    case "Missing":
      return <span className="text-[11px] font-semibold text-danger">missing · 0</span>;
    case "NotDue":
      return <span className="text-[11px] text-ink-muted">not due</span>;
    case "NotSubmitted":
      return <span className="text-ink-muted">—</span>;
  }
}

function Legend() {
  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-[11px] text-ink-muted">
      <span>
        <b className="text-ink">95</b> grade returned in Classroom
      </span>
      <span className="rounded-full bg-accent-tint px-2 py-0.5 font-semibold text-accent">turned in</span>
      <span>waiting for your grade</span>
      <span className="font-semibold text-danger">missing · 0</span>
      <span>past due</span>
    </div>
  );
}


function round(value: number) {
  return Math.round(value * 10) / 10;
}
