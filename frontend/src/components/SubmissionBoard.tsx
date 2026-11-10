import { CircleCheck, CircleDashed, GitCompareArrows } from "lucide-react";
import type { AssignmentGrading, StoredSubmission, StoredVersion, Submission } from "../api/client";
import { formatDay, initials } from "../format";
import { Score, SeverityDots } from "./bits";
import { hasWork } from "./SubmissionDetail";
import { Avatar, Chip } from "./ui";

type Stage = "missing" | "todo" | "draft" | "review" | "approved";

const stages: { key: Stage; title: string; hint: string; dot: string }[] = [
  { key: "missing", title: "Not submitted", hint: "Nothing turned in", dot: "bg-ink-faint" },
  { key: "todo", title: "To grade", hint: "Turned in, no AI draft yet", dot: "bg-accent" },
  { key: "draft", title: "AI draft", hint: "Waiting for your review", dot: "bg-mark" },
  { key: "review", title: "In review", hint: "Your report is saved, not approved", dot: "bg-warn" },
  { key: "approved", title: "Approved", hint: "Your report is final", dot: "bg-success" },
];

// The assignment as a board: every student is a card in the column of where their work stands.
// It only groups what the list shows; clicking a card opens that student in the list view.
export function SubmissionBoard({
  submissions,
  versionOf,
  reviews,
  storedById,
  gradingVersionIds,
  onOpen,
}: {
  submissions: Submission[];
  versionOf: Map<string, StoredVersion | undefined>;
  reviews: Map<string, AssignmentGrading>;
  storedById: Map<string, StoredSubmission>;
  gradingVersionIds: Map<string, string>;
  onOpen: (submission: Submission) => void;
}) {
  const reviewOf = (submission: Submission) => {
    const version = versionOf.get(submission.id);
    return version ? reviews.get(version.id) : undefined;
  };

  function stageOf(submission: Submission): Stage {
    if (!hasWork(submission)) {
      return "missing";
    }
    const review = reviewOf(submission);
    if (review?.reportStatus === "Approved") {
      return "approved";
    }
    if (review?.reportStatus === "InReview") {
      return "review";
    }
    return review ? "draft" : "todo";
  }

  const byStage = new Map<Stage, Submission[]>(stages.map((stage) => [stage.key, []]));
  for (const submission of [...submissions].sort((a, b) => a.studentName.localeCompare(b.studentName))) {
    byStage.get(stageOf(submission))!.push(submission);
  }

  return (
    <div className="flex min-h-[420px] shrink-0 gap-3 overflow-x-auto pb-1 lg:min-h-0 lg:flex-1 lg:shrink">
      {stages.map((stage, column) => {
        const cards = byStage.get(stage.key)!;
        return (
          <section
            key={stage.key}
            className="flex w-[240px] min-w-[220px] flex-1 animate-rise flex-col rounded-[10px] border border-line bg-paper-dark/60"
            style={{ animationDelay: `${column * 60}ms` }}
            aria-label={stage.title}
          >
            <header className="flex items-center gap-2 px-3 pb-2 pt-2.5" title={stage.hint}>
              <span className={`h-2 w-2 rounded-full ${stage.dot}`} />
              <h3 className="eyebrow text-ink">{stage.title}</h3>
              <span className="ml-auto rounded-full bg-white px-1.5 font-mono text-[10.5px] font-semibold leading-[18px] text-ink-muted shadow-card">{cards.length}</span>
            </header>
            <ul className="flex min-h-0 flex-1 flex-col gap-2 overflow-y-auto px-2 pb-2">
              {cards.length === 0 && <li className="rounded-lg border border-dashed border-ink-faint/40 px-3 py-4 text-center text-[11px] text-ink-faint">{stage.hint}</li>}
              {cards.map((submission) => {
                const version = versionOf.get(submission.id);
                const review = reviewOf(submission);
                const grading = version ? gradingVersionIds.get(version.id) : undefined;
                return (
                  <li key={submission.id}>
                    <button
                      onClick={() => onOpen(submission)}
                      className="flex w-full flex-col gap-2 rounded-lg border border-line bg-white px-3 py-2.5 text-left shadow-card transition-all hover:-translate-y-0.5 hover:border-ink-faint hover:shadow-raised"
                    >
                      <span className="flex items-center gap-2">
                        <span className={stage.key === "missing" ? "opacity-60 grayscale" : ""}>
                          <Avatar text={initials(submission.studentName)} userId={submission.studentId} size="sm" />
                        </span>
                        <span className={`min-w-0 flex-1 truncate font-semibold ${stage.key === "missing" ? "text-ink-muted" : ""}`}>{submission.studentName}</span>
                        {stage.key === "approved" && <CircleCheck className="h-4 w-4 shrink-0 text-success" />}
                      </span>
                      {stage.key !== "missing" && (
                        <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
                          {review ? (
                            <>
                              <Score score={review.score} max={review.maxScore} bonus={review.bonus} />
                              <SeverityDots major={review.major} minor={review.minor} style={review.style} empty="no mistakes" />
                            </>
                          ) : storedById.has(submission.id) ? (
                            <span className="text-[11px] text-ink-muted">Stored · ready to grade</span>
                          ) : (
                            <span className="flex items-center gap-1 text-[11px] text-ink-faint">
                              <CircleDashed className="h-3 w-3" /> Not fetched
                            </span>
                          )}
                          <span className="ml-auto flex items-center gap-1.5">
                            {grading && (
                              <Chip tone="info" icon={<span className="h-2.5 w-2.5 animate-spin rounded-full border-[1.5px] border-accent/30 border-t-accent" />}>
                                {grading === "Queued" ? "Queued" : "Grading"}
                              </Chip>
                            )}
                            {submission.late && <Chip tone="warn">Late</Chip>}
                            {version && version.number > 1 && (
                              <Chip tone="outline" icon={<GitCompareArrows />}>
                                v{version.number}
                              </Chip>
                            )}
                          </span>
                        </span>
                      )}
                      <span className="font-mono text-[10.5px] text-ink-faint">
                        {stage.key === "missing" ? "nothing turned in" : `turned in ${formatDay(submission.turnedInAt)}`}
                      </span>
                    </button>
                  </li>
                );
              })}
            </ul>
          </section>
        );
      })}
    </div>
  );
}
