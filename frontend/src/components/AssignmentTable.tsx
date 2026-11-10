import { Link } from "react-router";
import { ChevronRight, ClipboardList, Sparkles } from "lucide-react";
import type { Assignment, AssignmentGrading, GradebookColumn } from "../api/client";
import { coursePath } from "../navigation";
import { DueChip, ProgressSplit } from "./bits";
import { Chip } from "./ui";

// The course's assignments as rows: deadline, what Classroom knows (graded / waiting / missing) and the drafts waiting here.
export function AssignmentTable({
  courseId,
  assignments,
  columns,
  drafts,
  students,
  showDescription,
}: {
  courseId: string;
  assignments: Assignment[];
  columns: GradebookColumn[] | undefined;
  drafts: AssignmentGrading[] | undefined;
  students: number | undefined;
  showDescription?: boolean;
}) {
  const byId = new Map((columns ?? []).map((c) => [c.id, c]));

  return (
    <ul className="@container divide-y divide-paper-dark">
      {assignments.map((assignment) => {
        const column = byId.get(assignment.id);
        const waiting = (drafts ?? []).filter((d) => d.assignmentId === assignment.id && d.reportStatus !== "Approved").length;
        const approved = (drafts ?? []).filter((d) => d.assignmentId === assignment.id && d.reportStatus === "Approved").length;
        return (
          <li key={assignment.id}>
            <Link
              to={`${coursePath(courseId, "assignments")}/${assignment.id}`}
              className="group grid grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-x-4 gap-y-2 px-[18px] py-3 transition-colors hover:bg-paper @3xl:grid-cols-[auto_minmax(0,1.3fr)_minmax(0,190px)_170px_minmax(0,150px)_auto]"
            >
              <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-accent-tint text-accent transition-transform group-hover:scale-105">
                <ClipboardList className="h-[18px] w-[18px]" />
              </span>

              <span className="min-w-0">
                <span className="block truncate text-[14px] font-semibold group-hover:text-accent-strong">{assignment.title}</span>
                <span className="block truncate text-[11px] text-ink-muted">
                  <span className="font-mono">{assignment.maxPoints ?? "–"} pts</span>
                  {showDescription && <> · {assignment.description || "No task statement; the task is in an attached file"}</>}
                </span>
              </span>

              <span className="col-start-2 row-start-2 flex @3xl:col-start-auto @3xl:row-start-auto">
                <DueChip dueAt={assignment.dueAt} />
              </span>

              <span className="col-span-3 flex min-w-0 flex-col gap-1 @3xl:col-span-1">
                {column ? (
                  <>
                    <ProgressSplit graded={column.graded} waiting={column.awaitingGrade} missing={column.missing} total={students ?? 0} />
                    <span className="truncate text-[11px] text-ink-muted">
                      <b className="font-semibold text-success-ink">{column.graded}</b> graded
                      {column.awaitingGrade > 0 && (
                        <>
                          {" · "}
                          <b className="font-semibold text-accent-strong">{column.awaitingGrade}</b> waiting
                        </>
                      )}
                      {column.missing > 0 && (
                        <>
                          {" · "}
                          <b className="font-semibold text-danger-ink">{column.missing}</b> missing
                        </>
                      )}
                    </span>
                  </>
                ) : (
                  <span className="text-[11px] text-ink-faint">Sync the course to see grades</span>
                )}
              </span>

              <span className="col-span-3 flex flex-wrap items-center gap-1.5 @3xl:col-span-1 @3xl:justify-end">
                {column?.averagePercent != null && (
                  <span className="font-mono text-[12px] text-ink-muted" title="Class average of the grades returned in Classroom">
                    avg <b className="font-semibold text-ink">{Math.round(column.averagePercent)}%</b>
                  </span>
                )}
                {waiting > 0 && (
                  <Chip tone="warn" icon={<Sparkles />}>
                    {waiting} to review
                  </Chip>
                )}
                {waiting === 0 && approved > 0 && <Chip tone="success">{approved} approved</Chip>}
              </span>

              <ChevronRight className="col-start-3 row-start-1 h-4 w-4 text-ink-faint transition-transform group-hover:translate-x-0.5 group-hover:text-accent @3xl:col-start-auto @3xl:row-start-auto" />
            </Link>
          </li>
        );
      })}
    </ul>
  );
}
