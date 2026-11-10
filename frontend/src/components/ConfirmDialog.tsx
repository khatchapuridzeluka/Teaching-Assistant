import { type ReactNode, useEffect, useRef } from "react";
import { Sparkles, TriangleAlert } from "lucide-react";
import { Button } from "./ui";

export function ConfirmDialog({
  title,
  children,
  confirmLabel,
  danger,
  onConfirm,
  onCancel,
}: {
  title: string;
  children: ReactNode;
  confirmLabel: string;
  danger?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const confirmRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    confirmRef.current?.focus();
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        onCancel();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onCancel]);

  return (
    <div className="modal-overlay fixed inset-0 z-[55] flex items-center justify-center p-4" onClick={(e) => e.target === e.currentTarget && onCancel()}>
      <div role="alertdialog" aria-modal="true" aria-labelledby="confirm-title" className="modal-panel w-full max-w-md overflow-hidden rounded-xl border border-line bg-white">
        <div className="flex gap-3.5 px-5 pb-4 pt-5">
          <span
            className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-full ${danger ? "bg-danger-tint text-danger" : "bg-mark-tint text-warn-ink"}`}
            aria-hidden
          >
            {danger ? <TriangleAlert className="h-5 w-5" /> : <Sparkles className="h-5 w-5" />}
          </span>
          <div className="min-w-0">
            <h2 id="confirm-title" className="font-display text-[19px] font-semibold leading-tight">
              {title}
            </h2>
            <div className="mt-1.5 text-[13px] leading-relaxed text-ink-muted">{children}</div>
          </div>
        </div>
        <div className="flex justify-end gap-2 border-t border-line bg-paper px-5 py-3">
          <Button onClick={onCancel}>Cancel</Button>
          <Button ref={confirmRef} variant={danger ? "danger" : "primary"} onClick={onConfirm}>
            {confirmLabel}
          </Button>
        </div>
      </div>
    </div>
  );
}
