import { useState } from "react";
import { useDiscardGrading } from "../api/hooks";
import { ConfirmDialog } from "./ConfirmDialog";
import { Button } from "./ui";
import { Trash2 } from "lucide-react";

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
  const [confirming, setConfirming] = useState(false);
  const approved = reportStatus === "Approved";

  function onClick() {
    if (reportStatus) {
      setConfirming(true);
    } else {
      discard.mutate(gradingId);
    }
  }

  return (
    <>
      <Button small variant="ghost" icon={<Trash2 />} onClick={onClick} disabled={discard.isPending} title={title}>
        {discard.isPending ? "Discarding…" : "Discard"}
      </Button>
      {confirming && (
        <ConfirmDialog
          title={approved ? "Discard this approved draft?" : "Discard this draft and your report?"}
          confirmLabel="Discard"
          danger
          onConfirm={() => {
            setConfirming(false);
            discard.mutate(gradingId);
          }}
          onCancel={() => setConfirming(false)}
        >
          <p>
            {approved
              ? "Your approved report stops counting: its mistakes and strengths leave the student's profile, analyses and records."
              : "Your report in review is set aside with it."}{" "}
            The draft and the report stay in the history; the previous draft, if there is one, becomes the current one.
          </p>
        </ConfirmDialog>
      )}
    </>
  );
}
