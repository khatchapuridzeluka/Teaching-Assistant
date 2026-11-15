import { Link } from "react-router";
import {
  ArrowDown,
  ArrowRight,
  BookOpen,
  ChevronRight,
  CircleCheck,
  ClipboardList,
  FileText,
  Gavel,
  History,
  ListChecks,
  type LucideIcon,
  Minus,
  NotebookPen,
  Paperclip,
  Sparkles,
  TriangleAlert,
} from "lucide-react";
import type { Assignment, GradingGuidance } from "../api/client";
import { formatAge, formatDate, formatDay } from "../format";
import { type LayerInfo, type LayerKey, plural, rubricLayers, rubricPath, type RubricTab, ScopeChip, Step } from "./RubricShared";
import { Card, CardHeader, Chip, EmptyState, Stat, StatStrip } from "./ui";

const assignmentPreview = 6;

export function hasInstructions(assignment: Assignment) {
  return assignment.materials.length > 0 || (assignment.description ?? "").trim().length > 0;
}

export function RubricOverview({ courseId, data, assignments }: { courseId: string; data: GradingGuidance; assignments: Assignment[] }) {
  const withInstructions = assignments.filter(hasInstructions).length;
  const withoutInstructions = assignments.length - withInstructions;
  const deducting = data.rules.filter((rule) => rule.effect === "Deduction").length;
  const noteCounts = new Map(data.notes.map((n) => [n.assignmentId, n.count]));
  const noteTotal = data.notes.reduce((sum, n) => sum + n.count, 0);
  const written = Boolean(data.courseGuidance);

  const details: Record<LayerKey, { text: string; empty: boolean }> = {
    approach: {
      text: data.approach.length === 0 ? "Every principle is deleted" : `${plural(data.approach.length, "principle")}, the same for every course`,
      empty: data.approach.length === 0,
    },
    guidance: {
      text: written
        ? `Your text for this course · saved ${formatDay(data.courseGuidanceUpdatedAt)}`
        : "Not written yet: how strict to be with this group, what matters most",
      empty: !written,
    },
    rules: {
      text:
        data.rules.length === 0
          ? "None yet: judgement calls applied the same way every time"
          : `${plural(data.rules.length, "rule")} · ${deducting} deduct points, ${data.rules.length - deducting} cost nothing`,
      empty: data.rules.length === 0,
    },
    instructions: {
      text: `From Classroom · ${withInstructions} of ${plural(assignments.length, "assignment")} with a statement or files`,
      empty: assignments.length > 0 && withInstructions === 0,
    },
    notes: {
      text:
        noteTotal === 0
          ? "None yet: extra criteria or a rule that differs for one assignment"
          : `${plural(noteTotal, "note")} on ${plural(data.notes.length, "assignment")}`,
      empty: noteTotal === 0,
    },
  };

  const checks: Check[] = [
    data.approach.length > 0
      ? { state: "ok", title: "Built-in approach is in place", detail: plural(data.approach.length, "principle"), tab: "approach" }
      : { state: "warn", title: "No built-in principles", detail: "The grader starts directly from your rules and the instructions", tab: "approach" },
    written
      ? { state: "ok", title: "Course guidance is written", detail: `Saved ${formatDate(data.courseGuidanceUpdatedAt)}`, tab: "guidance" }
      : { state: "warn", title: "Course guidance is not written", detail: "Tell the grader how strict to be with this group", tab: "guidance" },
    data.rules.length > 0
      ? { state: "ok", title: `${plural(data.rules.length, "rule")} in use`, detail: "Applied the same way for every student", tab: "rules" }
      : { state: "optional", title: "No rules yet", detail: "Add one when a judgement call keeps coming back", tab: "rules" },
    assignments.length === 0
      ? { state: "optional", title: "No published assignments", detail: "Instructions appear here once Classroom has assignments", tab: "assignments" }
      : withoutInstructions === 0
        ? { state: "ok", title: "Every assignment has instructions", detail: "A statement or files in Classroom", tab: "assignments" }
        : {
            state: "warn",
            title: `${plural(withoutInstructions, "assignment")} without instructions`,
            detail: "No statement and no files in Classroom",
            tab: "assignments",
          },
    noteTotal > 0
      ? { state: "ok", title: `Notes on ${plural(data.notes.length, "assignment")}`, detail: "Optional: only where an assignment differs", tab: "assignments" }
      : { state: "optional", title: "No assignment notes", detail: "Optional: only where an assignment differs", tab: "assignments" },
  ];

  const changes: Change[] = [
    ...data.approach.map((p) => ({ key: p.id, icon: BookOpen, kind: "Principle", title: p.title, at: p.updatedAt, tab: "approach" as const })),
    ...data.rules.map((r) => ({ key: r.id, icon: Gavel, kind: "Rule", title: r.title, at: r.updatedAt, tab: "rules" as const })),
    ...(written && data.courseGuidanceUpdatedAt
      ? [{ key: "guidance", icon: NotebookPen, kind: "Course guidance", title: "How to grade this course", at: data.courseGuidanceUpdatedAt, tab: "guidance" as const }]
      : []),
  ]
    .sort((a, b) => new Date(b.at).getTime() - new Date(a.at).getTime())
    .slice(0, 5);

  return (
    <>
      <StatStrip columns={5}>
        <Stat label="Principles" value={data.approach.length} note="built in · every course" icon={<BookOpen />} />
        <Stat
          label="Course guidance"
          value={written ? "Written" : "Empty"}
          note={written ? `saved ${formatDay(data.courseGuidanceUpdatedAt)}` : "not written yet"}
          icon={<NotebookPen />}
          tone={written ? "good" : undefined}
        />
        <Stat label="Rules" value={data.rules.length} note={`${deducting} deduct · ${data.rules.length - deducting} cost nothing`} icon={<Gavel />} />
        <Stat
          label="Instructions"
          value={`${withInstructions}/${assignments.length}`}
          note="assignments, from Classroom"
          icon={<FileText />}
          tone={withoutInstructions > 0 ? "bad" : undefined}
        />
        <Stat
          label="Assignment notes"
          value={noteTotal}
          note={noteTotal === 0 ? "none yet · optional" : `on ${data.notes.length} of ${plural(assignments.length, "assignment")}`}
          icon={<ClipboardList />}
        />
      </StatStrip>

      <div className="@container/page">
        <div className="grid gap-4 @4xl/page:grid-cols-[minmax(0,1.35fr)_minmax(0,1fr)]">
          <Card className="flex flex-col">
            <CardHeader
              icon={<Sparkles />}
              title="What the grader receives"
              subtitle="Five layers, read top to bottom; a later layer wins where two disagree"
            />
            <ol className="relative flex flex-1 flex-col justify-between gap-1.5 px-[18px] py-4">
              <span className="absolute bottom-9 left-[31.5px] top-9 w-px bg-line" aria-hidden />
              {rubricLayers.map((layer, i) => (
                <LayerRow
                  key={layer.key}
                  n={i + 1}
                  layer={layer}
                  detail={details[layer.key].text}
                  empty={details[layer.key].empty}
                  to={rubricPath(courseId, layer.tab)}
                />
              ))}
              <li className="relative flex items-center gap-3 pt-1 text-xs text-ink-muted">
                <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full border border-dashed border-ink-faint bg-white">
                  <ArrowDown className="h-3.5 w-3.5" />
                </span>
                then the student's code, files and images, graded against all of the above
              </li>
            </ol>
          </Card>

          <div className="flex min-w-0 flex-col gap-4">
            <Card className="overflow-hidden">
              <CardHeader icon={<ListChecks />} title="Before you grade" subtitle="What is in place for this course" />
              <ul className="divide-y divide-paper-dark">
                {checks.map((check) => (
                  <CheckRow key={check.title} check={check} to={rubricPath(courseId, check.tab)} />
                ))}
              </ul>
            </Card>

            <Card className="overflow-hidden">
              <CardHeader icon={<History />} title="Last changed" subtitle="Principles, course guidance and rules" />
              {changes.length === 0 ? (
                <div className="px-[18px] py-6 text-center text-xs text-ink-muted">Nothing has been written yet.</div>
              ) : (
                <ul className="divide-y divide-paper-dark">
                  {changes.map((change) => (
                    <li key={change.key}>
                      <Link to={rubricPath(courseId, change.tab)} className="flex items-center gap-2.5 px-[18px] py-2 transition-colors hover:bg-paper">
                        <change.icon className="h-3.5 w-3.5 shrink-0 text-ink-faint" />
                        <span className="min-w-0 flex-1 truncate text-[12.5px]">
                          <span className="text-ink-muted">{change.kind} · </span>
                          <span className="font-medium">{change.title}</span>
                        </span>
                        <span className="shrink-0 text-[11px] text-ink-muted" title={formatDate(change.at)}>
                          {formatAge(change.at)} ago
                        </span>
                      </Link>
                    </li>
                  ))}
                </ul>
              )}
            </Card>
          </div>
        </div>
      </div>

      <Card className="overflow-hidden">
        <CardHeader
          icon={<ClipboardList />}
          title="By assignment"
          subtitle="Instructions come from Classroom; notes are yours and apply to one assignment only"
          actions={
            assignments.length > 0 && (
              <Link to={rubricPath(courseId, "assignments")} className="flex items-center gap-1 text-xs font-semibold text-accent hover:underline">
                Open all <ArrowRight className="h-3.5 w-3.5" />
              </Link>
            )
          }
        />
        {assignments.length === 0 ? (
          <EmptyState icon={<ClipboardList />}>This course has no published assignments.</EmptyState>
        ) : (
          <div className="@container">
            <div className="hidden grid-cols-[minmax(0,1fr)_150px_110px_120px_16px] items-center gap-x-4 border-b border-line bg-paper/60 px-[18px] py-1.5 text-[11px] font-semibold text-ink-muted @2xl:grid">
              <span>Assignment</span>
              <span>Statement</span>
              <span>Files</span>
              <span>Notes</span>
              <span />
            </div>
            <ul className="divide-y divide-paper-dark">
              {assignments.slice(0, assignmentPreview).map((assignment) => (
                <AssignmentRow
                  key={assignment.id}
                  assignment={assignment}
                  notes={noteCounts.get(assignment.id) ?? 0}
                  to={rubricPath(courseId, "assignments", assignment.id)}
                />
              ))}
            </ul>
            {assignments.length > assignmentPreview && (
              <Link
                to={rubricPath(courseId, "assignments")}
                className="block border-t border-line px-[18px] py-2.5 text-center text-xs font-semibold text-accent hover:bg-paper"
              >
                All {assignments.length} assignments →
              </Link>
            )}
          </div>
        )}
      </Card>
    </>
  );
}

