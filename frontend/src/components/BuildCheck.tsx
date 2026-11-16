import type { ReactNode } from "react";
import { CircleAlert, CircleCheck, CircleSlash, Clock, Hammer } from "lucide-react";
import type { components } from "../api/schema";
import { Chip, type ChipTone } from "./ui";

type BuildStatus = components["schemas"]["BuildStatus"];

const shown: Record<BuildStatus, { tone: ChipTone; label: string; icon: ReactNode; detail: string }> = {
  Compiled: { tone: "success", label: "Compiled", icon: <CircleCheck />, detail: "compiled with JDK 27 before grading, no errors" },
  CompileFailed: { tone: "danger", label: "Did not compile", icon: <CircleAlert />, detail: "javac (JDK 27) reported errors; the grader got them as facts" },
  TimedOut: { tone: "warn", label: "Compile timed out", icon: <Clock />, detail: "compiling took longer than 30 s and was stopped" },
  Skipped: { tone: "muted", label: "Nothing to compile", icon: <CircleSlash />, detail: "the submission has no Java files" },
  Unavailable: { tone: "warn", label: "Not compiled", icon: <CircleSlash />, detail: "the compile check could not run" },
};

/**
 * What the compile check before grading found, from the grading's buildStatus and buildOutput. The compiler's messages (or
 * why the check did not run) open below. Gradings made before the check existed have no status.
 */
export function BuildCheck({ status, output }: { status?: BuildStatus | null; output?: string | null }) {
  if (!status) {
    return (
      <div className="flex items-center gap-2 text-[11px] text-ink-muted">
        <Chip tone="muted" icon={<Hammer />}>
          Not compiled
        </Chip>
        <span>graded before the compile check existed: read from the code only</span>
      </div>
    );
  }

  const { tone, label, icon, detail } = shown[status];
  const text = output?.trim() ?? "";
  const warnings = status === "Compiled" && text.length > 0;
  const summary = (
    <span className="flex flex-wrap items-center gap-2">
      <Chip tone={warnings ? "warn" : tone} icon={icon}>
        {warnings ? "Compiled with warnings" : label}
      </Chip>
      <span>{warnings ? "compiled with JDK 27 before grading; javac printed warnings" : detail}</span>
      <span className="text-ink-faint">· the program was not run</span>
    </span>
  );

  if (text.length === 0) {
    return <div className="text-[11px] text-ink-muted">{summary}</div>;
  }

  return (
    <details className="text-[11px] text-ink-muted" open={status === "CompileFailed"}>
      <summary className="cursor-pointer list-none [&::-webkit-details-marker]:hidden">
        {summary}
      </summary>
      <pre className="mt-1.5 max-h-60 overflow-auto whitespace-pre-wrap rounded-md border border-line bg-paper px-3 py-2 font-mono text-[11px] leading-relaxed text-ink-soft">
        {text}
      </pre>
    </details>
  );
}
