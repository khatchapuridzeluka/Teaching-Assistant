import { Link } from "react-router";
import { Activity, ArrowRight, BadgeCheck, ClipboardList, CloudDownload, ExternalLink, Inbox, PartyPopper, Pencil, Sparkles, Table2, TriangleAlert, Users } from "lucide-react";
import type { Course, ReviewDraft } from "../api/client";
import { useAssignments, useCourses, useJobOverview, useReviewQueue } from "../api/hooks";
import { Score, SeverityDots } from "../components/bits";
import { CourseTile } from "../components/Layout";
import { Avatar, buttonClass, Card, CardHeader, Chip, EmptyState, ErrorPanel, SkeletonCards, SkeletonRows, Stat, StatStrip } from "../components/ui";
import { formatAge, formatDay, greeting, initials } from "../format";
import { coursePath } from "../navigation";

const draftLink = (draft: { courseId: string; assignmentId: string; submissionId: string }) =>
  `/courses/${draft.courseId}/assignments/${draft.assignmentId}?student=${draft.submissionId}`;

export function HomePage() {
  const courses = useCourses();
  const queue = useReviewQueue();
  const jobs = useJobOverview(undefined, undefined);

  const drafts = queue.data?.drafts ?? [];
  const ungraded = queue.data?.ungraded ?? [];
  const counts = jobs.data?.counts;
  const active = counts ? counts.queued + counts.running : 0;
  const oldest = drafts[0];
  const today = new Date().toLocaleDateString(undefined, { weekday: "long", month: "long", day: "numeric" });
  const courseNames = new Map((courses.data ?? []).map((c) => [c.id, c.name]));

  return (
    <>
      <section className="flex flex-wrap items-end justify-between gap-x-8 gap-y-4 pb-1 pt-2">
        <div className="min-w-0">
          <div className="eyebrow">{today}</div>
          <h1 className="mt-1.5 max-w-[760px] font-display text-[30px] font-semibold leading-[1.15] tracking-[-0.015em] sm:text-[36px]">
            {greeting()}.{" "}
            {queue.isLoading ? (
              <span className="text-ink-muted">Checking your queue…</span>
            ) : drafts.length > 0 ? (
              <>
                {drafts.length} {drafts.length === 1 ? "draft" : "drafts"}{" "}
                {drafts.length === 1 ? "is" : "are"} waiting for your review.
              </>
            ) : ungraded.length > 0 ? (
              <>
                {ungraded.length} turned-in {ungraded.length === 1 ? "submission has" : "submissions have"} no draft yet.
              </>
            ) : (
              <>You are all caught up.</>
            )}
          </h1>
          <p className="mt-2 max-w-[620px] text-[14px] text-ink-muted">
            The agent drafts scores and feedback; you check, edit and approve. Nothing is ever sent to Classroom.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {oldest && (
            <Link to={draftLink(oldest)} className={buttonClass("primary", false, "h-10 px-4 text-[14px]")}>
              Start reviewing <ArrowRight className="h-4 w-4" />
            </Link>
          )}
          <Link to="/review" className={buttonClass("outline", false, "h-10 px-4 text-[14px]")}>
            <Inbox className="h-4 w-4" /> Review queue
          </Link>
        </div>
      </section>

      <StatStrip>
        <Stat
          label="Drafts to review"
          value={queue.data ? drafts.length : "–"}
          note={oldest ? `oldest has waited ${formatAge(oldest.createdAt)}` : "nothing waiting"}
          tone={drafts.length > 0 ? "mark" : undefined}
          icon={<Sparkles />}
        />
        <Stat label="Not graded yet" value={queue.data ? ungraded.length : "–"} note="turned in, no AI draft" icon={<Inbox />} />
        <Stat
          label="Running now"
          value={counts ? active : "–"}
          note={active > 0 ? `${counts!.running} running · ${counts!.queued} queued` : "no background work"}
          icon={<Activity />}
        />
        <Stat
          label="Failed · 24 h"
          value={counts ? counts.failed : "–"}
          note={counts && counts.failed > 0 ? "open System to retry" : `${counts?.succeeded ?? 0} jobs succeeded`}
          tone={counts && counts.failed > 0 ? "bad" : undefined}
          icon={<TriangleAlert />}
        />
      </StatStrip>

      <div className="grid items-start gap-4 xl:grid-cols-[minmax(0,1fr)_minmax(0,440px)]">
        <section className="min-w-0">
          <div className="mb-2.5 flex items-baseline justify-between gap-3">
            <h2 className="font-display text-[19px] font-semibold">Your courses</h2>
            <span className="text-xs text-ink-muted">from Google Classroom · read-only</span>
          </div>
          {courses.isLoading && <SkeletonCards count={2} />}
          {courses.isError && <ErrorPanel error={courses.error} />}
          {courses.data?.length === 0 && (
            <Card>
              <EmptyState icon={<ClipboardList />}>No courses found where you are a teacher.</EmptyState>
            </Card>
          )}
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            {courses.data?.map((course) => (
              <CourseCard key={course.id} course={course} waiting={drafts.filter((d) => d.courseId === course.id).length} />
            ))}
          </div>

          <Pipeline />
        </section>

        <Card className="overflow-hidden">
          <CardHeader
            icon={<Inbox />}
            title="Next in your queue"
            subtitle="Oldest drafts first"
            actions={
              drafts.length > 0 && (
                <Link to="/review" className="text-xs font-semibold text-accent hover:underline">
                  All {drafts.length} →
                </Link>
              )
            }
          />
          {queue.isLoading && <SkeletonRows rows={3} avatar />}
          {queue.isError && (
            <div className="p-3">
              <ErrorPanel error={queue.error} />
            </div>
          )}
          {queue.data && drafts.length === 0 && (
            <EmptyState icon={<PartyPopper />}>
              <div className="font-semibold text-ink">Nothing to review</div>
              Every AI grading you start lands here until you decide on it.
            </EmptyState>
          )}
          <ul className="divide-y divide-paper-dark">
            {drafts.slice(0, 7).map((draft) => (
              <QueueItem key={draft.gradingId} draft={draft} courseName={courseNames.get(draft.courseId)} />
            ))}
          </ul>
        </Card>
      </div>
    </>
  );
}

