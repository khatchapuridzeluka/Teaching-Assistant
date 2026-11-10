import { useEffect, useState } from "react";
import type { CodeMistake, CodeStrength, GradingReport, GradingResult, MistakeCategory, MistakeSeverity } from "../api/client";
import { useSaveReport } from "../api/hooks";
import { severityColor, strengthColor } from "./CodeBrowser";
import { Button, Chip, ErrorPanel } from "./ui";

const severities: MistakeSeverity[] = ["Major", "Minor", "Style"];

const categories: { value: MistakeCategory; label: string }[] = [
  { value: "Correctness", label: "Correctness" },
  { value: "MissingRequirement", label: "Missing requirement" },
  { value: "Encapsulation", label: "Encapsulation" },
  { value: "Design", label: "Design" },
  { value: "ErrorHandling", label: "Error handling" },
  { value: "Naming", label: "Naming" },
  { value: "Readability", label: "Readability" },
  { value: "Other", label: "Other" },
];

type EditableMistake = {
  sourceMistakeId: string;
  file?: string | null;
  startLine?: number | null;
  endLine?: number | null;
  severity: MistakeSeverity;
  category: MistakeCategory;
  message: string;
  points: string;
  rejected: boolean;
  original: { severity: MistakeSeverity; category: MistakeCategory; message: string; points: number | null };
};

type EditableStrength = {
  sourceStrengthId: string;
  file?: string | null;
  startLine?: number | null;
  endLine?: number | null;
  category: MistakeCategory;
  message: string;
  rejected: boolean;
  original: { category: MistakeCategory; message: string };
};

