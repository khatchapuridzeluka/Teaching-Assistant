import { useState } from "react";
import { BookOpen, Plus } from "lucide-react";
import type { GradingGuidance, GradingPrinciple } from "../api/client";
import { useAddPrinciple, useDeletePrinciple, useUpdatePrinciple } from "../api/hooks";
import { ConfirmDialog } from "./ConfirmDialog";
import { EntryList, EntryRow, LayerAside, ListToolbar, matchesSearch, NoMatches, TitleTextModal, useOpenRows } from "./RubricShared";
import { Button, Card, CardHeader, EmptyState, ErrorPanel } from "./ui";

export function RubricApproach({ courseId, data }: { courseId: string; data: GradingGuidance }) {
  const [editing, setEditing] = useState<GradingPrinciple | "new" | null>(null);
  const [deleting, setDeleting] = useState<GradingPrinciple | null>(null);
  const [query, setQuery] = useState("");
  const remove = useDeletePrinciple();
  const rows = useOpenRows(data.approach.length);
  const search = query.trim().toLowerCase();
  const shown = data.approach.filter((principle) => matchesSearch(search, principle.title, principle.detail));
  const allOpen = shown.length > 0 && shown.every((principle) => rows.isOpen(principle.id));

  return (
    <>
      <div className="@container/page">
        <div className="grid items-start gap-4 @4xl/page:grid-cols-[minmax(0,1fr)_300px]">
          <Card className="overflow-hidden">
            <CardHeader
              icon={<BookOpen />}
              title="Built-in approach"
              subtitle={
                search
                  ? `${shown.length} of ${data.approach.length} principles`
                  : `${data.approach.length} ${data.approach.length === 1 ? "principle" : "principles"}, in the order the grader reads them`
              }
              actions={
                <Button small variant="primary" icon={<Plus />} onClick={() => setEditing("new")}>
                  Add principle
                </Button>
              }
            />
            {data.approach.length > 0 && (
              <ListToolbar
                query={query}
                onQuery={setQuery}
                placeholder="Search principles"
                allOpen={allOpen}
                onToggleAll={() => rows.setAll(!allOpen)}
              />
            )}
            {remove.isError && (
              <div className="px-[18px] pt-3">
                <ErrorPanel error={remove.error} />
              </div>
            )}
            {data.approach.length === 0 ? (
              <EmptyState icon={<BookOpen />}>
                <div className="font-semibold text-ink">Every principle is deleted</div>
                The grader starts directly from your course guidance, your rules and the instructions.
                <div className="mt-3">
                  <Button small variant="primary" icon={<Plus />} onClick={() => setEditing("new")}>
                    Add principle
                  </Button>
                </div>
              </EmptyState>
            ) : shown.length === 0 ? (
              <NoMatches query={query} onClear={() => setQuery("")} />
            ) : (
              <EntryList name="Principle">
                {shown.map((principle) => (
                  <EntryRow
                    key={principle.id}
                    n={data.approach.indexOf(principle) + 1}
                    title={principle.title}
                    detail={principle.detail}
                    updatedAt={principle.updatedAt}
                    open={search !== "" || rows.isOpen(principle.id)}
                    onToggle={() => rows.toggle(principle.id)}
                    onEdit={() => setEditing(principle)}
                    onDelete={() => setDeleting(principle)}
                  />
                ))}
              </EntryList>
            )}
          </Card>

          <LayerAside courseId={courseId} layerKey="approach">
            <p>
              The grader always starts from these principles, in every course. They describe how it grades in general; your course
              guidance, rules and assignment notes come after them and win where they disagree.
            </p>
            <p className="rounded-lg bg-paper px-3 py-2 text-xs text-ink-muted">
              A change here applies to the next grading in <b className="font-semibold text-ink-soft">every course</b>. Earlier drafts are
              not changed.
            </p>
          </LayerAside>
        </div>
      </div>

      {editing && (
        <PrincipleModal principle={editing === "new" ? null : editing} position={data.approach.length + 1} onClose={() => setEditing(null)} />
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
            The grader stops using it from the next grading, in every course. Earlier drafts are not changed, and it stays in the database
            marked as deleted.
          </p>
        </ConfirmDialog>
      )}
    </>
  );
}

function PrincipleModal({ principle, position, onClose }: { principle: GradingPrinciple | null; position: number; onClose: () => void }) {
  const add = useAddPrinciple();
  const update = useUpdatePrinciple();
  return (
    <TitleTextModal
      heading={principle ? "Edit principle" : `Add principle ${position}`}
      subtitle={principle ? "Applies to the next grading in every course" : "Added at the end; applies to the next grading in every course"}
      submitLabel={principle ? "Save" : "Add principle"}
      initialTitle={principle?.title ?? ""}
      initialDetail={principle?.detail ?? ""}
      titlePlaceholder="Check the printed output"
      detailPlaceholder="When the instructions show an example run, compare the student's output with it line by line."
      saving={add.isPending || update.isPending}
      error={add.isError ? add.error : update.isError ? update.error : null}
      onSave={(body) =>
        principle ? update.mutate({ id: principle.id, ...body }, { onSuccess: onClose }) : add.mutate(body, { onSuccess: onClose })
      }
      onClose={onClose}
    />
  );
}
