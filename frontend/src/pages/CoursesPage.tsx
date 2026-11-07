import { Link } from "react-router";
import { useCourses } from "../api/hooks";
import { Card, Chip, EmptyState, ErrorPanel, PageHeader, Spinner } from "../components/ui";

// GET /courses
export function CoursesPage() {
  const courses = useCourses();

  return (
    <>
      <PageHeader title="Courses" subtitle="Courses you teach in Google Classroom (read-only)." />

      {courses.isLoading && <Spinner />}
      {courses.isError && <ErrorPanel error={courses.error} />}
      {courses.data?.length === 0 && (
        <Card>
          <EmptyState>No courses found where you are a teacher.</EmptyState>
        </Card>
      )}

      <div className="grid grid-cols-1 gap-[18px] md:grid-cols-2 xl:grid-cols-3">
        {courses.data?.map((course) => (
          <Card key={course.id} className="flex flex-col p-[18px] transition-colors hover:border-accent">
            <div className="flex items-start justify-between gap-3">
              <Link to={`/courses/${course.id}`} className="min-w-0">
                <div className="truncate text-[15px] font-bold">{course.name}</div>
                <div className="text-xs text-ink-muted">{course.section ?? "No section"}</div>
              </Link>
              <Chip tone={course.state === "ACTIVE" ? "tint" : "muted"}>{course.state.toLowerCase()}</Chip>
            </div>
            <div className="mt-5 flex items-center justify-between">
              <Link to={`/courses/${course.id}`} className="font-semibold text-accent">
                Open course →
              </Link>
              {course.link && (
                <a href={course.link} target="_blank" rel="noreferrer" className="text-xs text-ink-muted hover:text-ink">
                  Classroom ↗
                </a>
              )}
            </div>
          </Card>
        ))}
      </div>
    </>
  );
}
