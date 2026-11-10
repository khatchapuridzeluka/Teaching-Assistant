import type { ReactNode } from "react";
import { Link, useSearchParams } from "react-router";
import { ArrowRight, Clock, GitCompareArrows, Inbox, PartyPopper, Sparkles } from "lucide-react";
import type { ReviewDraft, UngradedSubmission } from "../api/client";
import { useCourses, useReviewQueue } from "../api/hooks";
import { Score, SeverityDots } from "../components/bits";
import { CourseTile } from "../components/Layout";
import { Avatar, Card, Chip, EmptyState, ErrorPanel, PageHeader, Segmented, Spinner, Stat, StatStrip, Tabs } from "../components/ui";
import { formatAge, formatDate, initials } from "../format";

export function ReviewQueuePage() {
  const queue = useReviewQueue();
  const courses = useCourses();
  const [searchParams, setSearchParams] = useSearchParams();
  const tab = searchParams.get("tab") === "ungraded" ? "ungraded" : "drafts";
  const courseFilter = searchParams.get("course") ?? "all";
  const courseNames = new Map((courses.data ?? []).map((c) => [c.id, c.name]));

  const drafts = queue.data?.drafts ?? [];
  const ungraded = queue.data?.ungraded ?? [];
  const outdated = drafts.filter((d) => !d.isLatestVersion).length;
  const oldest = [...drafts.map((d) => d.createdAt), ...ungraded.map((u) => u.fetchedAt)].sort()[0];

  const involved = [...new Set([...drafts, ...ungraded].map((item) => item.courseId))];
  const inCourse = <T extends { courseId: string }>(items: T[]) => (courseFilter === "all" ? items : items.filter((item) => item.courseId === courseFilter));
  const shownDrafts = inCourse(drafts);
  const shownUngraded = inCourse(ungraded);

  function setParam(key: string, value: string | undefined) {
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

  return (
    <>
      <PageHeader title="Review queue" subtitle="Across all courses · AI drafts waiting for your decision, and turned-in work that has no draft yet" />

      {queue.isError && <ErrorPanel error={queue.error} />}
      {queue.isLoading && <Spinner />}

      {queue.data && (
        <>
          <StatStrip>
            <Stat label="Drafts" value={drafts.length} note="waiting for review" tone={drafts.length > 0 ? "mark" : undefined} icon={<Sparkles />} />
            <Stat label="Older version" value={outdated} note="student resubmitted since" icon={<GitCompareArrows />} />
            <Stat label="Not graded" value={ungraded.length} note="turned in, no draft yet" icon={<Inbox />} />
            <Stat label="Oldest waiting" value={oldest ? formatAge(oldest) : "–"} note="since drafted or fetched" icon={<Clock />} />
          </StatStrip>

          <Card className="overflow-hidden">
            <div className="flex flex-wrap items-end justify-between gap-x-4 gap-y-2 px-2 pt-1">
              <Tabs
                label="Queue"
                value={tab}
                onChange={(value) => setParam("tab", value === "drafts" ? undefined : value)}
                className="border-b-0"
                tabs={[
                  { value: "drafts", label: "Drafts to review", count: drafts.length, icon: <Sparkles /> },
                  { value: "ungraded", label: "Not graded yet", count: ungraded.length, icon: <Inbox /> },
                ]}
              />
              {involved.length > 1 && (
                <div className="pb-1.5 pr-2">
                  <Segmented
                    label="Course"
                    value={courseFilter}
                    onChange={(value) => setParam("course", value === "all" ? undefined : value)}
                    options={[{ value: "all", label: "All courses" }, ...involved.map((id) => ({ value: id, label: courseNames.get(id) ?? "Course" }))]}
                  />
                </div>
              )}
            </div>
            <div className="border-t border-line bg-paper/60 px-[18px] py-1.5 text-[11px] text-ink-muted">
              {tab === "drafts"
                ? "Oldest first · open one to check the AI review next to the code, then edit and approve"
                : "Turned in, and the latest version has no AI draft · oldest fetch first"}
            </div>

            {tab === "drafts" ? (
              shownDrafts.length === 0 ? (
                <EmptyState icon={<PartyPopper />}>
                  <div className="font-semibold text-ink">No drafts waiting</div>
                  Every AI grading you start lands here until you decide on it.
                </EmptyState>
              ) : (
                <ul className="@container divide-y divide-paper-dark">
                  {shownDrafts.map((draft) => (
                    <QueueRow
                      key={draft.gradingId}
                      item={draft}
                      assignment={draft.assignmentTitle}
                      courseName={courseNames.get(draft.courseId)}
                      detail={<DraftDetail draft={draft} />}
                      when={`drafted ${formatAge(draft.createdAt)} ago`}
                      whenTitle={formatDate(draft.createdAt)}
                      action="Review"
                    />
                  ))}
                </ul>
              )
            ) : shownUngraded.length === 0 ? (
              <EmptyState icon={<Inbox />}>
                <div className="font-semibold text-ink">Nothing waiting</div>
                Fetch an assignment to pick up newly turned-in work.
              </EmptyState>
            ) : (
              <ul className="@container divide-y divide-paper-dark">
                {shownUngraded.map((item) => (
                  <QueueRow
                    key={item.versionId}
                    item={item}
                    assignment={item.assignmentTitle}
                    courseName={courseNames.get(item.courseId)}
                    detail={<UngradedDetail item={item} />}
                    when={`fetched ${formatAge(item.fetchedAt)} ago`}
                    whenTitle={formatDate(item.fetchedAt)}
                    action="Open"
                  />
                ))}
              </ul>
            )}
          </Card>
        </>
      )}
    </>
  );
}

type QueueItem = { courseId: string; assignmentId: string; submissionId: string; studentId: string; studentName: string };

function QueueRow({
  item,
  assignment,
  courseName,
  detail,
  when,
  whenTitle,
  action,
}: {
  item: QueueItem;
  assignment: string | null | undefined;
  courseName: string | undefined;
  detail: ReactNode;
  when: string;
  whenTitle: string;
  action: string;
}) {
  return (
    <li>
      <Link
        to={`/courses/${item.courseId}/assignments/${item.assignmentId}?student=${item.submissionId}`}
        className="group grid grid-flow-row-dense grid-cols-[minmax(0,1fr)_auto] items-center gap-x-5 gap-y-2 px-[18px] py-2.5 transition-colors hover:bg-paper @3xl:grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)_auto_110px_auto]"
      >
        <div className="flex min-w-0 items-center gap-3">
          <Avatar text={initials(item.studentName)} userId={item.studentId} />
          <div className="min-w-0">
            <div className="truncate font-semibold">{item.studentName}</div>
            <div className="truncate text-[11px] text-ink-muted @3xl:hidden">
              {assignment ?? "Assignment"} · {courseName ?? "Course"}
            </div>
          </div>
        </div>
        <div className="hidden min-w-0 items-center gap-2 @3xl:flex">
          <CourseTile courseId={item.courseId} name={courseName ?? "Course"} size="sm" />
          <span className="min-w-0 truncate">
            <span className="font-medium">{assignment ?? "Assignment"}</span>
            <span className="text-ink-muted"> · {courseName ?? "Course"}</span>
          </span>
        </div>
        <div className="col-span-2 flex flex-wrap items-center gap-x-3 gap-y-1.5 @3xl:col-span-1 @3xl:justify-end">{detail}</div>
        <span className="hidden whitespace-nowrap text-right text-[11px] text-ink-muted @3xl:block" title={whenTitle}>
          {when}
        </span>
        <span className="col-start-2 row-start-1 flex items-center gap-1 whitespace-nowrap text-xs font-semibold text-accent @3xl:col-start-auto @3xl:row-start-auto">
          {action}
          <ArrowRight className="h-3.5 w-3.5 transition-transform group-hover:translate-x-0.5" />
        </span>
      </Link>
    </li>
  );
}

function DraftDetail({ draft }: { draft: ReviewDraft }) {
  return (
    <>
      {draft.reportStatus === "InReview" && <Chip tone="mark">Report in review</Chip>}
      {!draft.isLatestVersion && (
        <Chip tone="warn" icon={<GitCompareArrows />}>
          newer version exists
        </Chip>
      )}
      <SeverityDots major={draft.major} minor={draft.minor} style={draft.style} empty="no mistakes" />
      <span className="font-mono text-[11px] text-ink-faint">v{draft.versionNumber}</span>
      <span className="w-[86px] text-right">
        <Score score={draft.score} max={draft.maxScore} bonus={draft.bonus} />
      </span>
    </>
  );
}

function UngradedDetail({ item }: { item: UngradedSubmission }) {
  return (
    <>
      <Chip tone="info">Turned in</Chip>
      {item.late && <Chip tone="warn">Late</Chip>}
      <span className="font-mono text-[11px] text-ink-faint">v{item.versionNumber}</span>
    </>
  );
}
