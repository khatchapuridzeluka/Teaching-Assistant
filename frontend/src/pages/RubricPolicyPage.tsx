import { type ReactNode, useState } from "react";
import { Link, useParams } from "react-router";
import {
  ArrowDown,
  ArrowLeft,
  ArrowRight,
  BookOpen,
  ChevronRight,
  ClipboardList,
  FileText,
  Gavel,
  type LucideIcon,
  NotebookPen,
  Paperclip,
  Pencil,
  Plus,
  ScrollText,
  Sparkles,
  Trash2,
} from "lucide-react";
import type { Assignment, GradingGuidance, GradingPrinciple, PolicyRule } from "../api/client";
import {
  useAddPrinciple,
  useAssignments,
  useDeletePrinciple,
  useGradingGuidance,
  useSetCourseGuidance,
  useUpdatePrinciple,
} from "../api/hooks";
import { ConfirmDialog } from "../components/ConfirmDialog";
import { CourseName } from "../components/Layout";
import { Button, buttonClass, Card, CardHeader, Chip, ErrorPanel, Modal, PageHeader, Spinner, Stat, StatStrip } from "../components/ui";
import { formatDate, formatDay, formatPoints } from "../format";

type LayerKey = "approach" | "guidance" | "rules" | "instructions" | "notes";

type LayerInfo = { key: LayerKey; label: string; icon: LucideIcon; purpose: string };

export const rubricLayers: LayerInfo[] = [
  { key: "approach", label: "Built-in approach", icon: BookOpen, purpose: "How the grader works in every course; part of the tool" },
  { key: "guidance", label: "Course guidance", icon: NotebookPen, purpose: "Your own words for this course: how strict to be, what matters most" },
  { key: "rules", label: "Your rules", icon: Gavel, purpose: "Judgement calls applied the same way for every student" },
  { key: "instructions", label: "Assignment instructions", icon: FileText, purpose: "Each assignment's statement and files, fetched from Classroom" },
  { key: "notes", label: "Assignment notes", icon: ClipboardList, purpose: "Extra criteria or a different rule for one assignment" },
];

function hasInstructions(assignment: Assignment) {
  return assignment.materials.length > 0 || (assignment.description ?? "").trim().length > 0;
}

// ---------- Overview ----------

export function RubricPolicyPage() {
  const { courseId = "" } = useParams();
  const guidance = useGradingGuidance(courseId);
  const assignments = useAssignments(courseId);
  const data = guidance.data;
  const list = assignments.data ?? [];
  const withInstructions = list.filter(hasInstructions).length;

  const details: Record<LayerKey, { text: string; empty: boolean }> = {
    approach: { text: `${data?.approach.length ?? 0} principles, the same for every course`, empty: false },
    guidance: {
      text: data?.courseGuidance ? "Your text for this course" : "Not written yet: how strict to be with this group, what matters most",
      empty: !data?.courseGuidance,
    },
    rules: {
      text: `${data?.rules.length ?? 0} ${data?.rules.length === 1 ? "rule" : "rules"} applied the same way every time`,
      empty: (data?.rules.length ?? 0) === 0,
    },
    instructions: {
      text: assignments.data
        ? `From Classroom: ${withInstructions} of ${list.length} assignments have a statement or files`
        : "From Classroom, per assignment",
      empty: false,
    },
    notes: { text: "None yet: extra criteria or a rule that differs for one assignment", empty: true },
  };

  return (
    <>
      <PageHeader
        lead={<Lead icon={ScrollText} />}
        title="Rubric & policy"
        subtitle={
          <>
            <CourseName courseId={courseId} /> · everything the grader is told before it reads a student's work, in the order it gets it
          </>
        }
        actions={<Chip tone="outline">Rules and notes: editing comes next</Chip>}
      />

      {guidance.isError && <ErrorPanel error={guidance.error} />}
      {guidance.isLoading && <Spinner />}

      {data && (
        <Card>
          <CardHeader
            icon={<Sparkles />}
            title="What the grader receives"
            subtitle="Five layers, top to bottom; a later layer wins where two disagree"
          />
          <ol className="flex flex-col gap-1.5 px-[18px] py-4">
            {rubricLayers.map((layer, i) => (
              <LayerRow
                key={layer.key}
                n={i + 1}
                layer={layer}
                detail={details[layer.key].text}
                empty={details[layer.key].empty}
                to={`/courses/${courseId}/rubric/${layer.key}`}
              />
            ))}
            <li className="flex items-center gap-2 pl-3 pt-1 text-xs text-ink-muted">
              <ArrowDown className="h-3.5 w-3.5" />
              then the student's code, files and images, graded against all of the above
            </li>
          </ol>
        </Card>
      )}
    </>
  );
}

