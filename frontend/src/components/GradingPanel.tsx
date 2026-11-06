import { useEffect } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import type { GradingResult, StoredSubmission } from "../api/client";
import { Button, Chip, ErrorPanel, Spinner } from "./ui";

/**
 * The model's draft review of one submission: shown while it is being written, then as Markdown, with the list of
 * files the model was given and the ones it was not. Display only: the review comes from POST /versions/{id}/grade.
 */
export function GradingPanel({
  submission,
  versionNumber,
  pending,
  error,
  result,
  onClose,
}: {
  submission: StoredSubmission;
  versionNumber: number;
  pending: boolean;
  error: unknown;
  result: GradingResult | undefined;
  onClose: () => void;
}) {
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
    <div className="fixed inset-0 z-40 flex justify-end bg-ink/40" onClick={onClose}>
      <aside className="flex h-full w-full max-w-[920px] flex-col bg-paper shadow-xl" onClick={(e) => e.stopPropagation()}>
        <header className="flex items-center justify-between gap-4 border-b border-line bg-white px-6 py-4">
          <div className="min-w-0">
            <div className="truncate text-[15px] font-bold">{submission.studentName}</div>
            <div className="text-xs text-ink-muted">
              AI review of v{versionNumber}
              {result && ` · ${result.model}`} · read from the code only, nothing was compiled or run
            </div>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            <Chip tone="dashed">Draft · not saved</Chip>
            <Button small onClick={onClose}>Close</Button>
          </div>
        </header>

        <div className="flex-1 overflow-y-auto px-6 py-5">
          {pending && <Spinner label="Grading… the model reads the instructions and the code; this can take a minute." />}
          {!pending && error != null && <ErrorPanel error={error} />}
          {!pending && result && (
            <div className="flex flex-col gap-5">
              <div className="markdown rounded-[10px] border border-line bg-white px-5 py-4">
                <ReactMarkdown remarkPlugins={[remarkGfm]}>{result.review}</ReactMarkdown>
              </div>
              <FileList title="Sent to the model" items={result.sentFiles} />
              {result.skippedFiles.length > 0 && <FileList title="Not sent" items={result.skippedFiles} />}
            </div>
          )}
        </div>
      </aside>
    </div>
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