export function ReportEditor({
  grading,
  report,
  studentName,
  onClose,
}: {
  grading: GradingResult;
  report: GradingReport | null | undefined;
  studentName: string;
  onClose: () => void;
}) {
  const save = useSaveReport(grading.id);
  const [score, setScore] = useState(toInput(report ? report.score : grading.score));
  const [bonus, setBonus] = useState(toInput(report ? report.bonus : grading.bonus));
  const [review, setReview] = useState(report?.review ?? grading.review);
  const [mistakes, setMistakes] = useState<EditableMistake[]>(() => initialMistakes(grading, report));
  const [strengths, setStrengths] = useState<EditableStrength[]>(() => initialStrengths(grading, report));

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape" && !save.isPending) {
        onClose();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose, save.isPending]);

  function update(index: number, change: Partial<EditableMistake>) {
    setMistakes((list) => list.map((m, i) => (i === index ? { ...m, ...change } : m)));
  }

  function updateStrength(index: number, change: Partial<EditableStrength>) {
    setStrengths((list) => list.map((s, i) => (i === index ? { ...s, ...change } : s)));
  }

  function submit(approve: boolean) {
    save.mutate(
      {
        approve,
        body: {
          score: fromInput(score),
          bonus: fromInput(bonus),
          review,
          mistakes: mistakes.map((m) => ({
            sourceMistakeId: m.sourceMistakeId,
            rejected: m.rejected,
            severity: m.severity,
            category: m.category,
            message: m.message,
            points: fromInput(m.points),
          })),
          strengths: strengths.map((s) => ({
            sourceStrengthId: s.sourceStrengthId,
            rejected: s.rejected,
            category: s.category,
            message: s.message,
          })),
        },
      },
      { onSuccess: onClose },
    );
  }

  const rejected = mistakes.filter((m) => m.rejected).length;
  const deducted = mistakes.filter((m) => !m.rejected).reduce((sum, m) => sum + (fromInput(m.points) ?? 0), 0);
  const edited = mistakes.filter((m) => !m.rejected && isEdited(m)).length;
  const strengthsRejected = strengths.filter((s) => s.rejected).length;
  const strengthsEdited = strengths.filter((s) => !s.rejected && isStrengthEdited(s)).length;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-ink/40 p-3 sm:p-6"
      onClick={(e) => e.target === e.currentTarget && !save.isPending && onClose()}
    >
      <div role="dialog" aria-modal="true" aria-label="Edit draft" className="flex max-h-[92dvh] w-full max-w-3xl flex-col rounded-[12px] bg-paper shadow-xl">
        <header className="flex items-start justify-between gap-3 border-b border-line bg-white px-5 py-4 sm:rounded-t-[12px]">
          <div className="min-w-0">
            <h2 className="text-[15px] font-bold">Edit draft · {studentName}</h2>
            <div className="text-xs text-ink-muted">
              Your changes are saved as your report; the AI draft itself stays as it was.
              {report?.status === "Approved" && " Saving reopens the approved report."}
            </div>
          </div>
          <button onClick={onClose} aria-label="Close" className="shrink-0 text-lg leading-none text-ink-muted hover:text-ink">
            ×
          </button>
        </header>

        <div className="flex min-h-0 flex-1 flex-col gap-5 overflow-y-auto px-5 py-4">
          <section className="flex flex-wrap items-end gap-4">
            <label className="flex flex-col gap-1 text-xs font-semibold">
              Score
              <span className="flex items-center gap-1.5 font-normal text-ink-muted">
                <input
                  type="number"
                  min={0}
                  step="0.5"
                  value={score}
                  placeholder={toInput(grading.score)}
                  title={grading.score != null ? `AI suggested ${grading.score}` : undefined}
                  onChange={(e) => setScore(e.target.value)}
                  className="h-9 w-24 rounded-lg border border-line bg-white px-2 text-[14px] font-semibold text-ink placeholder:font-normal placeholder:text-ink-muted/60"
                />
                / {grading.maxScore ?? "–"}
              </span>
            </label>
            <label className="flex flex-col gap-1 text-xs font-semibold">
              Bonus
              <input
                type="number"
                min={0}
                step="0.5"
                value={bonus}
                placeholder={toInput(grading.bonus)}
                title={grading.bonus != null ? `AI suggested ${grading.bonus}` : undefined}
                onChange={(e) => setBonus(e.target.value)}
                className="h-9 w-24 rounded-lg border border-line bg-white px-2 text-[14px] font-semibold text-ink placeholder:font-normal placeholder:text-ink-muted/60"
              />
            </label>
            <span className="flex flex-col pb-1.5 text-xs text-ink-muted">
              <span>
                AI suggested {grading.score ?? "–"} / {grading.maxScore ?? "–"}
                {grading.bonus ? ` +${grading.bonus}` : ""}
              </span>
              <span>
                Kept mistakes take off <b className="text-ink">{formatPoints(deducted)}</b> points; the score is not changed for you
              </span>
            </span>
          </section>

          <section className="flex flex-col gap-1.5">
            <label htmlFor="report-review" className="text-xs font-semibold">
              Feedback <span className="font-normal text-ink-muted">· Markdown</span>
            </label>
            <textarea
              id="report-review"
              value={review}
              onChange={(e) => setReview(e.target.value)}
              rows={12}
              className="w-full resize-y rounded-[10px] border border-line bg-white px-3 py-2 font-mono text-[12px] leading-relaxed"
            />
          </section>

          <section className="flex flex-col gap-2">
            <h3 className="text-xs font-semibold">
              Mistakes · {mistakes.length}
              <span className="font-normal text-ink-muted">
                {edited > 0 && ` · ${edited} edited`}
                {rejected > 0 && ` · ${rejected} rejected`}
              </span>
            </h3>
            {mistakes.length === 0 && <div className="text-xs text-ink-muted">The AI found no mistakes in this draft.</div>}
            {mistakes.map((m, i) => (
              <div
                key={m.sourceMistakeId}
                className={`flex flex-col gap-2 rounded-[10px] border bg-white px-3 py-2.5 ${m.rejected ? "border-dashed border-line opacity-60" : "border-line"}`}
              >
                <div className="flex flex-wrap items-center gap-2 text-xs">
                  <span className="h-2.5 w-2.5 shrink-0 rounded-full" style={{ backgroundColor: severityColor[m.severity] }} />
                  <select
                    value={m.severity}
                    onChange={(e) => update(i, { severity: e.target.value as MistakeSeverity })}
                    disabled={m.rejected}
                    aria-label="Severity"
                    className="h-7 rounded-lg border border-line bg-white px-1.5 font-semibold"
                  >
                    {severities.map((s) => (
                      <option key={s} value={s}>
                        {s}
                      </option>
                    ))}
                  </select>
                  <select
                    value={m.category}
                    onChange={(e) => update(i, { category: e.target.value as MistakeCategory })}
                    disabled={m.rejected}
                    aria-label="Category"
                    className="h-7 rounded-lg border border-line bg-white px-1.5"
                  >
                    {categories.map((c) => (
                      <option key={c.value} value={c.value}>
                        {c.label}
                      </option>
                    ))}
                  </select>
                  <label className="flex items-center gap-1 text-ink-muted" title="Points this mistake takes off">
                    −
                    <input
                      type="number"
                      min={0}
                      step="0.5"
                      value={m.points}
                      placeholder={toInput(m.original.points)}
                      title={m.original.points != null ? `AI suggested ${m.original.points}` : "The AI gave no points for this mistake"}
                      onChange={(e) => update(i, { points: e.target.value })}
                      disabled={m.rejected}
                      aria-label="Points deducted"
                      className="h-7 w-14 rounded-lg border border-line bg-white px-1.5 font-semibold text-ink placeholder:font-normal placeholder:text-ink-muted/60"
                    />
                    pts
                  </label>
                  <span className="font-mono text-ink-muted">{location(m)}</span>
                  {m.rejected ? <Chip tone="muted">Rejected</Chip> : isEdited(m) && <Chip tone="tint">Edited</Chip>}
                  <Button small className="ml-auto" onClick={() => update(i, { rejected: !m.rejected })}>
                    {m.rejected ? "Restore" : "Reject"}
                  </Button>
                </div>
                <textarea
                  value={m.message}
                  onChange={(e) => update(i, { message: e.target.value })}
                  disabled={m.rejected}
                  rows={2}
                  aria-label="Message"
                  className={`w-full resize-y rounded-lg border border-line px-2.5 py-1.5 text-[13px] ${m.rejected ? "bg-paper line-through" : "bg-white"}`}
                />
              </div>
            ))}
          </section>

          <section className="flex flex-col gap-2">
            <h3 className="text-xs font-semibold">
              Strengths · {strengths.length}
              <span className="font-normal text-ink-muted">
                {strengthsEdited > 0 && ` · ${strengthsEdited} edited`}
                {strengthsRejected > 0 && ` · ${strengthsRejected} rejected`}
                {" · never change the score; kept ones go to the student's profile on approve"}
              </span>
            </h3>
            {strengths.length === 0 && <div className="text-xs text-ink-muted">The AI noted no strengths in this draft.</div>}
            {strengths.map((s, i) => (
              <div
                key={s.sourceStrengthId}
                className={`flex flex-col gap-2 rounded-[10px] border bg-white px-3 py-2.5 ${s.rejected ? "border-dashed border-line opacity-60" : "border-line"}`}
              >
                <div className="flex flex-wrap items-center gap-2 text-xs">
                  <span className="h-2.5 w-2.5 shrink-0 rounded-full" style={{ backgroundColor: strengthColor }} />
                  <b>Strength</b>
                  <select
                    value={s.category}
                    onChange={(e) => updateStrength(i, { category: e.target.value as MistakeCategory })}
                    disabled={s.rejected}
                    aria-label="Category"
                    className="h-7 rounded-lg border border-line bg-white px-1.5"
                  >
                    {categories.map((c) => (
                      <option key={c.value} value={c.value}>
                        {c.label}
                      </option>
                    ))}
                  </select>
                  <span className="font-mono text-ink-muted">{location(s)}</span>
                  {s.rejected ? <Chip tone="muted">Rejected</Chip> : isStrengthEdited(s) && <Chip tone="tint">Edited</Chip>}
                  <Button small className="ml-auto" onClick={() => updateStrength(i, { rejected: !s.rejected })}>
                    {s.rejected ? "Restore" : "Reject"}
                  </Button>
                </div>
                <textarea
                  value={s.message}
                  onChange={(e) => updateStrength(i, { message: e.target.value })}
                  disabled={s.rejected}
                  rows={2}
                  aria-label="Strength"
                  className={`w-full resize-y rounded-lg border border-line px-2.5 py-1.5 text-[13px] ${s.rejected ? "bg-paper line-through" : "bg-white"}`}
                />
              </div>
            ))}
          </section>
        </div>

        <footer className="flex flex-col gap-2 border-t border-line bg-white px-5 py-3 sm:rounded-b-[12px]">
          {save.isError && <ErrorPanel error={save.error} />}
          <div className="flex flex-wrap items-center justify-end gap-2">
            <span className="mr-auto text-[11px] text-ink-muted">
              Approve makes this the one that counts: {studentName}'s other drafts of this assignment are discarded.
            </span>
            <Button onClick={onClose} disabled={save.isPending}>
              Cancel
            </Button>
            <Button onClick={() => submit(false)} disabled={save.isPending} title="Keep your edits as a report in review; you can come back to it">
              {save.isPending && !save.variables?.approve ? "Saving…" : "Save"}
            </Button>
            <Button variant="primary" onClick={() => submit(true)} disabled={save.isPending} title="Save and mark this report as final; the student's other drafts of this assignment (any version) are discarded, so only this one counts on the profile">
              {save.isPending && save.variables?.approve ? "Approving…" : "Approve"}
            </Button>
          </div>
        </footer>
      </div>
    </div>
  );
}