function LayerRow({ n, layer, detail, empty, to }: { n: number; layer: LayerInfo; detail: string; empty: boolean; to: string }) {
  const Icon = layer.icon;
  return (
    <li className="relative flex items-center gap-3">
      <Step n={n} empty={empty} />
      <Link
        to={to}
        className={`group flex min-w-0 flex-1 items-center gap-3 rounded-lg border px-3 py-2.5 transition-colors hover:border-accent ${
          empty ? "border-dashed border-ink-faint bg-white" : "border-line bg-white hover:bg-accent-tint/30"
        }`}
      >
        <Icon className={`h-4 w-4 shrink-0 ${empty ? "text-ink-faint" : "text-ink-muted"}`} />
        <span className="min-w-0 flex-1">
          <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
            <b className={`text-[13px] ${empty ? "text-ink-muted" : ""}`}>{layer.label}</b>
            <ScopeChip scope={layer.scope} />
          </span>
          <span className="mt-0.5 block truncate text-[11.5px] text-ink-muted">{detail}</span>
        </span>
        {empty ? <Chip tone="dashed">Empty</Chip> : <Chip tone="success">In use</Chip>}
        <ChevronRight className="h-4 w-4 shrink-0 text-ink-faint transition-transform group-hover:translate-x-0.5 group-hover:text-accent" />
      </Link>
    </li>
  );
}

