import { useEffect, useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import type { CodeMistake, JobInfo, StoredFile, StoredSubmission, StoredVersion, Submission } from "../api/client";
import { useDiscardGrading, useReview, useVersionGradings } from "../api/hooks";
import { formatDate, formatDay, initials } from "../format";
import { CodeBrowser, type CodeFocus, severityColor } from "./CodeBrowser";
import { Avatar, Button, Chip, type ChipTone, ErrorPanel, Spinner } from "./ui";

export const stateChip: Record<string, { tone: ChipTone; label: string }> = {
  TURNED_IN: { tone: "tint", label: "Turned in" },
  RETURNED: { tone: "accent", label: "Returned" },
  RECLAIMED_BY_STUDENT: { tone: "outline", label: "Reclaimed" },
  CREATED: { tone: "muted", label: "Not submitted" },
  NEW: { tone: "muted", label: "Not submitted" },
};

export function hasWork(submission: Submission): boolean {
  return submission.state === "TURNED_IN" || submission.state === "RETURNED";
}

export function SubmissionDetail({
  submission,
  stored,
  version,
  onPickVersion,
  onPreview,
  onGrade,
  job,
  queuing,
  queueError,
  onCancel,
  onBack,
}: {
  onBack: () => void;
  submission: Submission;
  stored: StoredSubmission | undefined;
  version: StoredVersion | undefined;
  onPickVersion: (versionId: string) => void;
  onPreview: () => void;
  onGrade: () => void;
  job: JobInfo | undefined;
  queuing: boolean;
  queueError: unknown;
  onCancel: (jobId: string) => void;
}) {
  const current = useReview(version?.id).data ?? undefined;
  const history = useVersionGradings(version?.id).data ?? [];
  const discard = useDiscardGrading();
  const [viewingId, setViewingId] = useState<string | null>(null);
  const review = (viewingId ? history.find((g) => g.id === viewingId) : undefined) ?? current;
  const grading = job !== undefined && (job.status === "Queued" || job.status === "Running");
  const gradeDisabled = grading || queuing;
  const jobFailed = job?.status === "Failed" && (!current || job.createdAt > current.createdAt);
  const gradingError = queueError ?? (jobFailed ? new Error(job.error ?? "The grading failed.") : null);

  useEffect(() => {
    setViewingId(null);
  }, [current?.id]);
  const [focus, setFocus] = useState<CodeFocus>();
  const [tab, setTab] = useState<"review" | "code">("review");
  const [expanded, setExpanded] = useState(false);
  const state = stateChip[submission.state] ?? { tone: "muted" as const, label: submission.state };
  const working = hasWork(submission);

  useEffect(() => {
    if (!expanded) {
      return;
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setExpanded(false);
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [expanded]);

  function showMistake(file: string, line: number) {
    setFocus({ file, line, key: Date.now() });
    setTab("code");
  }

  return (
    <section className={`@container flex min-h-0 min-w-0 flex-col ${expanded ? "fixed inset-0 z-40 bg-paper" : "flex-1"}`}>
      <header className="flex flex-wrap items-center justify-between gap-3 border-b border-line bg-white px-4 py-3 sm:px-5">
        {!expanded && (
          <button onClick={onBack} className="w-full text-left text-xs font-semibold text-accent @2xl/workspace:hidden">
            ← All students
          </button>
        )}
        <div className="flex min-w-0 items-center gap-3">
          <Avatar text={initials(submission.studentName)} userId={submission.studentId} />
          <div className="min-w-0">
            <div className="flex min-w-0 flex-wrap items-center gap-2">
              <b className="truncate text-[15px]">{submission.studentName}</b>
              <Chip tone={state.tone}>{state.label}</Chip>
              {working && submission.late && <Chip tone="dashed">Late</Chip>}
            </div>
            <div className="text-xs text-ink-muted">
              {working ? `Turned in ${formatDate(version?.turnedInAt ?? submission.turnedInAt)}` : "Nothing turned in yet"}
            </div>
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {stored && version && (
            <>
              <VersionPicker stored={stored} version={version} onPick={onPickVersion} />
              <Button small onClick={onPreview} title={`Images, PDFs and documents of v${version.number}, from our stored copy`}>
                Preview files
              </Button>
              <Button
                small
                variant={review ? "outline" : "primary"}
                onClick={onGrade}
                disabled={gradeDisabled}
                title={`Send v${version.number} and the assignment's instructions to the AI for a draft review`}
              >
                {queuing ? "Queuing…" : grading ? (job.status === "Queued" ? "Queued…" : "Grading…") : review ? "Grade again" : "Grade with AI"}
              </Button>
            </>
          )}
          <Button small onClick={() => setExpanded(!expanded)} title={expanded ? "Back to the list (Esc)" : "Use the whole window"}>
            {expanded ? "Exit full screen" : "Expand"}
          </Button>
        </div>
      </header>

      {!stored || !version ? (
        <div className="flex flex-1 items-center justify-center px-6 text-center text-ink-muted">
          {working ? (
            <div>
              <div className="font-semibold text-ink">Not fetched yet</div>
              Fetch from Classroom to store this submission; then it can be previewed and graded.
            </div>
          ) : (
            <div>
              <div className="font-semibold text-ink">Nothing turned in</div>
              This student has not turned in work for this assignment.
            </div>
          )}
        </div>
      ) : (
        <div className="flex min-h-0 flex-1 flex-col @4xl:flex-row">
          <nav className="flex shrink-0 gap-1 border-b border-line bg-white px-4 sm:px-5 @4xl:hidden" aria-label="Review or code">
            {(["review", "code"] as const).map((key) => (
              <button
                key={key}
                onClick={() => setTab(key)}
                aria-pressed={tab === key}
                className={`-mb-px border-b-2 px-3 py-2 text-xs font-semibold ${
                  tab === key ? "border-accent text-ink" : "border-transparent text-ink-muted hover:text-ink"
                }`}
              >
                {key === "review" ? `Review${review ? ` · ${review.mistakes.length} mistakes` : ""}` : "Code"}
              </button>
            ))}
          </nav>

          <div
            className={`min-h-0 overflow-y-auto @4xl:block @4xl:w-[42%] @4xl:max-w-[560px] @4xl:flex-none @4xl:border-r @4xl:border-line ${
              tab === "review" ? "block flex-1" : "hidden"
            }`}
          >
            <div className="flex flex-col gap-4 p-4 sm:p-5">
              {version.extractionDetail && (
                <div className="rounded-[10px] border border-line border-l-4 border-l-ink bg-white px-4 py-2.5 text-xs">
                  <b>Extraction problem:</b> <span className="text-ink-muted">{version.extractionDetail}</span>
                </div>
              )}
              <FilesCard version={version} />
              {grading && (
                <div className="flex flex-wrap items-center justify-between gap-2 rounded-[10px] border border-line bg-white px-4 py-3 text-xs">
                  <Spinner
                    label={
                      job.status === "Queued"
                        ? job.attempts > 0
                          ? `Retrying soon (attempt ${job.attempts + 1} of ${job.maxAttempts}) after: ${job.error ?? "an error"}`
                          : "Queued: waiting for a free grading slot…"
                        : "Grading in the background… you can leave this page; the draft appears here when it is ready."
                    }
                  />
                  {!job.cancelRequested && (
                    <Button small onClick={() => onCancel(job.id)}>
                      Cancel
                    </Button>
                  )}
                </div>
              )}
              {!grading && gradingError != null && <ErrorPanel error={gradingError} />}
              {review ? (
                <div className={`flex flex-col gap-4 ${grading ? "opacity-50" : ""}`}>
                  <div>
                    <div className="mb-2 flex flex-wrap items-center gap-2">
                      <h3 className="text-[11px] font-bold uppercase tracking-[0.06em] text-accent">AI draft</h3>
                      {review.status === "Discarded" ? (
                        <Chip tone="muted">Discarded</Chip>
                      ) : (
                        <Chip tone="tint">Saved {formatDate(review.createdAt)}</Chip>
                      )}
                      {review.score != null && (
                        <b className="tabular-nums">
                          {points(review.score)} / {review.maxScore ?? "–"}
                          {review.bonus ? <span className="font-normal text-ink-muted"> +{points(review.bonus)} bonus</span> : null}
                        </b>
                      )}
                      <span className="ml-auto flex items-center gap-2">
                        {history.length > 1 && (
                          <select
                            value={review.id}
                            onChange={(e) => setViewingId(e.target.value)}
                            aria-label="Saved drafts of this version"
                            className="h-7 cursor-pointer rounded-lg border border-line bg-white pl-2 pr-1 text-xs font-semibold hover:border-accent"
                          >
                            {history.map((g) => (
                              <option key={g.id} value={g.id}>
                                {`${formatDate(g.createdAt)}${g.score != null ? ` · ${points(g.score)}` : ""}${
                                  g.id === current?.id ? " · current" : g.status === "Discarded" ? " · discarded" : " · earlier"
                                }`}
                              </option>
                            ))}
                          </select>
                        )}
                        {review.status === "Draft" && (
                          <Button
                            small
                            onClick={() => discard.mutate(review.id)}
                            disabled={discard.isPending}
                            title="Throw this draft away; it stays in the history and the previous draft becomes current"
                          >
                            {discard.isPending ? "Discarding…" : "Discard"}
                          </Button>
                        )}
                      </span>
                    </div>
                    <div className="markdown rounded-[10px] border border-line bg-white px-5 py-4">
                      <ReactMarkdown remarkPlugins={[remarkGfm]}>{review.review}</ReactMarkdown>
                    </div>
                    <div className="mt-1.5 text-[11px] text-ink-muted">
                      {review.model} · instructions v{review.instructionsVersion} · read from the code only, nothing was compiled or run
                    </div>
                  </div>
                  <MistakeList mistakes={review.mistakes} onShow={showMistake} />
                  <details className="text-xs">
                    <summary className="cursor-pointer font-semibold text-ink-muted hover:text-ink">
                      Files sent to the model · {review.sentFiles.length}
                      {review.skippedFiles.length > 0 && ` (${review.skippedFiles.length} not sent)`}
                    </summary>
                    <ul className="mt-1.5 flex flex-col gap-0.5 text-ink-muted">
                      {review.sentFiles.map((item) => (
                        <li key={item}>{item}</li>
                      ))}
                      {review.skippedFiles.map((item) => (
                        <li key={item} className="line-through">{item}</li>
                      ))}
                    </ul>
                  </details>
                </div>
              ) : (
                !grading && (
                  <div className="rounded-[10px] border border-dashed border-line px-5 py-6 text-center">
                    <div className="font-semibold">No AI draft yet</div>
                    <div className="mt-0.5 text-xs text-ink-muted">
                      Grade with AI to get a written review and line-level mistakes, marked in the code.
                    </div>
                    <Button small variant="primary" className="mt-3" onClick={onGrade} disabled={gradeDisabled}>
                      Grade with AI
                    </Button>
                  </div>
                )
              )}
            </div>
          </div>

          <div className={`min-h-0 min-w-0 flex-1 flex-col p-2 sm:p-4 @4xl:flex ${tab === "code" ? "flex" : "hidden"}`}>
            {version.hasCode ? (
              <CodeBrowser versionId={version.id} className="h-full" mistakes={review?.mistakes} focus={focus} />
            ) : (
              <div className="flex flex-1 items-center justify-center text-center text-ink-muted">
                No code in this version. Preview files shows its images and documents.
              </div>
            )}
          </div>
        </div>
      )}
    </section>
  );
}

function VersionPicker({ stored, version, onPick }: { stored: StoredSubmission; version: StoredVersion; onPick: (versionId: string) => void }) {
  return (
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
  );
}

const fileStatus: Record<StoredFile["status"], string> = {
  Extracted: "Code extracted",
  Stored: "Stored",
  Unsupported: "Not stored · unsupported type",
  TooLarge: "Not stored · too large",
};

function FilesCard({ version }: { version: StoredVersion }) {
  return (
    <div className="rounded-[10px] border border-line bg-white">
      <div className="flex items-center justify-between gap-3 border-b border-line px-4 py-2">
        <b className="text-xs">Files</b>
        <span className="text-[11px] text-ink-muted">
          {version.files.length} {version.files.length === 1 ? "attachment" : "attachments"} · v{version.number}
        </span>
      </div>
      {version.files.length === 0 ? (
        <div className="px-4 py-2.5 text-xs text-ink-muted">This version has no files.</div>
      ) : (
        <ul className="divide-y divide-paper-dark">
          {version.files.map((file) => (
            <li key={file.id} className="flex items-center gap-2.5 px-4 py-1.5 text-xs">
              <span className="w-10 shrink-0 rounded bg-paper-dark py-0.5 text-center text-[9px] font-bold tracking-wide text-ink-muted">
                {fileLabel(file)}
              </span>
              <span className="min-w-0 flex-1 truncate">{file.name}</span>
              <span className={`shrink-0 ${file.stored ? "text-ink-muted" : "font-semibold text-ink"}`}>
                {file.kind === "CodeArchive" && file.status === "Stored" ? "Stored · not extracted" : fileStatus[file.status]}
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function fileLabel(file: StoredFile): string {
  const extension = file.name.includes(".") ? file.name.split(".").pop()!.toUpperCase() : "";
  return extension && extension.length <= 4 ? extension : file.kind === "Image" ? "IMG" : "FILE";
}

export function MistakeList({ mistakes, onShow }: { mistakes: CodeMistake[]; onShow: (file: string, line: number) => void }) {
  if (mistakes.length === 0) {
    return <div className="rounded-[10px] border border-line bg-white px-4 py-3 text-xs text-ink-muted">No mistakes found.</div>;
  }

  return (
    <section>
      <h3 className="mb-2 text-[11px] font-bold uppercase tracking-[0.06em] text-accent">Mistakes · {mistakes.length}</h3>
      <ul className="flex flex-col gap-2">
        {mistakes.map((m, i) => {
          const name = m.file?.slice(m.file.lastIndexOf("/") + 1);
          const lines = m.startLine == null ? null : m.endLine && m.endLine !== m.startLine ? `${m.startLine}–${m.endLine}` : `${m.startLine}`;
          const canShow = m.file != null && m.startLine != null;
          return (
            <li key={i}>
              <button
                onClick={canShow ? () => onShow(m.file!, m.startLine!) : undefined}
                disabled={!canShow}
                className="flex w-full gap-3 rounded-[10px] border border-line bg-white px-4 py-2.5 text-left enabled:hover:border-accent disabled:cursor-default"
                title={canShow ? "Show in the code" : undefined}
              >
                <span className="mt-1 h-2.5 w-2.5 shrink-0 rounded-full" style={{ backgroundColor: severityColor[m.severity] }} />
                <span className="min-w-0 flex-1">
                  <span className="flex flex-wrap items-center gap-x-2 text-xs">
                    <b>{m.severity}</b>
                    <span className="text-ink-muted">{m.category}</span>
                    <span className={`ml-auto font-mono ${canShow ? "text-accent" : "text-ink-muted"}`}>
                      {name ? (lines ? `${name}:${lines}` : name) : "Whole submission"}
                    </span>
                  </span>
                  <span className="mt-0.5 block text-[13px]">{m.message}</span>
                </span>
              </button>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

function points(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(1);
}
