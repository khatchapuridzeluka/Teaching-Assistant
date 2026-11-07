import { useEffect, useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import type { CodeMistake, StoredSubmission } from "../api/client";
import { useReview } from "../api/hooks";
import { CodeBrowser, type CodeFocus, severityColor } from "./CodeBrowser";
import { Button, Chip, ErrorPanel, Spinner } from "./ui";

/**
 * The model's draft review of one version next to its code. Mistakes are listed under the review and marked on their
 * lines in the code; clicking one in the list shows its line. The review comes from this browser session's cache (useReview), so the panel can be closed
 * and opened again without grading again. Display only: grading itself is POST /versions/{id}/grade.
 */
export function GradingPanel({
  submission,
  versionId,
  versionNumber,
  pending,
  error,
  onRegrade,
  regradeDisabled,
  onClose,
}: {
  submission: StoredSubmission;
  versionId: string;
  versionNumber: number;
  pending: boolean;
  error: unknown;
  onRegrade: () => void;
  regradeDisabled: boolean;
  onClose: () => void;
}) {
  const review = useReview(versionId).data;
  const [focus, setFocus] = useState<CodeFocus>();

  // Escape closes the panel.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        onClose();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  return (
    // Full screen: the review and the code get the whole window. Close or Escape returns to the table.
    <div className="fixed inset-0 z-40 flex">
      <aside className="flex h-full w-full flex-col bg-paper">
        <header className="flex items-center justify-between gap-4 border-b border-line bg-white px-6 py-4">
          <div className="min-w-0">
            <div className="truncate text-[15px] font-bold">{submission.studentName}</div>
            <div className="text-xs text-ink-muted">
              AI review of v{versionNumber}
              {review && ` · ${review.model} · instructions v${review.instructionsVersion}`} · read from the code only, nothing
              was compiled or run
            </div>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            <Chip tone="dashed">Draft · kept until you refresh the page</Chip>
            <Button small onClick={onRegrade} disabled={regradeDisabled} title="Send the version to the AI again for a new review">
              {pending ? "Grading…" : review ? "Grade again" : "Grade"}
            </Button>
            <Button small onClick={onClose}>Close</Button>
          </div>
        </header>

        {/* Side by side on wide screens; stacked (review first) on narrow ones. */}
        <div className="flex min-h-0 flex-1 flex-col overflow-y-auto lg:flex-row lg:overflow-hidden">
          <section className="px-6 py-5 lg:w-[44%] lg:shrink-0 lg:overflow-y-auto lg:border-r lg:border-line">
            <div className="flex flex-col gap-5">
              {pending && <Spinner label="Grading… the model reads the instructions and the code; this can take a minute." />}
              {!pending && error != null && <ErrorPanel error={error} />}
              {review && (
                <div className={`flex flex-col gap-5 ${pending ? "opacity-50" : ""}`}>
                  <div className="markdown rounded-[10px] border border-line bg-white px-5 py-4">
                    <ReactMarkdown remarkPlugins={[remarkGfm]}>{review.review}</ReactMarkdown>
                  </div>
                  <MistakeList
                    mistakes={review.mistakes}
                    onShow={(file, line) => setFocus({ file, line, key: Date.now() })}
                  />
                  <FileList title="Sent to the model" items={review.sentFiles} />
                  {review.skippedFiles.length > 0 && <FileList title="Not sent" items={review.skippedFiles} />}
                </div>
              )}
            </div>
          </section>

          <section className="flex h-[640px] shrink-0 flex-col px-6 py-5 lg:h-auto lg:min-w-0 lg:flex-1">
            <h3 className="mb-2 text-[11px] font-bold uppercase tracking-[0.06em] text-accent">Code</h3>
            <div className="min-h-0 flex-1">
              <CodeBrowser versionId={versionId} className="h-full" mistakes={review?.mistakes} focus={focus} />
            </div>
          </section>
        </div>
      </aside>
    </div>
  );
}

/** The mistakes in the order the model found them; one with lines opens them in the code. */
function MistakeList({ mistakes, onShow }: { mistakes: CodeMistake[]; onShow: (file: string, line: number) => void }) {
  if (mistakes.length === 0) {
    return (
      <section className="rounded-[10px] border border-line bg-white px-5 py-3 text-xs text-ink-muted">
        No mistakes found.
      </section>
    );
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

function FileList({ title, items }: { title: string; items: string[] }) {
  return (
    <section>
      <h3 className="mb-2 text-[11px] font-bold uppercase tracking-[0.06em] text-accent">{title}</h3>
      <ul className="flex flex-col gap-1 text-xs text-ink-muted">
        {items.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>
    </section>
  );
}