function LayerRow({ n, layer, detail, empty, to }: { n: number; layer: LayerInfo; detail: string; empty: boolean; to: string }) {
  const Icon = layer.icon;
  return (
    <li>
      <Link
        to={to}
        className={`group flex items-center gap-3 rounded-lg border px-3 py-2.5 transition-colors hover:border-accent ${
          empty ? "border-dashed border-ink-faint bg-transparent" : "border-line bg-white"
        }`}
      >
        <Step n={n} empty={empty} />
        <Icon className={`h-4 w-4 shrink-0 ${empty ? "text-ink-faint" : "text-ink-muted"}`} />
        <span className="min-w-0 flex-1">
          <b className={`block text-[13px] ${empty ? "text-ink-muted" : ""}`}>{layer.label}</b>
          <span className="block truncate text-[11px] text-ink-muted">{detail}</span>
        </span>
        {empty ? <Chip tone="dashed">Empty</Chip> : <Chip tone="success">In use</Chip>}
        <ChevronRight className="h-4 w-4 shrink-0 text-ink-faint transition-transform group-hover:translate-x-0.5" />
      </Link>
    </li>
  );
}

// ---------- Shared frame of a layer page ----------

function LayerPage({
  layerKey,
  children,
  actions,
}: {
  layerKey: LayerKey;
  children: (data: GradingGuidance, assignments: Assignment[]) => ReactNode;
  actions?: ReactNode;
}) {
  const { courseId = "" } = useParams();
  const guidance = useGradingGuidance(courseId);
  const assignments = useAssignments(courseId);
  const index = rubricLayers.findIndex((l) => l.key === layerKey);
  const layer = rubricLayers[index]!;
  const previous = rubricLayers[index - 1];
  const next = rubricLayers[index + 1];
  const base = `/courses/${courseId}/rubric`;

  return (
    <>
      <PageHeader
        lead={<Lead icon={layer.icon} />}
        title={layer.label}
        subtitle={
          <>
            <CourseName courseId={courseId} /> · layer {index + 1} of {rubricLayers.length} · {layer.purpose}
          </>
        }
        actions={actions ?? <Chip tone="outline">Read-only for now</Chip>}
      />

      <nav className="flex flex-wrap items-center gap-1.5" aria-label="Layers">
        <Link to={base} className={buttonClass("ghost", true)}>
          <ArrowLeft className="h-3.5 w-3.5" /> All layers
        </Link>
        <span className="mx-1 h-4 w-px bg-line" />
        {rubricLayers.map((l, i) => (
          <Link
            key={l.key}
            to={`${base}/${l.key}`}
            aria-current={l.key === layerKey ? "page" : undefined}
            className={`flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-semibold transition-colors ${
              l.key === layerKey ? "border-accent bg-accent text-white" : "border-line bg-white text-ink-soft hover:border-accent"
            }`}
          >
            <span className="num">{i + 1}</span> {l.label}
          </Link>
        ))}
      </nav>

      {guidance.isError && <ErrorPanel error={guidance.error} />}
      {assignments.isError && <ErrorPanel error={assignments.error} />}
      {(guidance.isLoading || assignments.isLoading) && <Spinner />}
      {guidance.data && assignments.data && children(guidance.data, assignments.data)}

      <div className="flex items-center justify-between gap-3 border-t border-line pt-3">
        {previous ? (
          <Link to={`${base}/${previous.key}`} className={buttonClass("outline", true)}>
            <ArrowLeft className="h-3.5 w-3.5" /> {previous.label}
          </Link>
        ) : (
          <span />
        )}
        {next ? (
          <Link to={`${base}/${next.key}`} className={buttonClass("outline", true)}>
            {next.label} <ArrowRight className="h-3.5 w-3.5" />
          </Link>
        ) : (
          <span className="text-xs text-ink-muted">Then the student's work</span>
        )}
      </div>
    </>
  );
}

