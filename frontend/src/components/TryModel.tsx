import { useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { FlaskConical, X } from "lucide-react";
import type { GradingResult } from "../api/client";
import { useGradingModels, useTryModel } from "../api/hooks";
import { BuildCheck } from "./BuildCheck";
import { Button, Chip, ErrorPanel, Modal, Spinner } from "./ui";

/** Picks a model and grades the version once with it; the answer is handed back, never saved. */
export function TryModelDialog({
  versionId,
  versionNumber,
  onTried,
  onClose,
}: {
  versionId: string;
  versionNumber: number;
  onTried: (result: GradingResult) => void;
  onClose: () => void;
}) {
  const models = useGradingModels().data;
  const tryModel = useTryModel();
  const [picked, setPicked] = useState<string>();
  const model = picked ?? models?.available.find((m) => m !== models.current) ?? models?.current;

  return (
    <Modal
      title="Try with model"
      subtitle={`Grades v${versionNumber} once with another model, exactly like a draft`}
      onClose={onClose}
      busy={tryModel.isPending}
      size="max-w-lg"
      footer={
        <>
          <Button onClick={onClose} disabled={tryModel.isPending}>
            Cancel
          </Button>
          <Button
            variant="primary"
            icon={<FlaskConical />}
            disabled={!model || tryModel.isPending}
            onClick={() => {
              if (model) {
                tryModel.mutate({ versionId, model }, { onSuccess: (result) => onTried(result) });
              }
            }}
          >
            {tryModel.isPending ? "Grading…" : "Try"}
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-2 px-5 py-4">
        {!models && <Spinner label="Loading models…" />}
        {models?.available.map((m) => (
          <label
            key={m}
            className={`flex cursor-pointer items-center gap-3 rounded-lg border px-3 py-2 ${m === model ? "border-accent bg-accent-tint" : "border-line hover:bg-paper"}`}
          >
            <input type="radio" name="try-model" checked={m === model} onChange={() => setPicked(m)} disabled={tryModel.isPending} />
            <span className="font-mono text-[13px]">{m}</span>
            {m === models.current && <Chip tone="info">grades drafts</Chip>}
          </label>
        ))}
        <p className="text-xs text-ink-muted">
          The same instructions, rubric, policy and compile check as a real grading. The answer is shown next to the code and is not saved: no draft,
          report or student profile changes, and closing it throws it away. One paid AI call; it can take a minute.
        </p>
        {tryModel.isPending && <Spinner label="Grading… keep this open until the answer arrives." />}
        {tryModel.isError && <ErrorPanel error={tryModel.error} />}
      </div>
    </Modal>
  );
}

/** The header and review of a tried grading, shown in place of the draft until it is closed. */
export function TriedGrading({ tried, draftScore, onClose }: { tried: GradingResult; draftScore: number | null | undefined; onClose: () => void }) {
  const diff = tried.score != null && draftScore != null ? tried.score - draftScore : null;

  return (
    <div className="surface overflow-hidden">
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2 border-b border-l-4 border-line border-l-accent bg-accent-tint/40 px-4 py-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="eyebrow flex items-center gap-1.5 text-ink">
              <FlaskConical className="h-3.5 w-3.5 text-accent" />
              Tried with {tried.model}
            </h3>
            <Chip tone="muted">Not saved</Chip>
          </div>
          {tried.score != null ? (
            <div className="mt-1.5 flex flex-wrap items-baseline gap-1.5">
              <b className="num text-[32px] leading-none">{round(tried.score)}</b>
              <span className="num text-[16px] font-medium text-ink-muted">/ {tried.maxScore ?? "–"}</span>
              {tried.bonus ? (
                <span className="ml-1 rounded bg-success-tint px-1.5 py-0.5 font-mono text-[11px] font-semibold text-success-ink">
                  +{round(tried.bonus)} bonus
                </span>
              ) : null}
              {diff != null && (
                <span className="ml-2 text-xs text-ink-muted">
                  current draft {round(draftScore!)} · {diff === 0 ? "same" : `${diff > 0 ? "+" : "−"}${round(Math.abs(diff))}`}
                </span>
              )}
            </div>
          ) : (
            <div className="mt-1 text-xs text-ink-muted">The score is in the review text.</div>
          )}
        </div>
        <Button small icon={<X />} className="ml-auto" onClick={onClose} title="Throw this try away and show the draft again">
          Close try
        </Button>
      </div>
      <div className="markdown px-5 py-4">
        <ReactMarkdown remarkPlugins={[remarkGfm]}>{tried.review}</ReactMarkdown>
      </div>
      <div className="border-t border-paper-dark px-5 py-2.5">
        <BuildCheck
          status={tried.buildStatus}
          output={tried.buildOutput}
          styleFindings={tried.styleFindings}
          styleOutput={tried.styleOutput}
          pmdFindings={tried.pmdFindings}
          pmdOutput={tried.pmdOutput}
        />
      </div>
    </div>
  );
}

function round(value: number): string {
  return String(Math.round(value * 10) / 10);
}
