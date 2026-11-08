import type { ReactNode } from "react";
import { Link, useParams } from "react-router";
import { useAssignments, useCourses } from "../api/hooks";
import { CourseName } from "../components/Layout";
import { Button, Card, CardHeader, Chip, EmptyState, ErrorPanel, PageHeader, Spinner } from "../components/ui";
import { formatDate } from "../format";
import { coursePath, courseSections } from "../navigation";

export function CourseOverviewPage() {
  const { courseId = "" } = useParams();
  const course = useCourses().data?.find((c) => c.id === courseId);
  const assignments = useAssignments(courseId);

  const now = Date.now();
  const nextDue = assignments.data
    ?.filter((a) => a.dueAt && new Date(a.dueAt).getTime() > now)
    .sort((a, b) => new Date(a.dueAt!).getTime() - new Date(b.dueAt!).getTime())[0];

  return (
    <>
      <PageHeader
        title={<CourseName courseId={courseId} />}
        subtitle={course && `${course.section ?? "No section"} · ${course.state.toLowerCase()} in Google Classroom`}
        actions={
          course?.link && (
            <a href={course.link} target="_blank" rel="noreferrer">
              <Button>Open in Classroom ↗</Button>
            </a>
          )
        }
      />

      {assignments.isError && <ErrorPanel error={assignments.error} />}

      <Card>
        <div className="grid grid-cols-2 gap-px overflow-hidden rounded-[10px] bg-line md:grid-cols-4">
          <Tile label="Assignments" value={assignments.data?.length ?? "–"} note="published in Classroom" />
          <Tile
            label="Next deadline"
            value={nextDue ? formatDate(nextDue.dueAt) : "None"}
            note={nextDue ? nextDue.title : "no upcoming due dates"}
            small
          />
          <Tile label="Drafts to review" value="–" note="soon: Review queue" muted />
          <Tile label="Students" value="–" note="soon: Students" muted />
        </div>
      </Card>

      <Card>
        <CardHeader
          title="Recent assignments"
          actions={
            <Link to={coursePath(courseId, "assignments")} className="text-xs font-semibold text-accent">
              All assignments →
            </Link>
          }
        />
        {assignments.isLoading && (
          <div className="px-[18px] py-3">
            <Spinner />
          </div>
        )}
        {assignments.data?.length === 0 && <EmptyState>This course has no published assignments.</EmptyState>}
        <ul>
          {assignments.data?.slice(0, 4).map((assignment) => (
            <li key={assignment.id} className="border-b border-paper-dark last:border-b-0">
              <Link
                to={`${coursePath(courseId, "assignments")}/${assignment.id}`}
                className="flex items-center justify-between gap-4 px-[18px] py-2.5 hover:bg-paper/60"
              >
                <span className="truncate font-semibold">{assignment.title}</span>
                <span className="shrink-0 text-xs text-ink-muted">
                  Due {formatDate(assignment.dueAt)}
                  <span className="hidden sm:inline"> · {assignment.maxPoints ?? "–"} points</span>
                </span>
              </Link>
            </li>
          ))}
        </ul>
      </Card>

      <div>
        <h2 className="mb-2.5 text-[11px] font-bold uppercase tracking-[0.06em] text-accent">In this course</h2>
        <div className="grid grid-cols-1 gap-[14px] md:grid-cols-2 xl:grid-cols-3">
          {courseSections
            .filter((section) => section.path !== "")
            .map((section) => (
              <Link key={section.path} to={coursePath(courseId, section.path)}>
                <Card className="flex h-full flex-col gap-1.5 p-[16px] transition-colors hover:border-accent">
                  <div className="flex items-center justify-between gap-3">
                    <b className="text-sm">{section.label}</b>
                    <Chip tone={section.ready ? "tint" : "dashed"}>{section.ready ? "Ready" : "Soon"}</Chip>
                  </div>
                  <p className="text-xs text-ink-muted">{section.description}</p>
                </Card>
              </Link>
            ))}
        </div>
      </div>
    </>
  );
}

function Tile({ label, value, note, small, muted }: { label: string; value: ReactNode; note: string; small?: boolean; muted?: boolean }) {
  return (
    <div className="min-w-0 bg-white px-[18px] py-3.5">
      <div className="text-xs text-ink-muted">{label}</div>
      <div className={`truncate font-bold leading-tight tracking-tight ${small ? "py-1.5 text-[17px]" : "text-[28px]"} ${muted ? "text-ink-muted" : ""}`}>
        {value}
      </div>
      <div className={`truncate text-xs font-semibold ${muted ? "text-ink-muted" : "text-accent"}`}>{note}</div>
    </div>
  );
}
