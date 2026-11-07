import { Link, useParams } from "react-router";
import { useAssignments } from "../api/hooks";
import { CourseName } from "../components/Layout";
import { Card, EmptyState, ErrorPanel, PageHeader, Spinner } from "../components/ui";
import { formatDate } from "../format";

// GET /courses/{courseId}/assignments: the course's Assignments section.
export function AssignmentsPage() {
  const { courseId = "" } = useParams();
  const assignments = useAssignments(courseId);

  return (
    <>
      <PageHeader
        title="Assignments"
        subtitle={
          <>
            <CourseName courseId={courseId} /> · published assignments, newest first. Open one to see and fetch its submissions.
          </>
        }
      />

      {assignments.isLoading && <Spinner />}
      {assignments.isError && <ErrorPanel error={assignments.error} />}

      {assignments.data && (
        <Card>
          {assignments.data.length === 0 ? (
            <EmptyState>This course has no published assignments.</EmptyState>
          ) : (
            // Scrolls sideways on a phone rather than squeezing the columns; the long statement column hides there.
            <div className="overflow-x-auto">
            <table className="w-full border-collapse">
              <thead>
                <tr className="border-b border-line text-left text-[11px] uppercase tracking-[0.06em] text-ink-muted">
                  <th className="px-[18px] py-2.5 font-semibold">Assignment</th>
                  <th className="px-[18px] py-2.5 font-semibold">Due</th>
                  <th className="px-[18px] py-2.5 font-semibold">Points</th>
                  <th className="hidden px-[18px] py-2.5 font-semibold md:table-cell">Task statement</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {assignments.data.map((assignment) => (
                  <tr key={assignment.id} className="border-b border-paper-dark last:border-b-0 hover:bg-paper/60">
                    <td className="px-[18px] py-2.5 font-bold">
                      <Link to={`/courses/${courseId}/assignments/${assignment.id}`} className="hover:text-accent">
                        {assignment.title}
                      </Link>
                    </td>
                    <td className="whitespace-nowrap px-[18px] py-2.5 text-ink-muted">{formatDate(assignment.dueAt)}</td>
                    <td className="px-[18px] py-2.5 font-semibold tabular-nums">{assignment.maxPoints ?? "–"}</td>
                    <td className="hidden max-w-[420px] px-[18px] py-2.5 text-ink-muted md:table-cell">
                      <span className="line-clamp-1">{assignment.description || "No description (task may be in an attached file)"}</span>
                    </td>
                    <td className="px-[18px] py-2.5 text-right">
                      <Link
                        to={`/courses/${courseId}/assignments/${assignment.id}`}
                        className="inline-flex h-7 items-center rounded-lg border border-line px-2.5 text-xs font-semibold hover:bg-paper"
                      >
                        Open
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            </div>
          )}
        </Card>
      )}
    </>
  );
}