function QueueItem({ draft, courseName }: { draft: ReviewDraft; courseName: string | undefined }) {
  return (
    <li>
      <Link to={draftLink(draft)} className="group flex items-center gap-3 px-[18px] py-2.5 transition-colors hover:bg-paper">
        <Avatar text={initials(draft.studentName)} userId={draft.studentId} />
        <span className="min-w-0 flex-1">
          <span className="block truncate font-semibold">{draft.studentName}</span>
          <span className="block truncate text-[11px] text-ink-muted">
            {draft.assignmentTitle ?? "Assignment"} · {courseName ?? "Course"}
          </span>
        </span>
        <span className="flex shrink-0 flex-col items-end gap-0.5">
          <Score score={draft.score} max={draft.maxScore} bonus={draft.bonus} />
          <SeverityDots major={draft.major} minor={draft.minor} style={draft.style} empty="no mistakes" />
        </span>
        <ArrowRight className="h-4 w-4 shrink-0 text-ink-faint transition-transform group-hover:translate-x-0.5 group-hover:text-accent" />
      </Link>
    </li>
  );
}

function CourseCard({ course, waiting }: { course: Course; waiting: number }) {
  const assignments = useAssignments(course.id);
  const now = Date.now();
  const nextDue = assignments.data
    ?.filter((a) => a.dueAt && new Date(a.dueAt).getTime() > now)
    .sort((a, b) => new Date(a.dueAt!).getTime() - new Date(b.dueAt!).getTime())[0];
  const latest = assignments.data?.[0];

  return (
    <Card className="group flex flex-col overflow-hidden transition-all duration-200 hover:-translate-y-0.5 hover:border-ink-faint hover:shadow-raised">
      <Link to={coursePath(course.id)} className="flex items-center gap-3.5 px-[18px] pb-3 pt-4">
        <CourseTile courseId={course.id} name={course.name} size="lg" />
        <span className="min-w-0 flex-1">
          <span className="block truncate font-display text-[20px] font-semibold leading-tight">{course.name}</span>
          <span className="mt-0.5 flex items-center gap-2 text-xs text-ink-muted">
            {course.section ?? "Google Classroom"}
            <Chip tone={course.state === "ACTIVE" ? "success" : "muted"}>{course.state.toLowerCase()}</Chip>
          </span>
        </span>
        <ArrowRight className="h-4 w-4 shrink-0 text-ink-faint transition-transform group-hover:translate-x-0.5 group-hover:text-accent" />
      </Link>

      <dl className="grid grid-cols-3 gap-px border-y border-line bg-line">
        <Mini label="Assignments" value={assignments.data ? assignments.data.length : "–"} />
        <Mini label="To review" value={waiting} highlight={waiting > 0} />
        <Mini label="Next due" value={nextDue ? formatDay(nextDue.dueAt) : "–"} small />
      </dl>

      <div className="flex flex-wrap items-center gap-1 px-3 py-2">
        {latest && (
          <Link to={`${coursePath(course.id, "assignments")}/${latest.id}`} className={buttonClass("ghost", true)} title="The newest assignment">
            <ClipboardList className="h-3.5 w-3.5" /> {latest.title}
          </Link>
        )}
        <Link to={coursePath(course.id, "students")} className={buttonClass("ghost", true)}>
          <Users className="h-3.5 w-3.5" /> Students
        </Link>
        <Link to={coursePath(course.id, "gradebook")} className={buttonClass("ghost", true)}>
          <Table2 className="h-3.5 w-3.5" /> Gradebook
        </Link>
        {course.link && (
          <a href={course.link} target="_blank" rel="noreferrer" className="ml-auto flex items-center gap-1 px-1.5 text-[11px] text-ink-muted hover:text-ink">
            Classroom <ExternalLink className="h-3 w-3" />
          </a>
        )}
      </div>
    </Card>
  );
}

