import { useParams } from "react-router";
import { ClipboardList } from "lucide-react";
import { useAssignments, useCourseGradings, useGradebook } from "../api/hooks";
import { AssignmentTable } from "../components/AssignmentTable";
import { CourseName } from "../components/Layout";
import { Card, EmptyState, ErrorPanel, PageHeader, SkeletonList } from "../components/ui";

export function AssignmentsPage() {
  const { courseId = "" } = useParams();
  const assignments = useAssignments(courseId);
  const gradebook = useGradebook(courseId);
  const gradings = useCourseGradings(courseId);

  return (
    <>
      <PageHeader
        title="Assignments"
        subtitle={
          <>
            <CourseName courseId={courseId} /> · published assignments, newest first. Open one to fetch, read and grade its submissions.
          </>
        }
      />

      {assignments.isLoading && <SkeletonList rows={6} header={false} />}
      {assignments.isError && <ErrorPanel error={assignments.error} />}

      {assignments.data && (
        <Card className="overflow-hidden">
          {assignments.data.length === 0 ? (
            <EmptyState icon={<ClipboardList />}>This course has no published assignments.</EmptyState>
          ) : (
            <AssignmentTable
              courseId={courseId}
              assignments={assignments.data}
              columns={gradebook.data?.assignments}
              drafts={gradings.data}
              students={gradebook.data?.students.length}
              showDescription
            />
          )}
        </Card>
      )}
    </>
  );
}