export function keptMistakes(report: GradingReport): CodeMistake[] {
  return report.mistakes
    .filter((m) => m.decision !== "Rejected")
    .map((m) => ({
      file: m.file,
      startLine: m.startLine,
      endLine: m.endLine,
      severity: m.severity,
      category: m.category,
      message: m.message,
      points: m.points,
    }));
}

export function keptStrengths(report: GradingReport): CodeStrength[] {
  return report.strengths
    .filter((s) => s.decision !== "Rejected")
    .map((s) => ({ file: s.file, startLine: s.startLine, endLine: s.endLine, category: s.category, message: s.message }));
}

export function ReportChip({ report }: { report: GradingReport }) {
  return report.status === "Approved" ? <Chip tone="accent">Report approved</Chip> : <Chip tone="ink">Report in review</Chip>;
}

function initialMistakes(grading: GradingResult, report: GradingReport | null | undefined): EditableMistake[] {
  return grading.mistakes
    .filter((m) => m.id != null)
    .map((m) => {
      const saved = report?.mistakes.find((r) => r.sourceMistakeId === m.id);
      return {
        sourceMistakeId: m.id!,
        file: m.file,
        startLine: m.startLine,
        endLine: m.endLine,
        severity: saved?.severity ?? m.severity,
        category: saved?.category ?? m.category,
        message: saved?.message ?? m.message,
        points: toInput(saved ? saved.points : m.points),
        rejected: saved?.decision === "Rejected",
        original: { severity: m.severity, category: m.category, message: m.message, points: m.points ?? null },
      };
    });
}