function Mini({ label, value, highlight, small }: { label: string; value: string | number; highlight?: boolean; small?: boolean }) {
  return (
    <div className="bg-white px-[18px] py-2.5">
      <dt className="eyebrow">{label}</dt>
      <dd className={`num mt-0.5 ${small ? "text-[15px] leading-[26px] tracking-normal" : "text-[20px]"}`}>
        {highlight ? <span className="text-accent">{value}</span> : value}
      </dd>
    </div>
  );
}

const steps = [
  { icon: CloudDownload, title: "Fetch", text: "Submissions are copied from Classroom, read-only, and stored as versions." },
  { icon: Sparkles, title: "AI drafts", text: "The agent grades against the instructions and marks mistakes on the code." },
  { icon: Pencil, title: "You review", text: "Correct the score, the feedback and every mistake; reject what is wrong." },
  { icon: BadgeCheck, title: "Approve", text: "Your report is final; its mistakes and strengths build the student's profile." },
];

function Pipeline() {
  return (
    <div className="mt-4 rounded-[10px] border border-dashed border-ink-faint/50 bg-white/50 px-[18px] py-3.5">
      <div className="eyebrow mb-3">How a grade is made</div>
      <ol className="grid gap-x-3 gap-y-4 sm:grid-cols-2 2xl:grid-cols-4">
        {steps.map((step, i) => (
          <li key={step.title} className="relative flex gap-3">
            <span
              className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-full ${
                i === steps.length - 1 ? "bg-success-tint text-success-ink" : i === 2 ? "bg-accent text-white" : "bg-accent-tint text-accent-strong"
              }`}
            >
              <step.icon className="h-4 w-4" />
            </span>
            <span className="min-w-0">
              <span className="flex items-center gap-1.5 font-semibold">
                <span className="font-mono text-[10.5px] text-ink-faint">{i + 1}</span>
                {step.title}
              </span>
              <span className="block text-xs leading-snug text-ink-muted">{step.text}</span>
            </span>
          </li>
        ))}
      </ol>
    </div>
  );
}
