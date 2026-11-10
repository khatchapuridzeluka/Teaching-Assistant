import { CalendarClock } from "lucide-react";
import { formatDate, formatDay, formatPoints } from "../format";
import { severityColor } from "../theme";
import { Chip } from "./ui";

// A score as the app writes it everywhere: 94/100 +5, in the monospace face.
export function Score({
  score,
  max,
  bonus,
  className = "",
}: {
  score: number | null | undefined;
  max: number | null | undefined;
  bonus?: number | null;
  className?: string;
}) {
  if (score == null) {
    return <span className={`text-xs text-ink-muted ${className}`}>score in text</span>;
  }

  return (
    <span className={`whitespace-nowrap font-mono text-[12px] ${className}`}>
      <b className="font-semibold text-ink">{formatPoints(score)}</b>
      <span className="text-ink-faint">/{max ?? "–"}</span>
      {bonus ? <span className="ml-1 font-semibold text-success"> +{formatPoints(bonus)}</span> : null}
    </span>
  );
}

export function SeverityDots({ major, minor, style, empty = null }: { major: number; minor: number; style: number; empty?: string | null }) {
  const parts = [
    { key: "Major" as const, count: major },
    { key: "Minor" as const, count: minor },
    { key: "Style" as const, count: style },
  ].filter((part) => part.count > 0);

  if (parts.length === 0) {
    return empty ? <span className="text-[11px] text-ink-muted">{empty}</span> : null;
  }

  return (
    <span className="flex items-center gap-2 font-mono text-[11px] text-ink-muted" title={parts.map((part) => `${part.count} ${part.key.toLowerCase()}`).join(", ")}>
      {parts.map((part) => (
        <span key={part.key} className="flex items-center gap-1">
          <span className="h-1.5 w-1.5 rounded-full" style={{ backgroundColor: severityColor[part.key] }} />
          {part.count}
        </span>
      ))}
    </span>
  );
}

// When something is due, and how far that is from now.
export function DueChip({ dueAt }: { dueAt: string | null | undefined }) {
  if (!dueAt) {
    return <Chip tone="muted">No deadline</Chip>;
  }

  const days = Math.round((new Date(dueAt).getTime() - Date.now()) / 86_400_000);
  const hours = (new Date(dueAt).getTime() - Date.now()) / 3_600_000;

  if (hours < 0) {
    return (
      <Chip tone="muted" icon={<CalendarClock />}>
        <span title={formatDate(dueAt)}>Closed {formatDay(dueAt)}</span>
      </Chip>
    );
  }

  return (
    <Chip tone={hours < 48 ? "mark" : "info"} icon={<CalendarClock />}>
      <span title={formatDate(dueAt)}>{hours < 24 ? "Due today" : `Due in ${days} d`} · {formatDay(dueAt)}</span>
    </Chip>
  );
}

// Graded / waiting / missing as one bar: what Classroom knows about an assignment.
export function ProgressSplit({ graded, waiting, missing, total }: { graded: number; waiting: number; missing: number; total: number }) {
  const all = Math.max(total, graded + waiting + missing, 1);
  const part = (count: number) => `${(count / all) * 100}%`;

  return (
    <span
      className="flex h-1.5 w-full overflow-hidden rounded-full bg-paper-dark"
      title={`${graded} graded · ${waiting} waiting for a grade · ${missing} missing`}
      aria-hidden
    >
      <span className="bar-fill h-full bg-success" style={{ width: part(graded) }} />
      <span className="bar-fill h-full bg-accent" style={{ width: part(waiting) }} />
      <span className="bar-fill h-full bg-danger/70" style={{ width: part(missing) }} />
    </span>
  );
}
