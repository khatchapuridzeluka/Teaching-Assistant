import { Link } from "react-router";
import type { StrengthCategoryInfo, StudentStrengthInfo, StudentStrengths } from "../api/client";
import { useStudentStrengths } from "../api/hooks";
import { strengthColor } from "./CodeBrowser";
import { categoryLabels } from "./StudentMistakes";
import { Chip, ErrorPanel } from "./ui";

export function StrengthsCard({ courseId, userId }: { courseId: string; userId: string }) {
  const strengths = useStudentStrengths(courseId, userId);
  const data = strengths.data;
  const categories = data?.categories ?? [];
  const most = Math.max(1, ...categories.map((c) => c.count));
  const link = `/courses/${courseId}/students/${userId}/strengths`;

  return (
    <section className="surface">
      <div className="flex items-start justify-between gap-2 border-b border-line px-4 py-2.5">
        <div>
          <h3 className="text-[13px] font-semibold">Strengths</h3>
          <div className="text-[11px] text-ink-muted">From AI drafts and your reports · shown in most assignments first</div>
        </div>
        {categories.length > 0 && (
          <Link to={link} className="shrink-0 text-xs font-semibold text-accent hover:underline">
            Analyze →
          </Link>
        )}
      </div>
      {strengths.isError && (
        <div className="p-3">
          <ErrorPanel error={strengths.error} />
        </div>
      )}
      {data && categories.length === 0 && (
        <p className="px-4 py-3 text-xs text-ink-muted">
          Nothing yet: strengths appear here once a graded assignment of this student notes some (drafts graded before strengths
          existed have none).
        </p>
      )}
      {data && categories.length > 0 && <p className="border-b border-paper-dark px-4 py-2.5 text-xs leading-relaxed">{summarize(data)}</p>}
      <ul className="divide-y divide-paper-dark">
        {categories.map((category) => (
          <StrengthCategoryRow key={category.category} category={category} most={most} />
        ))}
      </ul>
    </section>
  );
}

function StrengthCategoryRow({ category, most }: { category: StrengthCategoryInfo; most: number }) {
  return (
    <li className="px-4 py-2.5">
      <div className="flex items-center justify-between gap-2 text-xs">
        <b>{categoryLabels[category.category] ?? category.category}</b>
        <span className="flex items-center gap-1.5">
          {category.improvedSinceAssignmentTitle && <Chip tone="success">Improved</Chip>}
          {category.consistent && <Chip tone="info">Consistent</Chip>}
        </span>
      </div>
      <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-paper-dark" aria-hidden>
        <div className="bar-fill h-full rounded-full" style={{ width: `${(category.count / most) * 100}%`, backgroundColor: strengthColor }} />
      </div>
      <div className="mt-1 text-[11px] text-ink-muted">
        {category.count} {category.count === 1 ? "strength" : "strengths"} in {category.assignments}{" "}
        {category.assignments === 1 ? "assignment" : "assignments"}
        {category.mistakes > 0 && ` · ${category.mistakes} ${category.mistakes === 1 ? "mistake" : "mistakes"} in this area`}
        {category.improvedSinceAssignmentTitle && ` · mistakes in ${category.improvedSinceAssignmentTitle} before`}
      </div>
    </li>
  );
}

export function StrengthRow({ strength }: { strength: StudentStrengthInfo }) {
  return (
    <li className="flex gap-3 px-4 py-2">
      <span className="mt-1 h-2.5 w-2.5 shrink-0 rounded-full" style={{ backgroundColor: strengthColor }} />
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs">
          <b>{categoryLabels[strength.category] ?? strength.category}</b>
          <StrengthLabelChip strength={strength} />
          {strength.decision === "Edited" && <span className="text-[11px] text-ink-muted">edited by you</span>}
          {strength.file && (
            <span className="ml-auto font-mono text-[11px] text-ink-muted">
              {strength.file.slice(strength.file.lastIndexOf("/") + 1)}
              {strength.startLine != null ? `:${strength.startLine}` : ""}
            </span>
          )}
        </div>
        <p className="mt-0.5 text-[13px]">{strength.message}</p>
      </div>
    </li>
  );
}

function StrengthLabelChip({ strength }: { strength: StudentStrengthInfo }) {
  const since = strength.sinceAssignmentTitle;
  if (strength.label === "Improved") {
    return <Chip tone="success">Improved{since ? ` · mistakes in ${since}` : ""}</Chip>;
  }
  if (strength.label === "Consistent") {
    return <Chip tone="info">Consistent{since ? ` · since ${since}` : ""}</Chip>;
  }
  return <Chip tone="outline">New</Chip>;
}

export function summarize(data: StudentStrengths): string {
  const categories = data.categories;
  if (categories.length === 0) {
    return "No strengths recorded for these filters.";
  }

  const assignments = data.assignments.filter((a) => a.strengths > 0).length;
  const parts = [`${data.total} ${data.total === 1 ? "strength" : "strengths"} in ${assignments} ${assignments === 1 ? "assignment" : "assignments"}.`];

  const top = categories.slice(0, 2).map((c) => `${name(c)} (${c.count} in ${c.assignments} ${c.assignments === 1 ? "assignment" : "assignments"})`);
  parts.push(`Strongest in ${top.join(" and ")}.`);

  const consistent = categories.filter((c) => c.consistent).map(name);
  if (consistent.length > 0) {
    parts.push(`Shown again and again: ${list(consistent)}.`);
  }

  const improved = categories.filter((c) => c.improvedSinceAssignmentTitle);
  if (improved.length > 0) {
    parts.push(
      `Improved: ${list(improved.map((c) => `${name(c)} (mistakes in ${c.improvedSinceAssignmentTitle}, a strength in ${c.firstAssignmentTitle ?? "a later assignment"})`))}.`,
    );
  }

  return parts.join(" ");
}

function name(category: StrengthCategoryInfo): string {
  return categoryLabels[category.category] ?? category.category;
}

function list(items: string[]): string {
  return items.length <= 1 ? (items[0] ?? "") : `${items.slice(0, -1).join(", ")} and ${items[items.length - 1]}`;
}
