import { type ReactNode, useState } from "react";
import { ArrowDown } from "lucide-react";
import type { Assignment, GradingGuidance } from "../api/client";
import { useAssignmentNotes } from "../api/hooks";
import { InstructionsStatus, useInstructionMaterials } from "./InstructionsCard";
import { FileChips } from "./RubricAssignments";
import { type LayerKey, plural, rubricLayers, ScopeChip, Step } from "./RubricShared";
import { ErrorPanel, Modal, SkeletonLines } from "./ui";

export function BriefModal({
  courseId,
  data,
  assignments,
  initialAssignmentId,
  onClose,
}: {
  courseId: string;
  data: GradingGuidance;
  assignments: Assignment[];
  initialAssignmentId: string | undefined;
  onClose: () => void;
}) {
  const [assignmentId, setAssignmentId] = useState(initialAssignmentId ?? assignments[0]?.id ?? "");
  const assignment = assignments.find((a) => a.id === assignmentId) ?? assignments[0];

  return (
    <Modal
      title="Grader's brief"
      subtitle="Everything the grader is told before it reads a student's work, in the order it gets it"
      onClose={onClose}
      size="max-w-3xl"
      height="h-[90dvh]"
      tinted
      headerExtra={
        assignment && (
          <select
            value={assignment.id}
            onChange={(e) => setAssignmentId(e.target.value)}
            aria-label="Assignment"
            title="The assignment whose instructions and notes are shown"
            className="field h-[30px] max-w-[150px] shrink-0 font-medium sm:max-w-[220px]"
          >
            {assignments.map((a) => (
              <option key={a.id} value={a.id}>
                {a.title}
              </option>
            ))}
          </select>
        )
      }
    >
      <div className="flex shrink-0 flex-col gap-3 px-5 py-4">
        <BriefLayer layerKey="approach" count={plural(data.approach.length, "principle")}>
          <BriefEntries entries={data.approach} empty="Every principle is deleted, so nothing is sent for this layer." />
        </BriefLayer>

        <BriefLayer layerKey="guidance" count={data.courseGuidance ? `${data.courseGuidance.length.toLocaleString()} characters` : "empty"}>
          {data.courseGuidance ? (
            <p className="whitespace-pre-line text-[13px] leading-relaxed">{data.courseGuidance}</p>
          ) : (
            <BriefEmpty>Not written, so nothing is sent for this layer.</BriefEmpty>
          )}
        </BriefLayer>

        <BriefLayer layerKey="rules" count={plural(data.rules.length, "rule")}>
          <BriefEntries entries={data.rules} empty="No rules, so nothing is sent for this layer." />
        </BriefLayer>

        {assignment ? (
          <AssignmentLayers key={assignment.id} courseId={courseId} assignment={assignment} />
        ) : (
          <BriefEmpty>This course has no published assignments, so there are no instructions or notes to show.</BriefEmpty>
        )}

        <div className="flex items-center gap-3 rounded-[10px] border border-dashed border-ink-faint px-4 py-3 text-xs text-ink-muted">
          <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full border border-dashed border-ink-faint bg-white">
            <ArrowDown className="h-3.5 w-3.5" />
          </span>
          then the student's code, files and images, graded against all of the above
        </div>
      </div>
    </Modal>
  );
}

function AssignmentLayers({ courseId, assignment }: { courseId: string; assignment: Assignment }) {
  const state = useInstructionMaterials(courseId, assignment);
  const notes = useAssignmentNotes(courseId, assignment.id);

  return (
    <>
      <BriefLayer layerKey="instructions" count={assignment.title}>
        <div className="flex flex-col gap-2.5">
          {state.text ? (
            <p className="whitespace-pre-line text-[13px] leading-relaxed">{state.text}</p>
          ) : (
            <BriefEmpty>No written statement in Classroom{assignment.materials.length > 0 ? "; the task is in the attached files" : ""}.</BriefEmpty>
          )}
          <FileChips assignment={assignment} />
          <div className="flex flex-wrap items-center gap-2">
            <InstructionsStatus state={state} />
          </div>
        </div>
      </BriefLayer>

      <BriefLayer layerKey="notes" count={notes.data ? plural(notes.data.length, "note") : assignment.title}>
        {notes.isLoading && <SkeletonLines lines={3} />}
        {notes.isError && <ErrorPanel error={notes.error} />}
        {notes.data && <BriefEntries entries={notes.data} empty="No notes: this assignment is graded with the layers above as they are." />}
      </BriefLayer>
    </>
  );
}

function BriefLayer({ layerKey, count, children }: { layerKey: LayerKey; count: string; children: ReactNode }) {
  const index = rubricLayers.findIndex((l) => l.key === layerKey);
  const layer = rubricLayers[index]!;

  return (
    <section className="surface">
      <div className="flex flex-wrap items-center gap-x-2.5 gap-y-1.5 border-b border-line px-4 py-2.5">
        <Step n={index + 1} />
        <h3 className="text-[13.5px] font-semibold">{layer.label}</h3>
        <ScopeChip scope={layer.scope} />
        <span className="ml-auto truncate text-[11px] text-ink-muted">{count}</span>
      </div>
      <div className="px-4 py-3">{children}</div>
    </section>
  );
}

function BriefEntries({ entries, empty }: { entries: { id: string; title: string; detail: string }[]; empty: string }) {
  if (entries.length === 0) {
    return <BriefEmpty>{empty}</BriefEmpty>;
  }
  return (
    <ol className="flex flex-col gap-3">
      {entries.map((entry, i) => (
        <li key={entry.id} className="flex gap-2.5">
          <span className="num w-4 shrink-0 pt-px text-right text-[11.5px] text-ink-faint">{i + 1}</span>
          <div className="min-w-0">
            <div className="text-[12px] font-semibold text-ink-muted">{entry.title}</div>
            <p className="mt-0.5 whitespace-pre-line text-[13px] leading-relaxed">{entry.detail}</p>
          </div>
        </li>
      ))}
    </ol>
  );
}

function BriefEmpty({ children }: { children: ReactNode }) {
  return <p className="text-xs text-ink-muted">{children}</p>;
}
