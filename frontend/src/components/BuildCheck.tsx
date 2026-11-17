import type { ReactNode } from "react";
import { CircleAlert, CircleCheck, CircleSlash, Clock, Hammer, ListChecks } from "lucide-react";
import type { components } from "../api/schema";
import { Chip, type ChipTone } from "./ui";

type BuildStatus = components["schemas"]["BuildStatus"];

const shown: Record<BuildStatus, { tone: ChipTone; label: string; icon: ReactNode; detail: string }> = {
  Compiled: { tone: "success", label: "Compiled", icon: <CircleCheck />, detail: "compiled with JDK 27 before grading, no errors" },
  CompileFailed: { tone: "danger", label: "Did not compile", icon: <CircleAlert />, detail: "javac (JDK 27) reported errors; the grader got them as facts" },
  TimedOut: { tone: "warn", label: "Compile timed out", icon: <Clock />, detail: "compiling took too long and was stopped" },
  Skipped: { tone: "muted", label: "Nothing to compile", icon: <CircleSlash />, detail: "the submission has no Java files" },
  Unavailable: { tone: "warn", label: "Not compiled", icon: <CircleSlash />, detail: "the compile check could not run" },
};

/**
 * What the checks before grading found, from the grading's build and Checkstyle fields: the compile result, then
 * Checkstyle's findings. Their messages open below. Gradings made before the checks existed have no status.
 */
export function BuildCheck({
  status,
  output,
  styleFindings,
  styleOutput,
  pmdFindings,
  pmdOutput,
}: {
  status?: BuildStatus | null;
  output?: string | null;
  styleFindings?: number | null;
  styleOutput?: string | null;
  pmdFindings?: number | null;
  pmdOutput?: string | null;
}) {
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
  const ranChecks = status === "Compiled" || status === "CompileFailed" || status === "TimedOut";

  return (
    <div className="flex flex-col gap-1.5">
      <Folded
        open={status === "CompileFailed"}
        text={text}
        summary={
          <>
            <Chip tone={warnings ? "warn" : tone} icon={icon}>
              {warnings ? "Compiled with warnings" : label}
            </Chip>
            <span>{warnings ? "compiled with JDK 27 before grading; javac printed warnings" : detail}</span>
            <span className="text-ink-faint">· the program was not run</span>
          </>
        }
      />
      {ranChecks && (
        <CheckerLine
          name="Checkstyle"
          covers="names, encapsulation, imports, braces, == on strings, empty catch, switch default, boolean logic"
          findings={styleFindings}
          output={styleOutput}
        />
      )}
      {ranChecks && (
        <CheckerLine
          name="PMD"
          covers="unclosed resources, objects compared with ==, unused code, complex or deeply nested code, string += in loops"
          findings={pmdFindings}
          output={pmdOutput}
        />
      )}
    </div>
  );
}

/** One checker's result: clean, N findings (folded below), or not run (older drafts, or it did not finish). */
function CheckerLine({
  name,
  covers,
  findings,
  output,
}: {
  name: string;
  covers: string;
  findings?: number | null;
  output?: string | null;
}) {
  const text = output?.trim() ?? "";
  if (findings === null || findings === undefined) {
    return (
      <Folded
        open={false}
        text={text}
        summary={
          <>
            <Chip tone="muted" icon={<ListChecks />}>
              {name} not run
            </Chip>
            <span>{text.length > 0 ? `${name} did not finish` : `graded before the ${name} check existed`}</span>
          </>
        }
      />
    );
  }

  return (
    <Folded
      open={false}
      text={text}
      summary={
        <>
          <Chip tone={findings === 0 ? "success" : "warn"} icon={<ListChecks />}>
            {findings === 0 ? `${name}: clean` : `${name}: ${findings} ${findings === 1 ? "finding" : "findings"}`}
          </Chip>
          <span>{covers}; the grader decides what costs points</span>
        </>
      }
    />
  );
}

function Folded({ summary, text, open }: { summary: ReactNode; text: string; open: boolean }) {
  const line = <span className="flex flex-wrap items-center gap-2">{summary}</span>;
  if (text.length === 0) {
    return <div className="text-[11px] text-ink-muted">{line}</div>;
  }

  return (
    <details className="text-[11px] text-ink-muted" open={open}>
      <summary className="cursor-pointer list-none [&::-webkit-details-marker]:hidden">{line}</summary>
      <pre className="mt-1.5 max-h-60 overflow-auto whitespace-pre-wrap rounded-md border border-line bg-paper px-3 py-2 font-mono text-[11px] leading-relaxed text-ink-soft">
        {text}
      </pre>
    </details>
  );
}
