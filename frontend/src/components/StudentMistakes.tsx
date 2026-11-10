import { Link } from "react-router";
import type { MistakeCategoryInfo, StudentMistakeInfo } from "../api/client";
import { useStudentMistakes } from "../api/hooks";
import { formatDay } from "../format";
import { severityColor } from "./CodeBrowser";
import { Chip, ErrorPanel } from "./ui";

export const categoryLabels: Record<string, string> = {
  Correctness: "Correctness",
  MissingRequirement: "Missing requirement",
  Encapsulation: "Encapsulation",
  Design: "Design",
  ErrorHandling: "Error handling",
  Naming: "Naming",
  Readability: "Readability",
  Other: "Other",
};

export function MistakeCategoriesCard({ courseId, userId }: { courseId: string; userId: string }) {
  const mistakes = useStudentMistakes(courseId, userId);
  const categories = mistakes.data?.categories ?? [];
  const most = Math.max(1, ...categories.map((c) => c.count));

  return (
    <section className="surface">
      <div className="flex items-start justify-between gap-2 border-b border-line px-4 py-2.5">
        <div>
          <h3 className="text-[13px] font-semibold">Mistake categories</h3>
          <div className="text-[11px] text-ink-muted">From approved reports · most points lost first</div>
        </div>
        {categories.length > 0 && (
          <Link to={`/courses/${courseId}/students/${userId}/mistakes`} className="shrink-0 text-xs font-semibold text-accent hover:underline">
            Analyze →
          </Link>
        )}
      </div>
      {mistakes.isError && (
        <div className="p-3">
          <ErrorPanel error={mistakes.error} />
        </div>
      )}
      {mistakes.data && categories.length === 0 && (
        <p className="px-4 py-3 text-xs text-ink-muted">Nothing yet: mistakes appear here once you approve a report for this student.</p>
      )}
      <ul className="divide-y divide-paper-dark">
        {categories.map((category) => (
          <CategoryRow key={category.category} category={category} most={most} />
        ))}
      </ul>
    </section>
  );
}

function CategoryRow({ category, most }: { category: MistakeCategoryInfo; most: number }) {
  return (
    <li className="px-4 py-2.5">
      <div className="flex items-center justify-between gap-2 text-xs">
        <b>{categoryLabels[category.category] ?? category.category}</b>
        <span className="flex items-center gap-1.5">
          {category.repeated && <Chip tone="warn">Repeated</Chip>}
          {category.pointsLost > 0 && <b className="rounded bg-danger-tint px-1 font-mono text-[11px] text-danger-ink">−{round(category.pointsLost)}</b>}
        </span>
      </div>
      <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-paper-dark" aria-hidden>
        <div className="bar-fill h-full rounded-full bg-ink-muted/55" style={{ width: `${(category.count / most) * 100}%` }} />
      </div>
      <div className="mt-1 text-[11px] text-ink-muted">
        {category.count} {category.count === 1 ? "mistake" : "mistakes"} in {category.assignments}{" "}
        {category.assignments === 1 ? "assignment" : "assignments"}
        {category.repeated && category.firstAssignmentTitle && ` · since ${category.firstAssignmentTitle}`}
      </div>
    </li>
  );
}

export function MistakeHistoryCard({ courseId, userId }: { courseId: string; userId: string }) {
  const mistakes = useStudentMistakes(courseId, userId);
  const list = mistakes.data?.mistakes ?? [];
  const groups = groupByAssignment(list);
  const repeated = list.filter((m) => m.label === "Repeated").length;

  return (
    <section className="surface">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-line px-4 py-2.5">
        <h3 className="text-[13px] font-semibold">Mistake history</h3>
        <span className="flex items-center gap-3 text-[11px] text-ink-muted">
          {list.length > 0
            ? `${list.length} from approved reports · ${repeated} repeated from an earlier assignment`
            : "Labelled new or repeated against earlier assignments"}
          {list.length > 0 && (
            <Link to={`/courses/${courseId}/students/${userId}/mistakes`} className="text-xs font-semibold text-accent hover:underline">
              Filter and charts →
            </Link>
          )}
        </span>
      </div>
      {mistakes.data && list.length === 0 && (
        <p className="px-4 py-3 text-xs text-ink-muted">
          No approved reports yet. Edit and approve a draft for this student and its mistakes are tracked here.
        </p>
      )}
      {groups.map((group) => (
        <div key={group.assignmentId} className="border-b border-paper-dark last:border-b-0">
          <div className="flex items-center justify-between gap-2 bg-paper/60 px-4 py-1.5 text-[11px]">
            <b>{group.title ?? "Assignment"}</b>
            <span className="text-ink-muted">{group.dueAt ? `due ${formatDay(group.dueAt)}` : ""}</span>
          </div>
          <ul className="divide-y divide-paper-dark">
            {group.items.map((m) => (
              <MistakeRow key={m.id} mistake={m} />
            ))}
          </ul>
        </div>
      ))}
    </section>
  );
}

export function MistakeRow({ mistake }: { mistake: StudentMistakeInfo }) {
  return (
    <li className="flex gap-3 px-4 py-2">
      <span className="mt-1 h-2.5 w-2.5 shrink-0 rounded-full" style={{ backgroundColor: severityColor[mistake.severity] }} />
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs">
          <b style={{ color: severityColor[mistake.severity] }}>{mistake.severity}</b>
          <span className="text-ink-muted">{categoryLabels[mistake.category] ?? mistake.category}</span>
          {mistake.points != null && mistake.points > 0 && <b className="rounded bg-danger-tint px-1 font-mono text-[11px] text-danger-ink">−{round(mistake.points)}</b>}
          {mistake.label === "Repeated" ? (
            <Chip tone="warn">Repeated{mistake.repeatedFromAssignmentTitle ? ` · since ${mistake.repeatedFromAssignmentTitle}` : ""}</Chip>
          ) : (
            <Chip tone="info">New</Chip>
          )}
          {mistake.decision === "Edited" && <span className="text-[11px] text-ink-muted">edited by you</span>}
        </div>
        <p className="mt-0.5 text-[13px]">{mistake.message}</p>
      </div>
    </li>
  );
}

function groupByAssignment(list: StudentMistakeInfo[]) {
  const groups: { assignmentId: string; title?: string | null; dueAt?: string | null; items: StudentMistakeInfo[] }[] = [];
  for (const mistake of list) {
    const last = groups[groups.length - 1];
    if (last && last.assignmentId === mistake.assignmentId) {
      last.items.push(mistake);
    } else {
      groups.push({ assignmentId: mistake.assignmentId, title: mistake.assignmentTitle, dueAt: mistake.assignmentDueAt, items: [mistake] });
    }
  }
  return groups;
}

function round(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(1);
}