// ---------- 1. Built-in approach ----------

export function RubricApproachPage() {
  // null: closed; "new": adding; a principle: editing it.
  const [editing, setEditing] = useState<GradingPrinciple | "new" | null>(null);
  const [deleting, setDeleting] = useState<GradingPrinciple | null>(null);
  const remove = useDeletePrinciple();

  return (
    <LayerPage layerKey="approach" actions={<Chip tone="outline">Same for every course</Chip>}>
      {(data) => (
        <>
          <Explainer>
            The grader always starts from these principles, in every course. They describe how it grades in general; your course
            guidance, rules and assignment notes come after them and win where they disagree. A change here applies to the next
            grading in every course.
          </Explainer>
          {remove.isError && <ErrorPanel error={remove.error} />}
          {data.approach.length === 0 ? (
            <div className="flex flex-col gap-3">
              <Card className="px-[18px] py-4 text-[13px] text-ink-muted">
                Every principle is deleted, so the grader starts directly from your rules and the instructions.
              </Card>
              <AddPrincipleCard onClick={() => setEditing("new")} />
            </div>
          ) : (
            <ol className="grid gap-3 lg:grid-cols-2">
              {data.approach.map((principle, i) => (
                <li key={principle.id}>
                  <Card className="h-full px-[18px] py-3.5">
                    <div className="flex items-start gap-3">
                      <Step n={i + 1} />
                      <div className="min-w-0 flex-1">
                        <h3 className="text-[14px] font-semibold">{principle.title}</h3>
                        <p className="mt-1 text-[13px] leading-relaxed text-ink-soft">{principle.detail}</p>
                      </div>
                      <span className="-mr-1.5 -mt-1 flex shrink-0 gap-0.5">
                        <IconButton label={`Edit "${principle.title}"`} onClick={() => setEditing(principle)}>
                          <Pencil className="h-3.5 w-3.5" />
                        </IconButton>
                        <IconButton label={`Delete "${principle.title}"`} danger onClick={() => setDeleting(principle)}>
                          <Trash2 className="h-3.5 w-3.5" />
                        </IconButton>
                      </span>
                    </div>
                  </Card>
                </li>
              ))}
              <li>
                <AddPrincipleCard onClick={() => setEditing("new")} />
              </li>
            </ol>
          )}

          {editing && (
            <PrincipleModal
              principle={editing === "new" ? null : editing}
              position={data.approach.length + 1}
              onClose={() => setEditing(null)}
            />
          )}
          {deleting && (
            <ConfirmDialog
              title={`Delete "${deleting.title}"?`}
              confirmLabel="Delete principle"
              danger
              onCancel={() => setDeleting(null)}
              onConfirm={() => {
                remove.mutate(deleting.id);
                setDeleting(null);
              }}
            >
              <p>
                The grader stops using it from the next grading, in every course. Earlier drafts are not changed, and it stays in
                the database marked as deleted.
              </p>
            </ConfirmDialog>
          )}
        </>
      )}
    </LayerPage>
  );
}

