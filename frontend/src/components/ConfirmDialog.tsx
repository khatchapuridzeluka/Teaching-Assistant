import { type ReactNode, useEffect, useRef } from "react";
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
    <div
      className="fixed inset-0 z-[55] flex items-center justify-center bg-ink/40 p-4"
      onClick={(e) => e.target === e.currentTarget && onCancel()}
    >
      <div role="alertdialog" aria-modal="true" aria-labelledby="confirm-title" className="toast-enter w-full max-w-md rounded-[12px] bg-white shadow-xl">
        <div className={`h-1.5 rounded-t-[12px] ${danger ? "bg-[#8f2d2d]" : "bg-accent"}`} />
        <div className="px-5 pb-4 pt-4">
          <h2 id="confirm-title" className="text-[15px] font-bold">
            {title}
          </h2>
          <div className="mt-1.5 text-[13px] leading-relaxed text-ink-muted">{children}</div>
        </div>
        <div className="flex justify-end gap-2 border-t border-line px-5 py-3">
          <Button onClick={onCancel}>Cancel</Button>
          <button
            ref={confirmRef}
            onClick={onConfirm}
            className={`inline-flex h-[34px] items-center rounded-lg border px-3.5 font-semibold text-white ${
              danger ? "border-[#8f2d2d] bg-[#8f2d2d] hover:bg-[#7a2626]" : "border-accent bg-accent hover:bg-accent/90"
            }`}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
