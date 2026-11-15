import { type KeyboardEvent, type ReactNode, useState } from "react";
import { Link } from "react-router";
import { ArrowLeft, ArrowRight, ClipboardList, FileText, Paperclip, Plus } from "lucide-react";
import type { Assignment, AssignmentNote, GradingGuidance } from "../api/client";
import { useAddNote, useAssignmentNotes, useDeleteNote, useUpdateNote } from "../api/hooks";
import { formatDay, formatPoints } from "../format";
import { DueChip } from "./bits";
import { ConfirmDialog } from "./ConfirmDialog";
import { instructionsPath, InstructionsNotes, InstructionsStatus, useInstructionMaterials } from "./InstructionsCard";
import { hasInstructions } from "./RubricOverview";
import { Bullet, EntryList, EntryRow, matchesSearch, plural, rubricPath, type RubricTab, TitleTextModal, useOpenRows } from "./RubricShared";
import { Button, buttonClass, Card, Chip, CountTabs, EmptyState, ErrorPanel, SearchField, Spinner } from "./ui";

type Filter = "all" | "notes" | "missing";

const filterLabels: Record<Filter, string> = { all: "All", notes: "With notes", missing: "No instructions" };

type WorkspaceProps = {
  courseId: string;
  data: GradingGuidance;
  assignments: Assignment[];
  selectedId: string | undefined;
  opened: boolean;
  onSelect: (assignmentId: string) => void;
};

export function RubricAssignments(props: WorkspaceProps) {
  if (props.assignments.length === 0) {
    return (
      <Card>
        <EmptyState icon={<ClipboardList />}>
          <div className="font-semibold text-ink">No published assignments</div>
          Instructions and notes appear here once this course has assignments in Classroom.
        </EmptyState>
      </Card>
    );
  }
  return <Workspace {...props} />;
}