function AddPrincipleCard({ onClick }: { onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="flex h-full min-h-[92px] w-full items-center justify-center gap-2 rounded-[10px] border border-dashed border-ink-faint px-[18px] py-3.5 text-[13px] font-semibold text-ink-muted transition-colors hover:border-accent hover:text-accent"
    >
      <Plus className="h-4 w-4" /> Add principle
    </button>
  );
}

// Adds a principle when principle is null, otherwise edits it.
function PrincipleModal({ principle, position, onClose }: { principle: GradingPrinciple | null; position: number; onClose: () => void }) {
  const [title, setTitle] = useState(principle?.title ?? "");
  const [detail, setDetail] = useState(principle?.detail ?? "");
  const [tried, setTried] = useState(false);
  const add = useAddPrinciple();
  const update = useUpdatePrinciple();
  const saving = add.isPending || update.isPending;
  const failed = add.isError ? add.error : update.isError ? update.error : null;
  const missing = title.trim().length === 0 || detail.trim().length === 0;

  function save() {
    setTried(true);
    if (missing) {
      return;
    }
    const body = { title: title.trim(), detail: detail.trim() };
    if (principle) {
      update.mutate({ id: principle.id, ...body }, { onSuccess: onClose });
    } else {
      add.mutate(body, { onSuccess: onClose });
    }
  }

  return (
    <Modal
      title={principle ? "Edit principle" : `Add principle ${position}`}
      subtitle={principle ? "Applies to the next grading in every course" : "Added at the end; applies to the next grading in every course"}
      onClose={onClose}
      busy={saving}
      size="max-w-xl"
      footer={
        <>
          <Button onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button variant="primary" onClick={save} disabled={saving}>
            {saving ? "Saving…" : principle ? "Save" : "Add principle"}
          </Button>
        </>
      }
    >
      <form
        className="flex shrink-0 flex-col gap-3.5 px-5 py-4"
        onSubmit={(e) => {
          e.preventDefault();
          save();
        }}
      >
        <label className="flex flex-col gap-1.5">
          <span className="eyebrow">Title</span>
          <input
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            maxLength={120}
            className="field"
            placeholder="Check the printed output"
            autoFocus
          />
          <span className="text-[11px] text-ink-muted">Short, for you; the grader does not see it</span>
        </label>
        <label className="flex flex-col gap-1.5">
          <span className="eyebrow">What the grader is told</span>
          <textarea
            value={detail}
            onChange={(e) => setDetail(e.target.value)}
            placeholder="When the instructions show an example run, compare the student's output with it line by line."
            maxLength={2000}
            rows={7}
            className="field min-h-[170px] resize-y py-2 leading-relaxed"
          />
          <span className="text-[11px] text-ink-muted">{detail.trim().length} / 2000 characters</span>
        </label>
        {tried && missing && <p className="text-xs text-danger">Enter a title and a text.</p>}
        {failed && <ErrorPanel error={failed} />}
      </form>
    </Modal>
  );
}

function IconButton({ label, danger, onClick, children }: { label: string; danger?: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={label}
      title={label}
      className={`flex h-7 w-7 items-center justify-center rounded-md text-ink-muted transition-colors hover:bg-paper-dark ${
        danger ? "hover:text-danger" : "hover:text-ink"
      }`}
    >
      {children}
    </button>
  );
}

// ---------- 2. Course guidance ----------

