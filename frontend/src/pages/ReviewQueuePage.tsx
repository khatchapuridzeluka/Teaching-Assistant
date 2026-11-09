import type { ReactNode } from "react";
import { Link } from "react-router";
import type { ReviewDraft, UngradedSubmission } from "../api/client";
import { useCourses, useReviewQueue } from "../api/hooks";
import { Avatar, Card, CardHeader, Chip, EmptyState, ErrorPanel, PageHeader, Spinner } from "../components/ui";
import { formatAge, formatDate, initials } from "../format";

export function ReviewQueuePage() {
  const queue = useReviewQueue();
  const courses = useCourses();
  const courseNames = new Map((courses.data ?? []).map((c) => [c.id, c.name]));

  const drafts = queue.data?.drafts ?? [];
  const ungraded = queue.data?.ungraded ?? [];
  const outdated = drafts.filter((d) => !d.isLatestVersion).length;
  const oldest = [...drafts.map((d) => d.createdAt), ...ungraded.map((u) => u.fetchedAt)].sort()[0];

  const where = (item: { courseId: string; assignmentTitle?: string | null }) =>
    `${item.assignmentTitle ?? "Assignment"} · ${courseNames.get(item.courseId) ?? "Course"}`;

  return (
    <>
      <PageHeader
        title="Review queue"
        subtitle="Across all courses · AI drafts waiting for your decision, then turned-in work that has no draft yet"
      />

      {queue.isError && <ErrorPanel error={queue.error} />}
      {queue.isLoading && <Spinner />}

      {queue.data && (
        <>
          <div className="grid grid-cols-2 border-t border-line md:grid-cols-4">
            <Stat label="Drafts" value={drafts.length} note="waiting for review" />
            <Stat label="Older version" value={outdated} note="student resubmitted since" />
            <Stat label="Not graded" value={ungraded.length} note="turned in, no draft yet" />
            <Stat label="Oldest waiting" value={oldest ? formatAge(oldest) : "–"} note="since drafted or fetched" />
          </div>

          <Card>
            <CardHeader title="Drafts waiting for review" subtitle="Oldest first · open one to check the AI review on the submission" />
            {drafts.length === 0 ? (
              <EmptyState>No drafts waiting. Every AI grading you start lands here until you decide on it.</EmptyState>
            ) : (
              <ul className="@container divide-y divide-line">
                {drafts.map((draft) => (
                  <QueueRow
                    key={draft.gradingId}
                    item={draft}
                    where={where(draft)}
                    chips={<DraftChips draft={draft} />}
                    when={`drafted ${formatAge(draft.createdAt)} ago`}
                    whenTitle={formatDate(draft.createdAt)}
                    action="Review"
                  />
                ))}
              </ul>
            )}
          </Card>

          <Card>
            <CardHeader title="Not graded yet" subtitle="Turned in, and the latest version has no AI draft · oldest fetch first" />
            {ungraded.length === 0 ? (
              <EmptyState>Nothing waiting. Fetch an assignment to pick up newly turned-in work.</EmptyState>
            ) : (
              <ul className="@container divide-y divide-line">
                {ungraded.map((item) => (
                  <QueueRow
                    key={item.versionId}
                    item={item}
                    where={where(item)}
                    chips={<UngradedChips item={item} />}
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
  where,
  chips,
  when,
  whenTitle,
  action,
}: {
  item: QueueItem;
  where: string;
  chips: ReactNode;
  when: string;
  whenTitle: string;
  action: string;
}) {
  return (
    <li>
      <Link
        to={`/courses/${item.courseId}/assignments/${item.assignmentId}?student=${item.submissionId}`}
        className="grid grid-flow-row-dense grid-cols-[minmax(0,1fr)_auto] items-center gap-x-4 gap-y-2 px-[18px] py-3 hover:bg-paper @3xl:grid-cols-[minmax(0,1fr)_auto_120px_auto]"
      >
        <div className="flex min-w-0 items-center gap-3">
          <Avatar text={initials(item.studentName)} userId={item.studentId} />
          <div className="min-w-0">
            <div className="truncate font-semibold">{item.studentName}</div>
            <div className="truncate text-xs text-ink-muted">{where}</div>
          </div>
        </div>
        <div className="col-span-2 flex flex-wrap items-center gap-x-3 gap-y-1.5 @3xl:contents">
          <div className="flex flex-wrap items-center gap-1.5 @3xl:justify-end">{chips}</div>
          <span className="whitespace-nowrap text-xs text-ink-muted" title={whenTitle}>
            {when}
          </span>
        </div>
        <span className="whitespace-nowrap text-right text-xs font-semibold text-accent">{action} →</span>
      </Link>
    </li>
  );
}

function DraftChips({ draft }: { draft: ReviewDraft }) {
  const score =
    draft.score == null ? "No score" : `${draft.score}${draft.maxScore ? ` / ${draft.maxScore}` : ""}${draft.bonus ? ` +${draft.bonus}` : ""}`;
  const mistakes = [
    draft.major > 0 ? `${draft.major} major` : null,
    draft.minor > 0 ? `${draft.minor} minor` : null,
    draft.style > 0 ? `${draft.style} style` : null,
  ].filter(Boolean);

  return (
    <>
      {draft.reportStatus === "InReview" && <Chip tone="ink">Report in review</Chip>}
      <Chip tone="accent">{score}</Chip>
      <Chip tone="muted">{mistakes.length > 0 ? mistakes.join(" · ") : "No mistakes"}</Chip>
      {draft.isLatestVersion ? (
        <Chip tone="outline">v{draft.versionNumber}</Chip>
      ) : (
        <Chip tone="dashed">v{draft.versionNumber} · newer version exists</Chip>
      )}
    </>
  );
}

function UngradedChips({ item }: { item: UngradedSubmission }) {
  return (
    <>
      <Chip tone="tint">Turned in</Chip>
      {item.late && <Chip tone="ink">Late</Chip>}
      <Chip tone="outline">v{item.versionNumber}</Chip>
    </>
  );
}

function Stat({ label, value, note }: { label: string; value: ReactNode; note: string }) {
  return (
    <div className="border-b border-line px-4 py-3 md:border-b-0 md:border-r md:last:border-r-0">
      <div className="text-xs text-ink-muted">{label}</div>
      <div className="text-[22px] font-bold leading-tight">{value}</div>
      <div className="text-[11px] text-ink-muted">{note}</div>
    </div>
  );
}
