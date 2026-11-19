import { useState } from "react";
import { Trash2 } from "lucide-react";
import type { DiscardReason } from "../api/client";
import { useDiscardGrading } from "../api/hooks";
import { Button, ErrorPanel, Modal } from "./ui";

const noteLimit = 500;

/** The reasons a teacher can pick; Superseded is only set automatically when another draft is approved. */
export const discardReasons: { value: Exclude<DiscardReason, "Superseded">; label: string; hint: string }[] = [
  { value: "PoorGrading", label: "Poor grading", hint: "Wrong or poor against the instructions and your rules. Counts against the AI on the Evaluation page." },
  { value: "WrongSubmission", label: "Wrong version or files", hint: "The wrong version or files were graded." },
  { value: "NotNeeded", label: "Not needed", hint: "A test, or graded again for another reason." },
  { value: "Other", label: "Other", hint: "Say why in the note." },
];

export const discardReasonLabel: Record<DiscardReason, string> = {
  PoorGrading: "Poor grading",
  WrongSubmission: "Wrong version or files",
  NotNeeded: "Not needed",
  Superseded: "Another draft approved",
  Other: "Other",
};

export function DiscardDraftButton({
  gradingId,
  reportStatus,
  title,
}: {
  gradingId: string;
  reportStatus: string | null | undefined;
  title: string;
}) {
  const discard = useDiscardGrading();
  const [open, setOpen] = useState(false);

  return (
    <>
      <Button small variant="ghost" icon={<Trash2 />} onClick={() => setOpen(true)} disabled={discard.isPending} title={title}>
        {discard.isPending ? "Discarding…" : "Discard"}
      </Button>
      {open && <DiscardDialog gradingId={gradingId} reportStatus={reportStatus} onClose={() => setOpen(false)} />}
    </>
  );
}

function DiscardDialog({
  gradingId,
  reportStatus,
  onClose,
}: {
  gradingId: string;
  reportStatus: string | null | undefined;
  onClose: () => void;
}) {
  const discard = useDiscardGrading();
  const [reason, setReason] = useState<DiscardReason | null>(null);
  const [note, setNote] = useState("");
  const approved = reportStatus === "Approved";

  return (
    <Modal
      title="Discard this draft"
      subtitle="Why? The reason is kept with the draft and shown on the Evaluation page"
      onClose={onClose}
      busy={discard.isPending}
      size="max-w-lg"
      footer={
        <>
          <Button onClick={onClose} disabled={discard.isPending}>
            Cancel
          </Button>
          <Button
            variant="danger"
            disabled={reason == null || discard.isPending}
            onClick={() => {
              if (reason == null) {
                return;
              }
              discard.mutate({ gradingId, reason, note: note.trim() || undefined }, { onSuccess: onClose });
            }}
          >
            {discard.isPending ? "Discarding…" : "Discard"}
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-3 px-5 py-4">
        <div role="radiogroup" aria-label="Reason" className="flex flex-col gap-1.5">
          {discardReasons.map((option) => (
            <label
              key={option.value}
              className={`flex cursor-pointer gap-3 rounded-lg border px-3 py-2 ${reason === option.value ? "border-accent bg-accent-tint" : "border-line hover:bg-paper"}`}
            >
              <input
                type="radio"
                name="discard-reason"
                className="mt-1"
                checked={reason === option.value}
                onChange={() => setReason(option.value)}
              />
              <span>
                <span className="block text-sm font-semibold">{option.label}</span>
                <span className="block text-xs text-ink-muted">{option.hint}</span>
              </span>
            </label>
          ))}
        </div>
        <label className="flex flex-col gap-1">
          <span className="eyebrow">Note (optional)</span>
          <textarea
            value={note}
            onChange={(e) => setNote(e.target.value)}
            maxLength={noteLimit}
            rows={3}
            placeholder="For example: deducted for print statements, which my rules say are free"
            className="field resize-y py-2 leading-relaxed"
          />
        </label>
        {reportStatus && (
          <p className="text-xs text-ink-muted">
            {approved
              ? "Your approved report stops counting: its mistakes and strengths leave the student's profile, analyses and records."
              : "Your report in review is set aside with it."}{" "}
            The draft and the report stay in the history; the previous draft, if there is one, becomes the current one.
          </p>
        )}
        {discard.isError && <ErrorPanel error={discard.error} />}
      </div>
    </Modal>
  );
}
