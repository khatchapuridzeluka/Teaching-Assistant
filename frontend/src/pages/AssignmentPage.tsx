import { useState } from "react";
import { useParams } from "react-router";
import type { Attachment, ExtractionSummary, FetchSummary, StoredSubmission, Submission } from "../api/client";
import { useAssignments, useFetchSubmissions, useReExtract, useStoredSubmissions, useSubmissions } from "../api/hooks";
import { PreviewPanel } from "../components/PreviewPanel";
import { Avatar, Button, Card, CardHeader, Chip, type ChipTone, EmptyState, ErrorPanel, PageHeader, Spinner } from "../components/ui";
import { formatDate, initials } from "../format";

// GET …/submissions, GET …/submissions/stored and POST …/submissions/fetch
export function AssignmentPage() {
  const { courseId = "", assignmentId = "" } = useParams();
  const assignment = useAssignments(courseId).data?.find((a) => a.id === assignmentId);
  const submissions = useSubmissions(courseId, assignmentId);
  const stored = useStoredSubmissions(courseId, assignmentId);
  const fetch = useFetchSubmissions(courseId, assignmentId);
  const extract = useReExtract(courseId, assignmentId);
  const [previewing, setPreviewing] = useState<StoredSubmission | null>(null);

  // Stored copies are matched to the live list by the Classroom submission ID.
  const storedById = new Map(stored.data?.map((s) => [s.externalSubmissionId, s]));

  const turnedIn = submissions.data?.filter((s) => s.state === "TURNED_IN" || s.state === "RETURNED").length ?? 0;

  return (
    <>
      <PageHeader
        title={assignment?.title ?? "Assignment"}
        subtitle={
          assignment && (
            <>
              Due {formatDate(assignment.dueAt)} · {assignment.maxPoints ?? "–"} points
              {submissions.data && ` · ${turnedIn} of ${submissions.data.length} turned in`}
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

      {assignment?.description && <TaskStatement text={assignment.description} />}

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
        <Card>
          <CardHeader title="Submissions" subtitle="Live from Classroom. Fetch stores turned-in work as versions." />
          {submissions.data.length === 0 ? (
            <EmptyState>No students in this assignment yet.</EmptyState>
          ) : (
            <table className="w-full border-collapse">
              <thead>
                <tr className="border-b border-line text-left text-[11px] uppercase tracking-[0.06em] text-ink-muted">
                  <th className="px-[18px] py-2.5 font-semibold">Student</th>
                  <th className="px-[18px] py-2.5 font-semibold">Status</th>
                  <th className="px-[18px] py-2.5 font-semibold">Turned in</th>
                  <th className="px-[18px] py-2.5 font-semibold">Attachments</th>
                  <th className="px-[18px] py-2.5 font-semibold">Stored copy</th>
                </tr>
              </thead>
              <tbody>
                {submissions.data.map((submission) => (
                  <SubmissionRow
                    key={submission.id}
                    submission={submission}
                    stored={storedById.get(submission.id)}
                    onPreview={setPreviewing}
                  />
                ))}
              </tbody>
            </table>
          )}
        </Card>
      )}

      {previewing && <PreviewPanel submission={previewing} onClose={() => setPreviewing(null)} />}
    </>
  );
}

function TaskStatement({ text }: { text: string }) {
  const [open, setOpen] = useState(false);
  const long = text.length > 280;

  return (
    <Card className="px-[18px] py-3.5">
      <div className="mb-1 text-[11px] font-bold uppercase tracking-[0.06em] text-accent">Task statement</div>
      <p className={`whitespace-pre-wrap ${long && !open ? "line-clamp-3" : ""}`}>{text}</p>
      {long && (
        <button onClick={() => setOpen(!open)} className="mt-1 text-xs font-semibold text-accent">
          {open ? "Show less" : "Show all"}
        </button>
      )}
    </Card>
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
  onPreview,
}: {
  submission: Submission;
  stored: StoredSubmission | undefined;
  onPreview: (stored: StoredSubmission) => void;
}) {
  const state = stateChip[submission.state] ?? { tone: "muted" as const, label: submission.state };
  const latest = stored?.versions[0];
  const turnedIn = submission.state === "TURNED_IN" || submission.state === "RETURNED";

  return (
    <tr className="border-b border-paper-dark align-top last:border-b-0">
      <td className="px-[18px] py-2.5">
        <div className="flex items-center gap-2.5">
          <Avatar text={initials(submission.studentName)} />
          <b>{submission.studentName}</b>
        </div>
      </td>
      <td className="px-[18px] py-2.5">
        <div className="flex flex-wrap gap-1.5 pt-1">
          <Chip tone={state.tone}>{state.label}</Chip>
          {submission.late && <Chip tone="dashed">Late</Chip>}
        </div>
      </td>
      <td className="whitespace-nowrap px-[18px] py-2.5 pt-3.5 text-ink-muted">{formatDate(submission.turnedInAt)}</td>
      <td className="px-[18px] py-2.5">
        {submission.attachments.length === 0 ? (
          <span className="block pt-1 text-ink-muted">–</span>
        ) : (
          <div className="flex flex-wrap gap-1.5 pt-0.5">
            {submission.attachments.map((attachment, i) => (
              <AttachmentLink key={i} attachment={attachment} />
            ))}
          </div>
        )}
      </td>
      <td className="whitespace-nowrap px-[18px] py-2.5">
        {stored && latest ? (
          <div className="flex items-center gap-2 pt-0.5">
            <Button small variant="primary" onClick={() => onPreview(stored)}>
              Preview
            </Button>
            <span className="text-xs text-ink-muted">
              v{latest.number} · {latest.files.filter((f) => f.stored).length} files
            </span>
          </div>
        ) : (
          <span className="block pt-1 text-xs text-ink-muted">{turnedIn ? "Fetch to preview" : "–"}</span>
        )}
      </td>
    </tr>
  );
}

const attachmentLabel: Record<Attachment["kind"], string> = { DriveFile: "Drive", Link: "Link", Other: "Other" };

function AttachmentLink({ attachment }: { attachment: Attachment }) {
  const content = (
    <>
      <span className="text-[10px] font-bold uppercase text-ink-muted">{attachmentLabel[attachment.kind]}</span>
      <span className="max-w-[220px] truncate">{attachment.title}</span>
    </>
  );
  const className = "inline-flex items-center gap-1.5 rounded-lg border border-line bg-white px-2.5 py-1 text-xs font-semibold";

  return attachment.url ? (
    <a href={attachment.url} target="_blank" rel="noreferrer" className={`${className} hover:border-accent`}>
      {content}
    </a>
  ) : (
    <span className={className}>{content}</span>
  );
}