export function RubricGuidancePage() {
  const { courseId = "" } = useParams();
  const [editing, setEditing] = useState(false);

  return (
    <LayerPage layerKey="guidance" actions={<Chip tone="outline">This course only</Chip>}>
      {(data) => (
        <>
          <div className="grid gap-4 lg:grid-cols-[minmax(0,1.5fr)_minmax(0,1fr)]">
            {data.courseGuidance ? (
              <Card>
                <CardHeader
                  icon={<NotebookPen />}
                  title="How to grade this course"
                  subtitle={`In use from the next grading · saved ${formatDate(data.courseGuidanceUpdatedAt)}`}
                  actions={
                    <Button small icon={<Pencil />} onClick={() => setEditing(true)}>
                      Edit
                    </Button>
                  }
                />
                <p className="whitespace-pre-line px-[18px] py-4 text-[13.5px] leading-relaxed">{data.courseGuidance}</p>
              </Card>
            ) : (
              <Card>
                <CardHeader
                  icon={<NotebookPen />}
                  title="Not written yet"
                  subtitle="This course is graded with the built-in approach and your rules only"
                  actions={
                    <Button small variant="primary" icon={<Pencil />} onClick={() => setEditing(true)}>
                      Write guidance
                    </Button>
                  }
                />
                <div className="flex flex-col gap-3 px-[18px] py-4">
                  <div className="rounded-lg border border-dashed border-ink-faint bg-paper/60 px-3.5 py-3 text-[13px] leading-relaxed text-ink-muted">
                    <div className="eyebrow mb-1">For example</div>
                    “These are second-year students. Judge mainly whether the program works and the classes are designed sensibly.
                    Don’t be strict about formatting; mention it as a suggestion. Be encouraging in the feedback.”
                  </div>
                  <p className="text-xs text-ink-muted">
                    One paragraph is enough. It sets the tone for this group, for example more lenient for C++ basics than for
                    Advanced Java.
                  </p>
                </div>
              </Card>
            )}
            <Card>
              <CardHeader title="What to write here" subtitle="Anything true for every assignment of this course" />
              <ul className="flex flex-col gap-2.5 px-[18px] py-4 text-[13px]">
                <Bullet title="Level of the group">what they already know, their age or year</Bullet>
                <Bullet title="How strict to be">what costs points and what is only a suggestion</Bullet>
                <Bullet title="What matters most">working code, class design, best practices</Bullet>
                <Bullet title="Feedback tone">how to speak to this group</Bullet>
              </ul>
              <p className="border-t border-line px-[18px] py-3 text-[11px] text-ink-muted">
                The grader reads it after the built-in approach and before your rules; your rules still decide the points.
              </p>
            </Card>
          </div>
          {editing && <GuidanceModal courseId={courseId} text={data.courseGuidance ?? ""} onClose={() => setEditing(false)} />}
        </>
      )}
    </LayerPage>
  );
}

function GuidanceModal({ courseId, text, onClose }: { courseId: string; text: string; onClose: () => void }) {
  const [value, setValue] = useState(text);
  const save = useSetCourseGuidance(courseId);
  const removing = value.trim().length === 0 && text.length > 0;

  return (
    <Modal
      title={text ? "Edit course guidance" : "Write course guidance"}
      subtitle={<CourseName courseId={courseId} />}
      onClose={onClose}
      busy={save.isPending}
      size="max-w-2xl"
      footer={
        <>
          <Button onClick={onClose} disabled={save.isPending}>
            Cancel
          </Button>
          <Button variant={removing ? "danger" : "primary"} onClick={() => save.mutate(value.trim(), { onSuccess: onClose })} disabled={save.isPending}>
            {save.isPending ? "Saving…" : removing ? "Remove guidance" : "Save"}
          </Button>
        </>
      }
    >
      <div className="flex shrink-0 flex-col gap-1.5 px-5 py-4">
        <label htmlFor="course-guidance" className="eyebrow">
          How to grade this course
        </label>
        <textarea
          id="course-guidance"
          value={value}
          onChange={(e) => setValue(e.target.value)}
          maxLength={4000}
          rows={14}
          autoFocus
          placeholder="These are second-year students. Judge mainly whether the program works and the classes are designed sensibly…"
          className="field min-h-[300px] resize-y py-2 leading-relaxed"
        />
        <span className="text-[11px] text-ink-muted">
          {value.trim().length} / 4000 characters · applies to the next grading in this course
          {removing ? " · empty removes the guidance" : ""}
        </span>
        {save.isError && <ErrorPanel error={save.error} />}
      </div>
    </Modal>
  );
}

