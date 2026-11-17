import { useEffect, useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import type { CodeMistake, CodeStrength, JobInfo, StoredFile, StoredSubmission, StoredVersion, Submission } from "../api/client";
import { useGradingReport, useReview, useVersionGradings } from "../api/hooks";
import { formatDate, formatDay, initials } from "../format";
import { CodeBrowser, type CodeFocus, severityColor, strengthColor } from "./CodeBrowser";
import { BuildCheck } from "./BuildCheck";
import { DiscardDraftButton } from "./DiscardDraftButton";
import { ExportPdfLink } from "./ExportPdfLink";
import { keptMistakes, keptStrengths, ReportEditor } from "./ReportEditor";
import { Avatar, Button, Chip, type ChipTone, ErrorPanel, Notice, Spinner } from "./ui";
import { ArrowLeft, BadgeCheck, CircleCheck, Eye, FileArchive, FileCode2, FileText, Image as ImageIcon, Maximize2, Minimize2, Pencil, Sparkles, ThumbsUp } from "lucide-react";

export const stateChip: Record<string, { tone: ChipTone; label: string }> = {
  TURNED_IN: { tone: "info", label: "Turned in" },
  RETURNED: { tone: "success", label: "Returned" },
  RECLAIMED_BY_STUDENT: { tone: "warn", label: "Reclaimed" },
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
  const [viewingId, setViewingId] = useState<string | null>(null);
  const review = (viewingId ? history.find((g) => g.id === viewingId) : undefined) ?? current;
  const grading = job !== undefined && (job.status === "Queued" || job.status === "Running");
  const gradeDisabled = grading || queuing;
  const jobFailed = job?.status === "Failed" && (!current || job.createdAt > current.createdAt);
  const gradingError = queueError ?? (jobFailed ? new Error(job.error ?? "The grading failed.") : null);

  const report = useGradingReport(review?.id).data;
  const [editing, setEditing] = useState(false);
  const [showOriginal, setShowOriginal] = useState(false);
  const showReport = report != null && !showOriginal;
  const shownMistakes: CodeMistake[] | undefined = showReport ? keptMistakes(report) : review?.mistakes;
  const shownStrengths: CodeStrength[] | undefined = showReport ? keptStrengths(report) : review?.strengths;

  useEffect(() => {
    setViewingId(null);
  }, [current?.id]);

  useEffect(() => {
    setShowOriginal(false);
    setEditing(false);
  }, [review?.id]);
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
    <section className={`@container flex min-h-0 min-w-0 flex-col ${expanded ? "fixed inset-0 z-40 animate-fade bg-paper" : "flex-1"}`}>
      <header className="flex flex-wrap items-center justify-between gap-3 border-b border-line bg-white px-4 py-2.5 sm:px-5">
        {!expanded && (
          <button onClick={onBack} className="flex w-full items-center gap-1 text-left text-xs font-semibold text-accent @2xl/workspace:hidden">
            <ArrowLeft className="h-3.5 w-3.5" /> All students
          </button>
        )}
        <div className="flex min-w-0 items-center gap-3">
          <Avatar text={initials(submission.studentName)} userId={submission.studentId} size="lg" />
          <div className="min-w-0">
            <div className="flex min-w-0 flex-wrap items-center gap-2">
              <b className="truncate font-display text-[19px] font-semibold leading-tight">{submission.studentName}</b>
              <Chip tone={state.tone}>{state.label}</Chip>
              {working && submission.late && <Chip tone="warn">Late</Chip>}
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
              <Button small icon={<Eye />} onClick={onPreview} title={`Images, PDFs and documents of v${version.number}, from our stored copy`}>
                Preview files
              </Button>
              <Button
                small
                icon={<Sparkles />}
                variant={review ? "outline" : "primary"}
                onClick={onGrade}
                disabled={gradeDisabled}
                title={`Send v${version.number} and the assignment's instructions to the AI for a draft review`}
              >
                {queuing ? "Queuing…" : grading ? (job.status === "Queued" ? "Queued…" : "Grading…") : review ? "Grade again" : "Grade with AI"}
              </Button>
            </>
          )}
          <Button
            small
            variant="ghost"
            className="px-1.5"
            onClick={() => setExpanded(!expanded)}
            title={expanded ? "Back to the list (Esc)" : "Use the whole window"}
            aria-label={expanded ? "Exit full screen" : "Expand"}
          >
            {expanded ? <Minimize2 className="h-4 w-4" /> : <Maximize2 className="h-4 w-4" />}
          </Button>
        </div>
      </header>

      {!stored || !version ? (
        <div className="flex flex-1 flex-col items-center justify-center gap-3 px-6 text-center text-ink-muted">
          <span className="flex h-14 w-14 items-center justify-center rounded-full border border-dashed border-ink-faint text-ink-faint">
            <FileCode2 className="h-6 w-6" />
          </span>
          {working ? (
            <div>
              <div className="font-display text-[17px] font-semibold text-ink">Not fetched yet</div>
              Fetch from Classroom to store this submission; then it can be previewed and graded.
            </div>
          ) : (
            <div>
              <div className="font-display text-[17px] font-semibold text-ink">Nothing turned in</div>
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
                className={`-mb-px border-b-2 px-3 py-2 text-xs font-semibold transition-colors ${
                  tab === key ? "border-accent text-accent-strong" : "border-transparent text-ink-muted hover:text-ink"
                }`}
              >
                {key === "review" ? `Review${shownMistakes ? ` · ${shownMistakes.length} mistakes` : ""}` : "Code"}
              </button>
            ))}
          </nav>

          <div
            className={`min-h-0 overflow-y-auto bg-paper/60 @4xl:block @4xl:w-[42%] @4xl:max-w-[560px] @4xl:flex-none @4xl:border-r @4xl:border-line ${
              tab === "review" ? "block flex-1" : "hidden"
            }`}
          >
            <div className="flex flex-col gap-4 p-4 sm:p-5">
              {version.extractionDetail && (
                <Notice tone="warn" title="Extraction problem">
                  {version.extractionDetail}
                </Notice>
              )}
              <FilesCard version={version} />
              {grading && (
                <div className="flex flex-wrap items-center justify-between gap-2 rounded-[10px] border border-accent/25 bg-accent-tint/60 px-4 py-3 text-xs">
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
                  <div className="surface overflow-hidden">
                    <div
                      className={`flex flex-wrap items-center gap-x-4 gap-y-2 border-b border-line border-l-4 px-4 py-3 ${
                        showReport ? (report.status === "Approved" ? "border-l-success bg-success-tint/40" : "border-l-accent bg-accent-tint/40") : "border-l-mark bg-mark-tint/40"
                      }`}
                    >
                      <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                          <h3 className="eyebrow flex items-center gap-1.5 text-ink">
                            {showReport ? <BadgeCheck className="h-3.5 w-3.5 text-success" /> : <Sparkles className="h-3.5 w-3.5 text-warn" />}
                            {showReport ? "Your report" : "AI draft"}
                          </h3>
                          {review.status === "Discarded" ? (
                            <Chip tone="muted">Discarded</Chip>
                          ) : showReport ? (
                            report.status === "Approved" ? (
                              <Chip tone="success" icon={<CircleCheck />} className="animate-stamp">
                                Approved {formatDate(report.approvedAt)}
                              </Chip>
                            ) : (
                              <Chip tone="info">In review · saved {formatDate(report.updatedAt)}</Chip>
                            )
                          ) : (
                            <Chip tone="warn">Waiting for your review · {formatDate(review.createdAt)}</Chip>
                          )}
                        </div>
                        {(showReport ? report.score : review.score) != null ? (
                          <div className="mt-1.5 flex items-baseline gap-1.5">
                            <b className="num text-[32px] leading-none">{points((showReport ? report.score : review.score)!)}</b>
                            <span className="num text-[16px] font-medium text-ink-muted">/ {review.maxScore ?? "–"}</span>
                            {(showReport ? report.bonus : review.bonus) ? (
                              <span className="ml-1 rounded bg-success-tint px-1.5 py-0.5 font-mono text-[11px] font-semibold text-success-ink">
                                +{points((showReport ? report.bonus : review.bonus)!)} bonus
                              </span>
                            ) : null}
                          </div>
                        ) : (
                          <div className="mt-1 text-xs text-ink-muted">The score is in the review text.</div>
                        )}
                      </div>
                      <span className="ml-auto flex flex-wrap items-center justify-end gap-2">
                        {report != null && (
                          <button
                            onClick={() => setShowOriginal(!showOriginal)}
                            className="text-xs font-semibold text-accent hover:underline"
                            title={showOriginal ? "Show your edited report" : "Show the AI draft as Claude wrote it"}
                          >
                            {showOriginal ? "Show your report" : "AI original"}
                          </button>
                        )}
                        {history.length > 1 && (
                          <select
                            value={review.id}
                            onChange={(e) => setViewingId(e.target.value)}
                            aria-label="Saved drafts of this version"
                            className="field h-7 max-w-[190px] px-1.5 font-medium"
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
                        <ExportPdfLink gradingId={review.id} />
                        {review.status === "Draft" && (
                          <DiscardDraftButton
                            gradingId={review.id}
                            reportStatus={report?.status}
                            title="Throw this draft away; it stays in the history and the previous draft becomes current"
                          />
                        )}
                        {review.status === "Draft" && (
                          <Button
                            small
                            icon={<Pencil />}
                            variant={report?.status === "Approved" ? "outline" : "primary"}
                            onClick={() => setEditing(true)}
                            title="Correct the score, feedback and mistakes; saved as your report, the AI draft stays as it was"
                          >
                            {report ? "Edit report" : "Review & approve"}
                          </Button>
                        )}
                      </span>
                    </div>
                    <div className="markdown px-5 py-4">
                      <ReactMarkdown remarkPlugins={[remarkGfm]}>{showReport ? report.review : review.review}</ReactMarkdown>
                    </div>
                    <div className="border-t border-paper-dark px-5 py-2.5">
                      <BuildCheck
                        status={review.buildStatus}
                        output={review.buildOutput}
                        styleFindings={review.styleFindings}
                        styleOutput={review.styleOutput}
                        pmdFindings={review.pmdFindings}
                        pmdOutput={review.pmdOutput}
                      />
                    </div>
                    <div className="border-t border-paper-dark px-5 py-2 font-mono text-[10.5px] text-ink-faint">
                      {review.model} · instructions v{review.instructionsVersion}
                    </div>
                  </div>
                  <MistakeList mistakes={shownMistakes ?? []} onShow={showMistake} />
                  <StrengthList strengths={shownStrengths ?? []} onShow={showMistake} />
                  {editing && (
                    <ReportEditor grading={review} report={report} studentName={submission.studentName} onClose={() => setEditing(false)} />
                  )}
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
                  <div className="flex flex-col items-center rounded-[10px] border border-dashed border-ink-faint/60 bg-white/60 px-5 py-8 text-center">
                    <span className="mb-3 flex h-11 w-11 items-center justify-center rounded-full bg-mark-tint text-mark-ink">
                      <Sparkles className="h-5 w-5" />
                    </span>
                    <div className="font-display text-[17px] font-semibold">No AI draft yet</div>
                    <div className="mt-0.5 max-w-[340px] text-xs text-ink-muted">
                      Grade with AI to get a written review and line-level mistakes, marked in the code.
                    </div>
                    <Button small variant="primary" icon={<Sparkles />} className="mt-3.5" onClick={onGrade} disabled={gradeDisabled}>
                      Grade with AI
                    </Button>
                  </div>
                )
              )}
            </div>
          </div>

          <div className={`min-h-0 min-w-0 flex-1 flex-col p-2 sm:p-3 @4xl:flex ${tab === "code" ? "flex" : "hidden"}`}>
            {version.hasCode ? (
              <CodeBrowser versionId={version.id} className="h-full" mistakes={shownMistakes} strengths={shownStrengths} focus={focus} />
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
      className="field h-7 px-1.5 font-mono text-[11px] font-medium"
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

function FileIcon({ file }: { file: StoredFile }) {
  const Icon = file.kind === "Image" ? ImageIcon : file.kind === "CodeArchive" ? FileArchive : file.kind === "SourceFile" ? FileCode2 : FileText;
  return <Icon className="h-4 w-4 shrink-0 text-ink-faint" />;
}

function FilesCard({ version }: { version: StoredVersion }) {
  return (
    <div className="surface">
      <div className="flex items-center justify-between gap-3 border-b border-line px-4 py-2">
        <h3 className="eyebrow">Files</h3>
        <span className="font-mono text-[10.5px] text-ink-faint">
          {version.files.length} {version.files.length === 1 ? "attachment" : "attachments"} · v{version.number}
        </span>
      </div>
      {version.files.length === 0 ? (
        <div className="px-4 py-2.5 text-xs text-ink-muted">This version has no files.</div>
      ) : (
        <ul className="divide-y divide-paper-dark">
          {version.files.map((file) => (
            <li key={file.id} className="flex items-center gap-2.5 px-4 py-2 text-xs">
              <FileIcon file={file} />
              <span className="min-w-0 flex-1 truncate font-mono text-[11.5px]">{file.name}</span>
              <span className={`shrink-0 ${file.status === "Extracted" ? "text-success-ink" : file.stored ? "text-ink-muted" : "font-semibold text-warn-ink"}`}>
                {file.kind === "CodeArchive" && file.status === "Stored" ? "Stored · not extracted" : fileStatus[file.status]}
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function location(item: { file?: string | null; startLine?: number | null; endLine?: number | null }) {
  const name = item.file?.slice(item.file.lastIndexOf("/") + 1);
  const lines = item.startLine == null ? null : item.endLine && item.endLine !== item.startLine ? `${item.startLine}–${item.endLine}` : `${item.startLine}`;
  return { text: name ? (lines ? `${name}:${lines}` : name) : "Whole submission", canShow: item.file != null && item.startLine != null };
}

export function MistakeList({ mistakes, onShow }: { mistakes: CodeMistake[]; onShow: (file: string, line: number) => void }) {
  if (mistakes.length === 0) {
    return (
      <div className="flex items-center gap-2 rounded-[10px] border border-success/25 bg-success-tint/40 px-4 py-3 text-xs text-success-ink">
        <CircleCheck className="h-4 w-4" /> No mistakes found.
      </div>
    );
  }

  return (
    <section>
      <h3 className="eyebrow mb-2">Mistakes · {mistakes.length}</h3>
      <ul className="flex flex-col gap-1.5">
        {mistakes.map((m, i) => {
          const where = location(m);
          return (
            <li key={i}>
              <button
                onClick={where.canShow ? () => onShow(m.file!, m.startLine!) : undefined}
                disabled={!where.canShow}
                className="group flex w-full gap-3 rounded-lg border border-line border-l-[3px] bg-white px-3.5 py-2.5 text-left shadow-card transition-all enabled:hover:-translate-y-px enabled:hover:shadow-raised disabled:cursor-default"
                style={{ borderLeftColor: severityColor[m.severity] }}
                title={where.canShow ? "Show in the code" : undefined}
              >
                <span className="min-w-0 flex-1">
                  <span className="flex flex-wrap items-center gap-x-2 text-xs">
                    <b style={{ color: severityColor[m.severity] }}>{m.severity}</b>
                    <span className="text-ink-muted">{m.category}</span>
                    {m.points != null && m.points > 0 && (
                      <b className="rounded bg-danger-tint px-1 font-mono text-[11px] text-danger-ink">−{points(m.points)}</b>
                    )}
                    <span className={`ml-auto font-mono text-[11px] ${where.canShow ? "text-accent group-hover:underline" : "text-ink-faint"}`}>{where.text}</span>
                  </span>
                  <span className="mt-1 block text-[13px] leading-snug">{m.message}</span>
                </span>
              </button>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

export function StrengthList({ strengths, onShow }: { strengths: CodeStrength[]; onShow: (file: string, line: number) => void }) {
  if (strengths.length === 0) {
    return null;
  }

  return (
    <section>
      <h3 className="eyebrow mb-2">Strengths · {strengths.length}</h3>
      <ul className="flex flex-col gap-1.5">
        {strengths.map((s, i) => {
          const where = location(s);
          return (
            <li key={i}>
              <button
                onClick={where.canShow ? () => onShow(s.file!, s.startLine!) : undefined}
                disabled={!where.canShow}
                className="group flex w-full gap-3 rounded-lg border border-line border-l-[3px] bg-white px-3.5 py-2.5 text-left shadow-card transition-all enabled:hover:-translate-y-px enabled:hover:shadow-raised disabled:cursor-default"
                style={{ borderLeftColor: strengthColor }}
                title={where.canShow ? "Show in the code" : undefined}
              >
                <span className="min-w-0 flex-1">
                  <span className="flex flex-wrap items-center gap-x-2 text-xs">
                    <b className="flex items-center gap-1 text-success-ink">
                      <ThumbsUp className="h-3 w-3" /> Strength
                    </b>
                    <span className="text-ink-muted">{s.category}</span>
                    <span className={`ml-auto font-mono text-[11px] ${where.canShow ? "text-accent group-hover:underline" : "text-ink-faint"}`}>{where.text}</span>
                  </span>
                  <span className="mt-1 block text-[13px] leading-snug">{s.message}</span>
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
