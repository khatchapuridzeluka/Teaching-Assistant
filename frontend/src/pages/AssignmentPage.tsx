import { useState } from "react";
import { useParams } from "react-router";
import type { Attachment, ExtractionSummary, FetchSummary, StoredSubmission, StoredVersion, Submission } from "../api/client";
import {
  useAssignments,
  useFetchSubmissions,
  useGradeVersion,
  useReExtract,
  useReview,
  useStoredSubmissions,
  useSubmissions,
} from "../api/hooks";
import { GradingPanel } from "../components/GradingPanel";
import { InstructionsCard } from "../components/InstructionsCard";
import { PreviewPanel } from "../components/PreviewPanel";
import { Avatar, Button, Card, CardHeader, Chip, type ChipTone, EmptyState, ErrorPanel, PageHeader, Spinner } from "../components/ui";
import { formatDate, formatDay, initials } from "../format";

// GET …/submissions, GET …/submissions/stored and POST …/submissions/fetch
export function AssignmentPage() {
  const { courseId = "", assignmentId = "" } = useParams();
  const assignment = useAssignments(courseId).data?.find((a) => a.id === assignmentId);
  const submissions = useSubmissions(courseId, assignmentId);
  const stored = useStoredSubmissions(courseId, assignmentId);
  const fetch = useFetchSubmissions(courseId, assignmentId);
  const extract = useReExtract(courseId, assignmentId);
  const [previewing, setPreviewing] = useState<{ submission: StoredSubmission; versionId: string } | null>(null);
  const grade = useGradeVersion();
  const [reviewing, setReviewing] = useState<{ submission: StoredSubmission; version: StoredVersion } | null>(null);
  const [sort, setSort] = useState<Sort>({ key: "status", descending: false });
  const [filter, setFilter] = useState<Filter>("all");
  // The version picked per student (by stored submission ID); the newest one when nothing is picked.
  const [pickedVersions, setPickedVersions] = useState<Record<string, string>>({});
  // The version being graded right now, if any: one grading at a time.
  const gradingVersionId = grade.isPending ? grade.variables : undefined;

  // Opens the review panel for the picked version and, when asked, grades it. Grading starts from the click
  // (not from the panel), so it is sent exactly once.
  function openReview(submission: StoredSubmission, version: StoredVersion, gradeNow: boolean) {
    setReviewing({ submission, version });
    if (gradeNow) {
      grade.mutate(version.id);
    }
  }

  // Clicking the sorted column flips its direction; another column starts ascending.
  function sortBy(key: SortKey) {
    setSort((current) => ({ key, descending: current.key === key ? !current.descending : false }));
  }

  // Stored copies are matched to the live list by the Classroom submission ID.
  const storedById = new Map(stored.data?.map((s) => [s.externalSubmissionId, s]));

  const all = submissions.data ?? [];
  const counts: Record<Filter, number> = {
    all: all.length,
    returned: all.filter(filters.returned.matches).length,
    turnedIn: all.filter(filters.turnedIn.matches).length,
    missing: all.filter(filters.missing.matches).length,
  };
  const rows = sortSubmissions(all.filter(filters[filter].matches), sort);

  return (
    <>
      <PageHeader
        title={assignment?.title ?? "Assignment"}
        subtitle={
          assignment && (
            <>
              Due {formatDate(assignment.dueAt)} · {assignment.maxPoints ?? "–"} points
              {submissions.data && ` · ${counts.returned + counts.turnedIn} of ${all.length} turned in`}
            </>
          )
        }
        actions={
          <>
            {assignment?.link && (
              <a href={assignment.link} target="_blank" rel="noreferrer">
                <Button>Open in Classroom ↗</Button>
              </a>
            )}
            {(stored.data?.length ?? 0) > 0 && (
              <Button
                onClick={() => extract.mutate()}
                disabled={extract.isPending || fetch.isPending}
                title="Rebuild the extracted code from the stored files; nothing is downloaded"
              >
                {extract.isPending ? "Extracting…" : "Re-extract"}
              </Button>
            )}
            <Button variant="primary" onClick={() => fetch.mutate()} disabled={fetch.isPending}>
              {fetch.isPending ? "Fetching…" : "Fetch from Classroom"}
            </Button>
          </>
        }
      />

      {assignment && <InstructionsCard courseId={courseId} assignment={assignment} />}

      {fetch.isPending && (
        <Card className="px-[18px] py-3.5">
          <Spinner label="Downloading new and changed submissions from Google Drive…" />
        </Card>
      )}
      {fetch.isError && <ErrorPanel error={fetch.error} />}
      {fetch.data && <FetchResult summary={fetch.data} />}
      {extract.isError && <ErrorPanel error={extract.error} />}
      {extract.data && <ExtractResult summary={extract.data} />}

      {submissions.isLoading && <Spinner />}
      {submissions.isError && <ErrorPanel error={submissions.error} />}
      {submissions.data && (
        // A container: the table adapts to its own width (narrower while the agent panel is open), not the window's.
        <Card className="@container">
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-line px-[18px] py-[13px]">
            <div className="min-w-0">
              <h3 className="text-sm font-bold">Submissions</h3>
              <div className="text-xs text-ink-muted">Live from Classroom. Fetch stores turned-in work as versions.</div>
            </div>
            <div className="flex rounded-lg border border-line bg-paper p-0.5" role="tablist" aria-label="Filter submissions">
              {(Object.keys(filters) as Filter[]).map((key) => (
                <button
                  key={key}
                  role="tab"
                  aria-selected={filter === key}
                  onClick={() => setFilter(key)}
                  className={`flex h-7 items-center gap-1.5 rounded-md px-2.5 text-xs font-semibold ${
                    filter === key ? "bg-white text-ink shadow-sm" : "text-ink-muted hover:text-ink"
                  }`}
                >
                  {filters[key].label}
                  <span className={filter === key ? "text-accent" : ""}>{counts[key]}</span>
                </button>
              ))}
            </div>
          </div>
          {rows.length === 0 ? (
            <EmptyState>{all.length === 0 ? "No students in this assignment yet." : "No submissions match this filter."}</EmptyState>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full border-collapse">
                <thead>
                  <tr className="border-b border-line bg-paper/50 text-left text-[11px] uppercase tracking-[0.06em] text-ink-muted">
                    <SortHeader label="Student" sortKey="student" sort={sort} onSort={sortBy} />
                    <SortHeader label="Status" sortKey="status" sort={sort} onSort={sortBy} />
                    <SortHeader label="Turned in" sortKey="turnedIn" sort={sort} onSort={sortBy} className="hidden @5xl:table-cell" />
                    <th className="px-[18px] py-2.5 font-semibold">Version</th>
                    <th className="px-[18px] py-2.5 font-semibold">
                      <span className="sr-only">Actions</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((submission, i) => {
                    const storedSubmission = storedById.get(submission.id);
                    const previous = rows[i - 1];
                    return (
                      <SubmissionRow
                        key={submission.id}
                        submission={submission}
                        stored={storedSubmission}
                        // A thicker divider where the students without turned-in work begin.
                        firstMissing={!hasWork(submission) && previous !== undefined && hasWork(previous)}
                        pickedVersionId={storedSubmission ? pickedVersions[storedSubmission.id] : undefined}
                        onPickVersion={(storedId, versionId) => setPickedVersions((p) => ({ ...p, [storedId]: versionId }))}
                        onPreview={(s, version) => setPreviewing({ submission: s, versionId: version.id })}
                        onGrade={(s, version) => openReview(s, version, true)}
                        onOpenReview={(s, version) => openReview(s, version, false)}
                        gradingVersionId={gradingVersionId}
                      />
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      )}

      {previewing && (
        <PreviewPanel
          submission={previewing.submission}
          initialVersionId={previewing.versionId}
          onClose={() => setPreviewing(null)}
        />
      )}
      {reviewing && (
        <GradingPanel
          submission={reviewing.submission}
          versionId={reviewing.version.id}
          versionNumber={reviewing.version.number}
          pending={gradingVersionId === reviewing.version.id}
          // Only this version's failure; a failed grading of another student is not shown here.
          error={grade.isError && grade.variables === reviewing.version.id ? grade.error : null}
          onRegrade={() => grade.mutate(reviewing.version.id)}
          regradeDisabled={grade.isPending}
          onClose={() => setReviewing(null)}
        />
      )}
    </>
  );
}

type SortKey = "student" | "status" | "turnedIn";
type Sort = { key: SortKey; descending: boolean };

// Returned work first, then work waiting for grading, then reclaimed; students with nothing turned in are always last
// (see sortSubmissions).
const statusRank: Record<string, number> = { RETURNED: 0, TURNED_IN: 1, RECLAIMED_BY_STUDENT: 2 };

/** Whether the student has turned in work (returned work counts: it was turned in first). */
function hasWork(submission: Submission): boolean {
  return submission.state === "TURNED_IN" || submission.state === "RETURNED";
}

function time(value: string | null | undefined): number {
  return value ? new Date(value).getTime() : 0;
}

/**
 * Sorts by the chosen column; ties go to the turn-in date (newest first), then the name. Students without turned-in
 * work stay at the bottom in either direction, so the "–" rows never mix with the work to grade.
 */
function sortSubmissions(list: Submission[], sort: Sort): Submission[] {
  const direction = sort.descending ? -1 : 1;
  const byName = (a: Submission, b: Submission) => a.studentName.localeCompare(b.studentName);
  const byNewest = (a: Submission, b: Submission) => time(b.turnedInAt) - time(a.turnedInAt);
  const byColumn: Record<SortKey, (a: Submission, b: Submission) => number> = {
    student: byName,
    status: (a, b) => (statusRank[a.state] ?? 9) - (statusRank[b.state] ?? 9),
    turnedIn: (a, b) => time(a.turnedInAt) - time(b.turnedInAt),
  };

  return [...list].sort((a, b) => {
    const missing = Number(!hasWork(a)) - Number(!hasWork(b));
    if (missing !== 0) {
      return missing;
    }
    return direction * byColumn[sort.key](a, b) || byNewest(a, b) || byName(a, b);
  });
}

type Filter = "all" | "returned" | "turnedIn" | "missing";

const filters: Record<Filter, { label: string; matches: (s: Submission) => boolean }> = {
  all: { label: "All", matches: () => true },
  returned: { label: "Returned", matches: (s) => s.state === "RETURNED" },
  turnedIn: { label: "Turned in", matches: (s) => s.state === "TURNED_IN" },
  missing: { label: "Not submitted", matches: (s) => !hasWork(s) },
};

function SortHeader({
  label,
  sortKey,
  sort,
  onSort,
  className = "",
}: {
  label: string;
  sortKey: SortKey;
  sort: Sort;
  onSort: (key: SortKey) => void;
  className?: string;
}) {
  const active = sort.key === sortKey;

  return (
    <th className={`px-[18px] py-2.5 font-semibold ${className}`} aria-sort={active ? (sort.descending ? "descending" : "ascending") : "none"}>
      <button onClick={() => onSort(sortKey)} className={`inline-flex items-center gap-1 uppercase tracking-[0.06em] hover:text-ink ${active ? "text-ink" : ""}`}>
        {label}
        <span className={active ? "" : "opacity-30"}>{active && sort.descending ? "↓" : "↑"}</span>
      </button>
    </th>
  );
}

function FetchResult({ summary }: { summary: FetchSummary }) {
  const tiles = [
    { label: "New submissions", value: summary.newSubmissions, note: "stored as v1" },
    { label: "New versions", value: summary.newVersions, note: "resubmitted after a change" },
    { label: "Unchanged", value: summary.unchanged, note: "already stored, not downloaded" },
    { label: "Not submitted", value: summary.notSubmitted, note: "nothing turned in" },
  ];

  return (
    <Card>
      <CardHeader title="Fetch finished" subtitle={`${summary.submissions} submissions checked`} />
      <div className="grid grid-cols-2 gap-px bg-line md:grid-cols-4">
        {tiles.map((tile) => (
          <div key={tile.label} className="bg-white px-[18px] py-3.5">
            <div className="text-xs text-ink-muted">{tile.label}</div>
            <div className="text-[28px] font-bold leading-tight tracking-tight">{tile.value}</div>
            <div className="text-xs font-semibold text-accent">{tile.note}</div>
          </div>
        ))}
      </div>
      <div className="border-t border-line px-[18px] py-2.5 text-xs">
        <b>Instructions:</b>{" "}
        <span className="text-ink-muted">
          {summary.instructionsVersion == null
            ? "could not be stored (see below); grading uses the previously stored version, if any"
            : summary.newInstructions
              ? `stored as v${summary.instructionsVersion} (new or changed in Classroom)`
              : `unchanged since v${summary.instructionsVersion}`}
        </span>
      </div>
      {summary.problems.length > 0 && (
        <div className="border-t border-line px-[18px] py-3">
          <div className="mb-1.5 text-xs font-bold">Needs your attention</div>
          <ul className="flex flex-col gap-1.5">
            {summary.problems.map((problem, i) => (
              <li key={i} className="flex gap-2 text-xs">
                <b className="shrink-0">{problem.studentName}</b>
                <span className="text-ink-muted">{problem.detail}</span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </Card>
  );
}

function ExtractResult({ summary }: { summary: ExtractionSummary }) {
  return (
    <Card>
      <CardHeader
        title="Extraction finished"
        subtitle={`${summary.versionsWithCode} of ${summary.versions} stored versions have code (the others hold only images or documents)`}
      />
      {summary.problems.length > 0 && (
        <ul className="flex flex-col gap-1.5 px-[18px] py-3">
          {summary.problems.map((problem, i) => (
            <li key={i} className="flex gap-2 text-xs">
              <b className="shrink-0">{problem.studentName}</b>
              <span className="text-ink-muted">{problem.detail}</span>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}

// Classroom's states, shown with the chip styles from the design.
const stateChip: Record<string, { tone: ChipTone; label: string }> = {
  TURNED_IN: { tone: "tint", label: "Turned in" },
  RETURNED: { tone: "accent", label: "Returned" },
  RECLAIMED_BY_STUDENT: { tone: "outline", label: "Reclaimed" },
  CREATED: { tone: "muted", label: "Not submitted" },
  NEW: { tone: "muted", label: "Not submitted" },
};

function SubmissionRow({
  submission,
  stored,
  firstMissing,
  pickedVersionId,
  onPickVersion,
  onPreview,
  onGrade,
  onOpenReview,
  gradingVersionId,
}: {
  submission: Submission;
  stored: StoredSubmission | undefined;
  firstMissing: boolean;
  pickedVersionId: string | undefined;
  onPickVersion: (storedId: string, versionId: string) => void;
  onPreview: (stored: StoredSubmission, version: StoredVersion) => void;
  onGrade: (stored: StoredSubmission, version: StoredVersion) => void;
  onOpenReview: (stored: StoredSubmission, version: StoredVersion) => void;
  gradingVersionId: string | undefined;
}) {
  const state = stateChip[submission.state] ?? { tone: "muted" as const, label: submission.state };
  const working = hasWork(submission);
  const version = stored?.versions.find((v) => v.id === pickedVersionId) ?? stored?.versions[0];
  const hasReview = useReview(version?.id).data !== undefined;
  const gradingThis = version !== undefined && gradingVersionId === version.id;

  return (
    <tr className={`border-b border-paper-dark last:border-b-0 hover:bg-paper/40 ${firstMissing ? "border-t-2 border-t-line" : ""}`}>
      <td className="px-[18px] py-3">
        <div className="flex items-center gap-2.5">
          <Avatar text={initials(submission.studentName)} />
          <div className="min-w-0">
            <div className={`font-semibold ${working ? "" : "text-ink-muted"}`}>{submission.studentName}</div>
            {submission.attachments.length > 0 && (
              <div className="mt-0.5 flex flex-wrap gap-x-2.5 gap-y-0.5">
                {submission.attachments.map((attachment, i) => (
                  <AttachmentLink key={i} attachment={attachment} />
                ))}
              </div>
            )}
          </div>
        </div>
      </td>
      <td className="px-[18px] py-3">
        <div className="flex flex-wrap gap-1.5">
          <Chip tone={state.tone}>{state.label}</Chip>
          {submission.late && <Chip tone="dashed">Late</Chip>}
        </div>
        {/* When the table is narrow the turn-in date moves under the status. */}
        {submission.turnedInAt && <div className="mt-1 text-[11px] text-ink-muted @5xl:hidden">{formatDate(submission.turnedInAt)}</div>}
      </td>
      <td className="hidden whitespace-nowrap px-[18px] py-3 text-ink-muted @5xl:table-cell">{formatDate(submission.turnedInAt)}</td>
      <td className="whitespace-nowrap px-[18px] py-3">
        {stored && version ? (
          <VersionPicker stored={stored} version={version} onPick={(id) => onPickVersion(stored.id, id)} />
        ) : (
          <span className="text-xs text-ink-muted">{working ? "Not fetched yet" : "–"}</span>
        )}
      </td>
      <td className="whitespace-nowrap px-[18px] py-3">
        {stored && version && (
          <div className="flex items-center justify-end gap-2">
            <Button small onClick={() => onPreview(stored, version)} title={`Open v${version.number} from our stored copy`}>
              Preview
            </Button>
            {/* A review or a running grading opens the panel; otherwise this grades. One grading at a time. */}
            {hasReview || gradingThis ? (
              <Button small variant="dark" onClick={() => onOpenReview(stored, version)} title="Show the AI review next to the code">
                {gradingThis ? "Grading…" : "AI review"}
              </Button>
            ) : (
              <Button
                small
                variant="primary"
                onClick={() => onGrade(stored, version)}
                disabled={gradingVersionId !== undefined}
                title={`Send v${version.number} and the assignment's instructions to the AI for a draft review`}
              >
                Grade with AI
              </Button>
            )}
          </div>
        )}
      </td>
    </tr>
  );
}

/** The stored versions of one student, newest first; the picked one is what Preview and Grade use. */
function VersionPicker({ stored, version, onPick }: { stored: StoredSubmission; version: StoredVersion; onPick: (versionId: string) => void }) {
  const files = version.files.filter((f) => f.stored).length;

  return (
    <div className="flex items-center gap-2">
      <select
        value={version.id}
        onChange={(e) => onPick(e.target.value)}
        aria-label={`Version of ${stored.studentName}'s submission`}
        title={`Fetched ${formatDate(version.fetchedAt)}`}
        className="h-7 cursor-pointer rounded-lg border border-line bg-white pl-2 pr-1 text-xs font-semibold hover:border-accent"
      >
        {stored.versions.map((v, i) => (
          <option key={v.id} value={v.id}>
            {`v${v.number} · ${formatDay(v.fetchedAt)}${i === 0 ? " (latest)" : ""}`}
          </option>
        ))}
      </select>
      <span className="hidden text-xs text-ink-muted @5xl:inline">
        {files} {files === 1 ? "file" : "files"}
      </span>
    </div>
  );
}

const attachmentLabel: Record<Attachment["kind"], string> = { DriveFile: "Drive", Link: "Link", Other: "Other" };

function AttachmentLink({ attachment }: { attachment: Attachment }) {
  const content = (
    <>
      <span className="text-[9px] font-bold uppercase tracking-wide">{attachmentLabel[attachment.kind]}</span>
      <span className="max-w-[200px] truncate">{attachment.title}</span>
    </>
  );
  const className = "inline-flex items-center gap-1 text-xs text-ink-muted";

  return attachment.url ? (
    <a href={attachment.url} target="_blank" rel="noreferrer" className={`${className} hover:text-accent hover:underline`}>
      {content}
    </a>
  ) : (
    <span className={className}>{content}</span>
  );
}