// ---------- 3. Your rules ----------

export function RubricRulesPage() {
  return (
    <LayerPage layerKey="rules">
      {(data) => {
        const free = data.rules.filter((r) => r.effect === "NoDeduction").length;
        return (
          <>
            <StatStrip columns={3}>
              <Stat label="Rules" value={data.rules.length} note="for this course" />
              <Stat label="No deduction" value={free} note="listed as suggestions" tone={free > 0 ? "good" : undefined} />
              <Stat label="Deduction" value={data.rules.length - free} note="fixed points each time" />
            </StatStrip>
            <Explainer>
              A rule is a judgement call the grader must make the same way for every student. Rules override the built-in
              approach; an assignment note can still change a rule for one assignment.
            </Explainer>
            <div className="flex flex-col gap-3">
              {data.rules.map((rule, i) => (
                <RuleCard key={rule.title} n={i + 1} rule={rule} />
              ))}
            </div>
          </>
        );
      }}
    </LayerPage>
  );
}

function RuleCard({ n, rule }: { n: number; rule: PolicyRule }) {
  const deducts = rule.effect === "Deduction";
  return (
    <Card>
      <div className="flex flex-wrap items-start gap-3 px-[18px] py-3.5">
        <Step n={n} />
        <div className="min-w-0 flex-1">
          <h3 className="text-[14px] font-semibold">{rule.title}</h3>
          <p className="mt-0.5 text-xs text-ink-muted">Covers: {rule.covers}</p>
        </div>
        <span
          className={`flex shrink-0 flex-col items-end rounded-lg px-2.5 py-1.5 text-right ${
            deducts ? "bg-warn-tint text-warn-ink" : "bg-success-tint text-success-ink"
          }`}
        >
          <b className="num text-[17px] leading-none">{deducts ? `−${formatPoints(rule.points ?? 0)}` : "0"}</b>
          <span className="mt-0.5 text-[10px] font-semibold uppercase tracking-wide">
            {deducts ? `points ${rule.pointsPer ?? ""}`.trim() : "no deduction"}
          </span>
        </span>
      </div>
      <div className="border-t border-line px-[18px] py-3">
        <div className="eyebrow mb-1">Exact text the grader gets</div>
        <p className="text-[13px] leading-relaxed text-ink-soft">{rule.detail}</p>
      </div>
    </Card>
  );
}

// ---------- 4. Assignment instructions ----------

export function RubricInstructionsPage() {
  const { courseId = "" } = useParams();
  return (
    <LayerPage layerKey="instructions" actions={<Chip tone="outline">From Classroom · read-only</Chip>}>
      {(_, assignments) => {
        const withInstructions = assignments.filter(hasInstructions).length;
        const files = assignments.reduce((sum, a) => sum + a.materials.length, 0);
        return (
          <>
            <StatStrip columns={3}>
              <Stat label="Assignments" value={assignments.length} note="published" />
              <Stat label="With instructions" value={withInstructions} note="statement or files" />
              <Stat label="Files" value={files} note="attached in Classroom" />
            </StatStrip>
            <div className="flex flex-col gap-3">
              {assignments.map((assignment) => (
                <InstructionsCard key={assignment.id} courseId={courseId} assignment={assignment} />
              ))}
            </div>
          </>
        );
      }}
    </LayerPage>
  );
}