function Workspace({ courseId, data, assignments, selectedId, opened, onSelect }: WorkspaceProps) {
  const [query, setQuery] = useState("");
  const [filter, setFilter] = useState<Filter>("all");
  const [showDetail, setShowDetail] = useState(opened);
  const [editing, setEditing] = useState<AssignmentNote | "new" | null>(null);
  const [deleting, setDeleting] = useState<AssignmentNote | null>(null);
  const selected = assignments.find((a) => a.id === selectedId) ?? assignments[0]!;
  const notes = useAssignmentNotes(courseId, selected.id);
  const remove = useDeleteNote();

  const noteCounts = new Map(data.notes.map((n) => [n.assignmentId, n.count]));
  const matches: Record<Filter, (a: Assignment) => boolean> = {
    all: () => true,
    notes: (a) => (noteCounts.get(a.id) ?? 0) > 0,
    missing: (a) => !hasInstructions(a),
  };
  const search = query.trim().toLowerCase();
  const rows = assignments.filter((a) => matches[filter](a) && matchesSearch(search, a.title, a.description));

  function select(assignment: Assignment, open = true) {
    onSelect(assignment.id);
    if (open) {
      setShowDetail(true);
    }
  }

  function onKeyDown(e: KeyboardEvent<HTMLUListElement>) {
    if (e.key !== "ArrowDown" && e.key !== "ArrowUp") {
      return;
    }
    e.preventDefault();
    const index = rows.findIndex((a) => a.id === selected.id);
    const next = rows[Math.min(rows.length - 1, Math.max(0, index + (e.key === "ArrowDown" ? 1 : -1)))];
    if (next) {
      select(next, false);
      document.getElementById(`rubric-assignment-${next.id}`)?.focus();
    }
  }

  return (
    <>
      <Card className="@container/workspace flex h-[calc(100dvh-250px)] min-h-[480px] overflow-hidden lg:h-[calc(100dvh-206px)]">
        <aside
          className={`w-full shrink-0 flex-col bg-white @2xl/workspace:flex @2xl/workspace:w-[270px] @2xl/workspace:border-r @2xl/workspace:border-line @4xl/workspace:w-[310px] ${
            showDetail ? "hidden" : "flex"
          }`}
        >
          <div className="flex shrink-0 flex-col gap-2 border-b border-line p-2.5">
            <CountTabs
              label="Filter assignments"
              value={filter}
              onChange={setFilter}
              options={(Object.keys(filterLabels) as Filter[]).map((key) => ({
                value: key,
                label: filterLabels[key],
                count: assignments.filter(matches[key]).length,
              }))}
            />
            <SearchField value={query} onChange={setQuery} placeholder="Search assignments" />
          </div>

          {rows.length === 0 ? (
            <div className="px-4 py-8 text-center text-xs text-ink-muted">No assignments match.</div>
          ) : (
            <ul className="min-h-0 flex-1 overflow-y-auto" onKeyDown={onKeyDown} aria-label="Assignments">
              {rows.map((assignment) => {
                const isSelected = assignment.id === selected.id;
                const count = noteCounts.get(assignment.id) ?? 0;
                return (
                  <li key={assignment.id} className="border-b border-paper-dark">
                    <button
                      id={`rubric-assignment-${assignment.id}`}
                      onClick={() => select(assignment)}
                      aria-current={isSelected}
                      className={`flex w-full items-center gap-2.5 border-l-[3px] py-2.5 pl-3 pr-3 text-left ${
                        isSelected ? "border-l-accent bg-accent-tint/60" : "border-l-transparent hover:bg-paper"
                      }`}
                    >
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-[13px] font-semibold">{assignment.title}</span>
                        <span className="flex items-center gap-2 text-[11px] text-ink-muted">
                          <span>{assignment.dueAt ? `due ${formatDay(assignment.dueAt)}` : "no due date"}</span>
                          <span className="flex items-center gap-0.5" title={`${plural(assignment.materials.length, "file")} attached in Classroom`}>
                            <Paperclip className="h-3 w-3" />
                            {assignment.materials.length}
                          </span>
                        </span>
                      </span>
                      {!hasInstructions(assignment) ? (
                        <Chip tone="warn">No instructions</Chip>
                      ) : (
                        count > 0 && <Chip tone="info">{plural(count, "note")}</Chip>
                      )}
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
        </aside>

        <div className={`min-w-0 flex-1 ${showDetail ? "flex" : "hidden @2xl/workspace:flex"}`}>
          <AssignmentDetail
            key={selected.id}
            courseId={courseId}
            data={data}
            assignment={selected}
            notes={notes}
            removeError={remove.isError ? remove.error : null}
            onBack={() => setShowDetail(false)}
            onAdd={() => setEditing("new")}
            onEdit={setEditing}
            onDelete={setDeleting}
          />
        </div>
      </Card>

      {editing && (
        <NoteModal
          courseId={courseId}
          assignmentId={selected.id}
          note={editing === "new" ? null : editing}
          position={(notes.data?.length ?? 0) + 1}
          onClose={() => setEditing(null)}
        />
      )}
      {deleting && (
        <ConfirmDialog
          title={`Delete "${deleting.title}"?`}
          confirmLabel="Delete note"
          danger
          onCancel={() => setDeleting(null)}
          onConfirm={() => {
            remove.mutate(deleting.id);
            setDeleting(null);
          }}
        >
          <p>
            The grader stops using it from this assignment's next grading. Earlier drafts are not changed, and it stays in the database
            marked as deleted.
          </p>
        </ConfirmDialog>
      )}
    </>
  );
}

function AssignmentDetail({
  courseId,
  data,
  assignment,
  notes,
  removeError,
  onBack,
  onAdd,
  onEdit,
  onDelete,
}: {
  courseId: string;
  data: GradingGuidance;
  assignment: Assignment;
  notes: ReturnType<typeof useAssignmentNotes>;
  removeError: unknown;
  onBack: () => void;
  onAdd: () => void;
  onEdit: (note: AssignmentNote) => void;
  onDelete: (note: AssignmentNote) => void;
}) {
  const state = useInstructionMaterials(courseId, assignment);
  const [whole, setWhole] = useState(false);
  const list = notes.data ?? [];
  const rows = useOpenRows(list.length);
  const long = state.text.length > 420 || state.text.split("\n").length > 6;

  return (
    <section className="flex min-w-0 flex-1 flex-col overflow-y-auto">
      <header className="flex flex-wrap items-center gap-x-3 gap-y-2 border-b border-line px-[18px] py-3">
        <button type="button" onClick={onBack} className="flex w-full items-center gap-1 text-xs font-semibold text-accent @2xl/workspace:hidden">
          <ArrowLeft className="h-3.5 w-3.5" /> All assignments
        </button>
        <div className="min-w-0 flex-1">
          <h2 className="truncate font-display text-[19px] font-semibold leading-tight">{assignment.title}</h2>
          <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
            <DueChip dueAt={assignment.dueAt} />
            {assignment.maxPoints != null && <Chip tone="outline">{formatPoints(assignment.maxPoints)} points</Chip>}
          </div>
        </div>
        <Link to={`/courses/${courseId}/assignments/${assignment.id}`} className={buttonClass("outline", true)}>
          Open assignment <ArrowRight className="h-3.5 w-3.5" />
        </Link>
      </header>

      <div className="flex flex-wrap items-center gap-x-2 gap-y-1.5 border-b border-line bg-paper/50 px-[18px] py-2.5 text-xs text-ink-muted">
        <span className="mr-1">Sent first, with every assignment of this course</span>
        <LayerLink courseId={courseId} tab="approach" n={1} label="Built-in approach" value={String(data.approach.length)} />
        <LayerLink courseId={courseId} tab="guidance" n={2} label="Course guidance" value={data.courseGuidance ? "written" : "empty"} />
        <LayerLink courseId={courseId} tab="rules" n={3} label="Your rules" value={String(data.rules.length)} />
      </div>

      <LayerSection
        n={4}
        icon={<FileText />}
        title="Instructions"
        tag={<Chip tone="outline">From Classroom · read-only</Chip>}
        actions={
          <Link to={instructionsPath(courseId, assignment.id)} className="flex items-center gap-1 text-xs font-semibold text-accent hover:underline">
            Open instructions <ArrowRight className="h-3.5 w-3.5" />
          </Link>
        }
      >
        <InstructionsNotes state={state} />
        <div className="flex flex-col gap-2.5 px-[18px] pb-4 pt-1">
          {state.text ? (
            <div>
              <p className={`max-w-[88ch] whitespace-pre-line text-[13px] leading-relaxed text-ink-soft ${whole ? "" : "line-clamp-5"}`}>{state.text}</p>
              {long && (
                <button type="button" onClick={() => setWhole(!whole)} aria-expanded={whole} className="mt-1 text-xs font-semibold text-accent hover:underline">
                  {whole ? "Show less" : "Show the whole statement"}
                </button>
              )}
            </div>
          ) : (
            <p className="text-xs text-ink-muted">
              No written statement in Classroom{assignment.materials.length > 0 ? "; the task is in the attached files" : ""}.
            </p>
          )}
          <FileChips assignment={assignment} />
          <div className="flex flex-wrap items-center gap-2">
            <InstructionsStatus state={state} />
          </div>
        </div>
      </LayerSection>

      <LayerSection
        n={5}
        icon={<ClipboardList />}
        title={list.length > 0 ? `Notes · ${list.length}` : "Notes"}
        tag={<Chip tone="outline">Only this assignment</Chip>}
        actions={
          <Button small variant="primary" icon={<Plus />} onClick={onAdd}>
            Add note
          </Button>
        }
      >
        {notes.isLoading && (
          <div className="px-[18px] pb-4">
            <Spinner />
          </div>
        )}
        {notes.isError && (
          <div className="px-[18px] pb-4">
            <ErrorPanel error={notes.error} />
          </div>
        )}
        {removeError != null && (
          <div className="px-[18px] pb-3">
            <ErrorPanel error={removeError} />
          </div>
        )}
        {notes.data &&
          (list.length === 0 ? (
            <div className="px-[18px] pb-4 pt-1">
              <div className="rounded-lg border border-dashed border-ink-faint bg-paper/50 px-4 py-3.5">
                <div className="text-[13px] font-semibold">No notes for this assignment</div>
                <p className="mt-0.5 text-xs text-ink-muted">It is graded with the layers above as they are. Add a note only for what differs here:</p>
                <ul className="mt-2.5 flex flex-col gap-2 text-[13px]">
                  <Bullet title="The point of the task">“The Strategy pattern is what this task tests; deduct if it is missing.”</Bullet>
                  <Bullet title="A different rule">
                    “Printed output must match exactly here: −2 points.” Wins over the course rule for this assignment.
                  </Bullet>
                  <Bullet title="Something to ignore">“Input validation is not part of this task.”</Bullet>
                </ul>
              </div>
            </div>
          ) : (
            <div className="mt-1 border-t border-line">
              <EntryList name="Note">
                {list.map((note, i) => (
                  <EntryRow
                    key={note.id}
                    n={i + 1}
                    title={note.title}
                    detail={note.detail}
                    updatedAt={note.updatedAt}
                    open={rows.isOpen(note.id)}
                    onToggle={() => rows.toggle(note.id)}
                    onEdit={() => onEdit(note)}
                    onDelete={() => onDelete(note)}
                  />
                ))}
              </EntryList>
              <p className="border-t border-paper-dark px-[18px] py-2.5 text-[11px] text-ink-muted">
                Read last, only when this assignment is graded; where a note differs from the layers above, the note wins.
              </p>
            </div>
          ))}
      </LayerSection>
    </section>
  );
}

function LayerLink({ courseId, tab, n, label, value }: { courseId: string; tab: RubricTab; n: number; label: string; value: string }) {
  return (
    <Link
      to={rubricPath(courseId, tab)}
      className="inline-flex h-6 items-center gap-1.5 rounded-md border border-line bg-white px-2 text-[11px] font-semibold text-ink-soft transition-colors hover:border-accent hover:text-accent-strong"
    >
      <span className="num text-ink-faint">{n}</span>
      {label}
      <span className="font-normal text-ink-muted">· {value}</span>
    </Link>
  );
}

function LayerSection({
  n,
  icon,
  title,
  tag,
  actions,
  children,
}: {
  n: number;
  icon: ReactNode;
  title: string;
  tag: ReactNode;
  actions: ReactNode;
  children: ReactNode;
}) {
  return (
    <section className="border-b border-line last:border-b-0">
      <div className="flex flex-wrap items-center gap-x-2.5 gap-y-1.5 px-[18px] pb-2 pt-3.5">
        <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-md bg-paper text-ink-muted [&>svg]:h-4 [&>svg]:w-4">{icon}</span>
        <h3 className="text-[14px] font-semibold">{title}</h3>
        <Chip tone="ink">Layer {n}</Chip>
        {tag}
        <span className="ml-auto flex items-center gap-2">{actions}</span>
      </div>
      {children}
    </section>
  );
}

export function FileChips({ assignment }: { assignment: Assignment }) {
  if (assignment.materials.length === 0) {
    return null;
  }
  return (
    <div className="flex flex-wrap gap-1.5">
      {assignment.materials.map((material, i) => (
        <span key={i} className="flex max-w-full items-center gap-1 rounded-md border border-line bg-paper px-2 py-0.5 text-[11px]">
          <Paperclip className="h-3 w-3 shrink-0 text-ink-muted" />
          <span className="truncate">{material.title ?? "Attachment"}</span>
        </span>
      ))}
    </div>
  );
}

function NoteModal({
  courseId,
  assignmentId,
  note,
  position,
  onClose,
}: {
  courseId: string;
  assignmentId: string;
  note: AssignmentNote | null;
  position: number;
  onClose: () => void;
}) {
  const add = useAddNote(courseId, assignmentId);
  const update = useUpdateNote();
  return (
    <TitleTextModal
      heading={note ? "Edit note" : `Add note ${position}`}
      subtitle={note ? "Applies to this assignment's next grading" : "Added at the end; applies to this assignment's next grading"}
      submitLabel={note ? "Save" : "Add note"}
      initialTitle={note?.title ?? ""}
      initialDetail={note?.detail ?? ""}
      titlePlaceholder="The Strategy pattern is the point"
      detailPlaceholder="This task tests the Strategy pattern: the algorithms must be behind one interface and chosen at runtime. Deduct the design points of task 2 if they are not."
      saving={add.isPending || update.isPending}
      error={add.isError ? add.error : update.isError ? update.error : null}
      onSave={(body) => (note ? update.mutate({ id: note.id, ...body }, { onSuccess: onClose }) : add.mutate(body, { onSuccess: onClose }))}
      onClose={onClose}
    />
  );
}
