import { useParams } from "react-router";
import { useAssignments } from "../api/hooks";
import { InstructionsNotes, InstructionsStatus, InstructionsViewer, useInstructionMaterials } from "../components/InstructionsCard";
import { Card, PageHeader, Spinner } from "../components/ui";
import { formatDate } from "../format";

export function InstructionsPage() {
  const { courseId = "", assignmentId = "" } = useParams();
  const assignment = useAssignments(courseId).data?.find((a) => a.id === assignmentId);
  const state = useInstructionMaterials(courseId, assignment);

  if (!assignment) {
    return <Spinner />;
  }

  return (
    <>
      <PageHeader
        title={`${assignment.title} · instructions`}
        subtitle={`Due ${formatDate(assignment.dueAt)} · ${assignment.maxPoints ?? "–"} points`}
        actions={<InstructionsStatus state={state} />}
      />

      <Card>
        <InstructionsNotes state={state} />
        <div className="px-[18px] py-3.5">
          <div className="mb-1 eyebrow">Task statement</div>
          {state.text ? (
            <p className="whitespace-pre-wrap leading-relaxed">{state.text}</p>
          ) : (
            <p className="text-ink-muted">No task statement; the task is in the attached materials.</p>
          )}
        </div>
      </Card>

      {state.materials.length > 0 && (
        <Card className="flex h-[calc(100dvh-150px)] min-h-[420px] flex-col overflow-hidden">
          <InstructionsViewer courseId={courseId} assignmentId={assignmentId} materials={state.materials} initial={state.main} />
        </Card>
      )}
    </>
  );
}