function InstructionsCard({ courseId, assignment }: { courseId: string; assignment: Assignment }) {
  const statement = (assignment.description ?? "").trim();
  return (
    <Card>
      <CardHeader
        title={assignment.title}
        subtitle={`${assignment.dueAt ? `due ${formatDay(assignment.dueAt)}` : "no due date"}${
          assignment.maxPoints != null ? ` · ${formatPoints(assignment.maxPoints)} points` : ""
        }`}
        actions={
          <Link to={`/courses/${courseId}/assignments/${assignment.id}/instructions`} className={buttonClass("outline", true)}>
            Open instructions <ArrowRight className="h-3.5 w-3.5" />
          </Link>
        }
      />
      <div className="flex flex-col gap-2.5 px-[18px] py-3">
        {statement ? (
          <p className="line-clamp-3 whitespace-pre-line text-[13px] leading-relaxed text-ink-soft">{statement}</p>
        ) : (
          <p className="text-xs text-ink-muted">No written statement in Classroom.</p>
        )}
        {assignment.materials.length > 0 && (
          <div className="flex flex-wrap gap-1.5">
            {assignment.materials.map((m, i) => (
              <span key={i} className="flex max-w-full items-center gap-1 rounded-md border border-line bg-paper px-2 py-0.5 text-[11px]">
                <Paperclip className="h-3 w-3 shrink-0 text-ink-muted" />
                <span className="truncate">{m.title ?? "Attachment"}</span>
              </span>
            ))}
          </div>
        )}
      </div>
    </Card>
  );
}

// ---------- 5. Assignment notes ----------

export function RubricNotesPage() {
  const { courseId = "" } = useParams();
  return (
    <LayerPage layerKey="notes">
      {(_, assignments) => (
        <div className="grid gap-4 lg:grid-cols-[minmax(0,1.3fr)_minmax(0,1fr)]">
          <Card className="overflow-hidden">
            <CardHeader title="Per assignment" subtitle="Most assignments need none: course guidance and rules cover them" />
            <ul className="divide-y divide-paper-dark">
              {assignments.map((assignment) => (
                <li key={assignment.id}>
                  <Link
                    to={`/courses/${courseId}/assignments/${assignment.id}/instructions`}
                    className="flex items-center gap-3 px-[18px] py-3 hover:bg-paper"
                  >
                    <span className="min-w-0 flex-1">
                      <b className="block truncate text-[13px]">{assignment.title}</b>
                      <span className="text-[11px] text-ink-muted">
                        {assignment.dueAt ? `due ${formatDay(assignment.dueAt)}` : "no due date"}
                      </span>
                    </span>
                    <Chip tone="dashed">No notes</Chip>
                    <ChevronRight className="h-4 w-4 text-ink-faint" />
                  </Link>
                </li>
              ))}
            </ul>
          </Card>
          <Card>
            <CardHeader title="When a note helps" subtitle="Only what differs for this one assignment" />
            <ul className="flex flex-col gap-2.5 px-[18px] py-4 text-[13px]">
              <Bullet title="The point of the task">“The Strategy pattern is what this task tests; deduct if it is missing.”</Bullet>
              <Bullet title="A different rule">“Printed output must match exactly here: −2 points.” Replaces the course rule for this assignment.</Bullet>
              <Bullet title="Something to ignore">“Input validation is not part of this task.”</Bullet>
            </ul>
          </Card>
        </div>
      )}
    </LayerPage>
  );
}

// ---------- Small pieces ----------

function Lead({ icon: Icon }: { icon: LucideIcon }) {
  return (
    <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-[10px] bg-paper text-ink-muted">
      <Icon className="h-5 w-5" />
    </span>
  );
}

function Step({ n, empty = false }: { n: number; empty?: boolean }) {
  return (
    <span
      className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-semibold ${
        empty ? "border border-dashed border-ink-faint text-ink-muted" : "bg-accent-tint text-accent-strong"
      }`}
    >
      {n}
    </span>
  );
}

function Explainer({ children }: { children: ReactNode }) {
  return <p className="max-w-[820px] text-[13px] leading-relaxed text-ink-muted">{children}</p>;
}

function Bullet({ title, children }: { title: string; children: ReactNode }) {
  return (
    <li className="flex gap-2.5">
      <span className="mt-[7px] h-1.5 w-1.5 shrink-0 rounded-full bg-accent" />
      <span>
        <b className="font-semibold">{title}</b>
        <span className="text-ink-muted"> · {children}</span>
      </span>
    </li>
  );
}