type Check = { state: "ok" | "warn" | "optional"; title: string; detail: string; tab: RubricTab };

type Change = { key: string; icon: LucideIcon; kind: string; title: string; at: string; tab: RubricTab };

const checkIcons = {
  ok: <CircleCheck className="h-4 w-4 text-success" />,
  warn: <TriangleAlert className="h-4 w-4 text-warn" />,
  optional: <Minus className="h-4 w-4 text-ink-faint" />,
};

function CheckRow({ check, to }: { check: Check; to: string }) {
  return (
    <li>
      <Link to={to} className="group flex items-center gap-3 px-[18px] py-2.5 transition-colors hover:bg-paper">
        <span className="flex shrink-0">{checkIcons[check.state]}</span>
        <span className="min-w-0 flex-1">
          <span className="block truncate text-[13px] font-semibold">{check.title}</span>
          <span className="block truncate text-[11.5px] text-ink-muted">{check.detail}</span>
        </span>
        <ChevronRight className="h-4 w-4 shrink-0 text-ink-faint transition-transform group-hover:translate-x-0.5 group-hover:text-accent" />
      </Link>
    </li>
  );
}

function AssignmentRow({ assignment, notes, to }: { assignment: Assignment; notes: number; to: string }) {
  const statement = (assignment.description ?? "").trim();
  const files = assignment.materials.length;
  return (
    <li>
      <Link
        to={to}
        className="group grid grid-cols-[minmax(0,1fr)_16px] items-center gap-x-4 gap-y-1.5 px-[18px] py-2.5 transition-colors hover:bg-paper @2xl:grid-cols-[minmax(0,1fr)_150px_110px_120px_16px]"
      >
        <span className="min-w-0">
          <span className="block truncate text-[13px] font-semibold group-hover:text-accent-strong">{assignment.title}</span>
          <span className="block truncate text-[11px] text-ink-muted">{assignment.dueAt ? `due ${formatDay(assignment.dueAt)}` : "no due date"}</span>
        </span>
        <span className="col-start-1 row-start-2 flex flex-wrap items-center gap-x-4 gap-y-1.5 @2xl:contents">
          <span className="flex items-center gap-1.5 text-xs">
            {statement ? (
              <>
                <CircleCheck className="h-3.5 w-3.5 shrink-0 text-success" /> Written
              </>
            ) : files > 0 ? (
              <span className="text-ink-muted">In the files</span>
            ) : (
              <Chip tone="warn">No instructions</Chip>
            )}
          </span>
          <span className={`flex items-center gap-1.5 text-xs ${files === 0 ? "text-ink-faint" : "text-ink-soft"}`}>
            <Paperclip className="h-3.5 w-3.5 shrink-0" />
            {files === 0 ? "none" : plural(files, "file")}
          </span>
          <span className="flex">{notes > 0 ? <Chip tone="info">{plural(notes, "note")}</Chip> : <Chip tone="dashed">No notes</Chip>}</span>
        </span>
        <ChevronRight className="col-start-2 row-start-1 h-4 w-4 text-ink-faint transition-transform group-hover:translate-x-0.5 group-hover:text-accent @2xl:col-start-auto @2xl:row-start-auto" />
      </Link>
    </li>
  );
}
