import { useEffect } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import type { StoredSubmission } from "../api/client";
import { useReview } from "../api/hooks";
import { CodeBrowser } from "./CodeBrowser";
import { Button, Chip, ErrorPanel, Spinner } from "./ui";

/**
 * The model's draft review of one version next to its code, so every "File.java:28" in the review can be checked
 * against the line itself. The review comes from this browser session's cache (useReview), so the panel can be closed
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
                  <FileList title="Sent to the model" items={review.sentFiles} />
                  {review.skippedFiles.length > 0 && <FileList title="Not sent" items={review.skippedFiles} />}
                </div>
              )}
            </div>
          </section>

          <section className="flex h-[640px] shrink-0 flex-col px-6 py-5 lg:h-auto lg:min-w-0 lg:flex-1">
            <h3 className="mb-2 text-[11px] font-bold uppercase tracking-[0.06em] text-accent">Code</h3>
            <div className="min-h-0 flex-1">
              <CodeBrowser versionId={versionId} className="h-full" />
            </div>
          </section>
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