function initialStrengths(grading: GradingResult, report: GradingReport | null | undefined): EditableStrength[] {
  return grading.strengths
    .filter((s) => s.id != null)
    .map((s) => {
      const saved = report?.strengths.find((r) => r.sourceStrengthId === s.id);
      return {
        sourceStrengthId: s.id!,
        file: s.file,
        startLine: s.startLine,
        endLine: s.endLine,
        category: saved?.category ?? s.category,
        message: saved?.message ?? s.message,
        rejected: saved?.decision === "Rejected",
        original: { category: s.category, message: s.message },
      };
    });
}

function isStrengthEdited(s: EditableStrength): boolean {
  return s.category !== s.original.category || s.message.trim() !== s.original.message.trim();
}

function isEdited(m: EditableMistake): boolean {
  return (
    m.severity !== m.original.severity ||
    m.category !== m.original.category ||
    m.message.trim() !== m.original.message.trim() ||
    fromInput(m.points) !== m.original.points
  );
}

function location(m: { file?: string | null; startLine?: number | null; endLine?: number | null }): string {
  const name = m.file?.slice(m.file.lastIndexOf("/") + 1);
  if (!name) {
    return "Whole submission";
  }
  if (m.startLine == null) {
    return name;
  }
  return m.endLine && m.endLine !== m.startLine ? `${name}:${m.startLine}–${m.endLine}` : `${name}:${m.startLine}`;
}

function toInput(value: number | null | undefined): string {
  return value == null ? "" : String(value);
}

function fromInput(value: string): number | null {
  return value.trim() === "" ? null : Number(value);
}

function formatPoints(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(1);
}
