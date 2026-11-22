import { Link, useParams } from "react-router";
import { ArrowRight, CalendarClock, ClipboardList, ExternalLink, GraduationCap, PartyPopper, Sparkles, Users } from "lucide-react";
import { useAssignments, useCourseGradings, useCourses, useGradebook } from "../api/hooks";
import { AssignmentTable } from "../components/AssignmentTable";
import { Score, SeverityDots } from "../components/bits";
import { CourseName, CourseTile } from "../components/Layout";
import { Avatar, Button, Card, CardHeader, EmptyState, ErrorPanel, PageHeader, SkeletonRows, Stat, StatStrip } from "../components/ui";
import { formatDate, initials } from "../format";
import { coursePath } from "../navigation";

export function CourseOverviewPage() {
  const { courseId = "" } = useParams();
  const course = useCourses().data?.find((c) => c.id === courseId);
  const assignments = useAssignments(courseId);
  const gradebook = useGradebook(courseId);
  const gradings = useCourseGradings(courseId);

  const now = Date.now();
  const nextDue = assignments.data
    ?.filter((a) => a.dueAt && new Date(a.dueAt).getTime() > now)
    .sort((a, b) => new Date(a.dueAt!).getTime() - new Date(b.dueAt!).getTime())[0];
  const waiting = (gradings.data ?? []).filter((d) => d.reportStatus !== "Approved");
  const titles = new Map((assignments.data ?? []).map((a) => [a.id, a.title]));
  const book = gradebook.data;
  const synced = book?.syncedAt != null;

  return (
    <>
      <PageHeader
        lead={<CourseTile courseId={courseId} name={course?.name ?? "Course"} size="lg" />}
        title={<CourseName courseId={courseId} />}
        subtitle={course && `${course.section ?? "No section"} · ${course.state.toLowerCase()} in Google Classroom`}
        actions={
          course?.link && (
            <a href={course.link} target="_blank" rel="noreferrer">
              <Button icon={<ExternalLink />}>Open in Classroom</Button>
            </a>
          )
        }
      />

      {assignments.isError && <ErrorPanel error={assignments.error} />}

      <StatStrip>
        <Stat label="Assignments" value={assignments.data?.length ?? "–"} note="published in Classroom" icon={<ClipboardList />} />
        <Stat
          label="Next deadline"
          value={nextDue ? formatDate(nextDue.dueAt) : "None"}
          note={nextDue ? nextDue.title : "no upcoming due dates"}
          icon={<CalendarClock />}
        />
        <Stat
          label="Drafts to review"
          value={gradings.data ? waiting.length : "–"}
          note={waiting.length > 0 ? "AI drafts waiting for you" : "nothing waiting"}
          tone={waiting.length > 0 ? "mark" : undefined}
          icon={<Sparkles />}
        />
        <Stat
          label="Class average"
          value={book?.classAveragePercent == null ? "–" : `${Math.round(book.classAveragePercent)}%`}
          note={synced ? `${book!.students.length} students · missing work as 0` : "sync the course on Students"}
          icon={<GraduationCap />}
        />
      </StatStrip>

      <div className="grid items-start gap-4 xl:grid-cols-[minmax(0,1fr)_minmax(0,380px)]">
        <Card className="overflow-hidden">
          <CardHeader
            icon={<ClipboardList />}
            title="Assignments"
            subtitle="Newest first · graded, waiting and missing as Classroom has them"
            actions={
              <Link to={coursePath(courseId, "assignments")} className="text-xs font-semibold text-accent hover:underline">
                All assignments →
              </Link>
            }
          />
          {assignments.isLoading && <SkeletonRows rows={5} />}
          {assignments.data?.length === 0 && <EmptyState icon={<ClipboardList />}>This course has no published assignments.</EmptyState>}
          {assignments.data && (
            <AssignmentTable
              courseId={courseId}
              assignments={assignments.data.slice(0, 6)}
              columns={book?.assignments}
              drafts={gradings.data}
              students={book?.students.length}
            />
          )}
        </Card>

        <div className="flex min-w-0 flex-col gap-4">
          <Card className="overflow-hidden">
            <CardHeader
              icon={<Sparkles />}
              title="Waiting for your review"
              subtitle="AI drafts of this course"
              actions={
                waiting.length > 0 && (
                  <Link to={coursePath(courseId, "drafts")} className="text-xs font-semibold text-accent hover:underline">
                    All {waiting.length} →
                  </Link>
                )
              }
            />
            {gradings.isLoading && <SkeletonRows rows={3} avatar />}
            {gradings.data && waiting.length === 0 && (
              <EmptyState icon={<PartyPopper />}>
                <div className="font-semibold text-ink">Nothing to review</div>
                Open an assignment and grade with AI; drafts wait here for you.
              </EmptyState>
            )}
            <ul className="divide-y divide-paper-dark">
              {waiting.slice(0, 6).map((draft) => (
                <li key={draft.id}>
                  <Link
                    to={`${coursePath(courseId, "assignments")}/${draft.assignmentId}?student=${encodeURIComponent(draft.submissionId)}`}
                    className="group flex items-center gap-3 px-[18px] py-2.5 transition-colors hover:bg-paper"
                  >
                    <Avatar text={initials(draft.studentName)} userId={draft.studentId} />
                    <span className="min-w-0 flex-1">
                      <span className="block truncate font-semibold">{draft.studentName}</span>
                      <span className="block truncate text-[11px] text-ink-muted">{titles.get(draft.assignmentId) ?? "Assignment"}</span>
                    </span>
                    <span className="flex shrink-0 flex-col items-end gap-0.5">
                      <Score score={draft.score} max={draft.maxScore} bonus={draft.bonus} />
                      <SeverityDots major={draft.major} minor={draft.minor} style={draft.style} empty="no mistakes" />
                    </span>
                    <ArrowRight className="h-4 w-4 shrink-0 text-ink-faint transition-transform group-hover:translate-x-0.5 group-hover:text-accent" />
                  </Link>
                </li>
              ))}
            </ul>
          </Card>

          <Link
            to={coursePath(courseId, "students")}
            className="surface group flex items-center gap-3 px-[18px] py-3.5 transition-all hover:-translate-y-0.5 hover:border-ink-faint hover:shadow-raised"
          >
            <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-paper text-ink-muted">
              <Users className="h-[18px] w-[18px]" />
            </span>
            <span className="min-w-0 flex-1">
              <span className="block font-semibold">Students and profiles</span>
              <span className="block truncate text-xs text-ink-muted">Grades per assignment, mistake history and strengths for every student</span>
            </span>
            <ArrowRight className="h-4 w-4 shrink-0 text-ink-faint transition-transform group-hover:translate-x-0.5 group-hover:text-accent" />
          </Link>
        </div>
      </div>
    </>
  );
}
