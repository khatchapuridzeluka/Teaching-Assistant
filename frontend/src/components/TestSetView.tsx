import { type ReactNode, useEffect, useState } from "react";
import type { JobInfo, TestCase, TestCategory, TestRequirement, TestSet } from "../api/client";
import { useApproveTestSet, useSaveTestSet } from "../api/hooks";
import { formatDate } from "../format";
import { Check, CircleCheck, Copy, FileCode2, FlaskConical, ListChecks, Pencil, Plus, RefreshCw, Trash2 } from "lucide-react";
import { TestCodeCard } from "./TestCodeCard";
import { Button, Card, CardHeader, Chip, type ChipTone, ErrorPanel, Notice } from "./ui";

export const categoryInfo: Record<TestCategory, { label: string; formLabel: string; tone: ChipTone }> = {
  Normal: { label: "Normal", formLabel: "Normal cases", tone: "info" },
  Boundary: { label: "Boundary", formLabel: "Boundaries (0, empty, max)", tone: "warn" },
  Invalid: { label: "Invalid", formLabel: "Invalid input and exceptions", tone: "danger" },
  LargeInput: { label: "Large input", formLabel: "Large inputs", tone: "muted" },
  Concurrency: { label: "Concurrency", formLabel: "Concurrency", tone: "mark" },
};

type Draft = { requirements: TestRequirement[]; interface: string; cases: TestCase[] };

const inputClass = "w-full rounded-[7px] border border-line bg-white px-2.5 py-1.5 text-[13px] focus:border-accent focus:outline-none";
const codeClass = `${inputClass} font-mono text-[11.5px] leading-relaxed`;

function toDraft(testSet: TestSet): Draft {
  return { requirements: testSet.requirements, interface: testSet.interface ?? "", cases: testSet.cases };
}

let nextKey = 1;
function newKey(prefix: string) {
  return `${prefix}-new-${nextKey++}`;
}

export function TestSetView({
  testSet,
  courseId,
  assignmentId,
  busy,
  codeJob,
  onRegenerate,
}: {
  testSet: TestSet;
  courseId: string;
  assignmentId: string;
  busy: boolean;
  codeJob: JobInfo | undefined;
  onRegenerate: () => void;
}) {
  const save = useSaveTestSet(courseId, assignmentId);
  const approve = useApproveTestSet(courseId, assignmentId);
  const [draft, setDraft] = useState<Draft>(() => toDraft(testSet));
  const [editingCase, setEditingCase] = useState<string | null>(null);
  const [editingRequirement, setEditingRequirement] = useState<string | null>(null);
  const [editingInterface, setEditingInterface] = useState(false);

  function reset() {
    setDraft(toDraft(testSet));
    setEditingCase(null);
    setEditingRequirement(null);
    setEditingInterface(false);
  }

  useEffect(reset, [testSet]);

  const dirty = JSON.stringify(draft) !== JSON.stringify(toDraft(testSet));
  const approved = testSet.status === "Approved";
  const classDesign = testSet.style === "ClassDesign";
  const hasReference = testSet.request.referenceFiles.length > 0;
  const testable = draft.requirements.filter((r) => r.testable);
  const counts = new Map(draft.requirements.map((r) => [r.id, draft.cases.filter((c) => c.requirementIds.includes(r.id)).length]));
  const covered = testable.filter((r) => (counts.get(r.id) ?? 0) > 0).length;

  function updateCase(id: string, change: Partial<TestCase>) {
    setDraft((d) => ({ ...d, cases: d.cases.map((c) => (c.id === id ? { ...c, ...change } : c)) }));
  }

  function addCase() {
    const id = newKey("T");
    setDraft((d) => ({ ...d, cases: [...d.cases, { id, title: "", category: "Normal", requirementIds: [], steps: "", expected: null }] }));
    setEditingCase(id);
  }

  function duplicateCase(testCase: TestCase) {
    const id = newKey("T");
    setDraft((d) => {
      const at = d.cases.findIndex((c) => c.id === testCase.id);
      const cases = [...d.cases];
      cases.splice(at + 1, 0, { ...testCase, id, title: `${testCase.title} (copy)` });
      return { ...d, cases };
    });
    setEditingCase(id);
  }

  function deleteCase(id: string) {
    setDraft((d) => ({ ...d, cases: d.cases.filter((c) => c.id !== id) }));
  }

  function updateRequirement(id: string, change: Partial<TestRequirement>) {
    setDraft((d) => ({ ...d, requirements: d.requirements.map((r) => (r.id === id ? { ...r, ...change } : r)) }));
  }

  function addRequirement() {
    const id = newKey("R");
    setDraft((d) => ({ ...d, requirements: [...d.requirements, { id, text: "", testable: true, reason: null, testCount: 0 }] }));
    setEditingRequirement(id);
  }

  function deleteRequirement(id: string) {
    setDraft((d) => ({
      ...d,
      requirements: d.requirements.filter((r) => r.id !== id),
      cases: d.cases.map((c) => ({ ...c, requirementIds: c.requirementIds.filter((r) => r !== id) })),
    }));
  }

  function saveChanges() {
    save.mutate({
      testSetId: testSet.id,
      body: { requirements: draft.requirements, interface: classDesign ? draft.interface : null, cases: draft.cases },
    });
  }

  const label = (index: number, prefix: string) => `${prefix}${index + 1}`;
  const requirementLabel = new Map(draft.requirements.map((r, i) => [r.id, label(i, "R")]));

  return (
    <>
      {approved ? (
        <Notice
          tone="success"
          title={`Approved, version ${testSet.version}.`}
          action={<HeaderActions dirty={dirty} busy={busy} saving={save.isPending} saveLabel={`Save as version ${testSet.version + 1}`}
            onSave={saveChanges} onDiscard={reset} onRegenerate={onRegenerate} />}
        >
          {dirty
            ? `Saving your edits makes version ${testSet.version + 1} as a new draft; version ${testSet.version} stays approved until you approve the new one.`
            : "This is the version gradings will use once running tests is built. Its test code is below; editing the cases makes a new draft version."}
        </Notice>
      ) : (
        <Notice
          tone="warn"
          title={`Draft, version ${testSet.version}.`}
          action={
            <HeaderActions dirty={dirty} busy={busy} saving={save.isPending} saveLabel="Save changes" onSave={saveChanges}
              onDiscard={reset} onRegenerate={onRegenerate}
              approve={
                <Button small variant="success" icon={<CircleCheck />} disabled={approve.isPending} onClick={() => approve.mutate(testSet.id)}>
                  {approve.isPending ? "Approving…" : "Approve"}
                </Button>
              }
            />
          }
        >
          {dirty
            ? "You have unsaved changes. Save them before approving."
            : testSet.approvedVersion
              ? `Not used in grading; version ${testSet.approvedVersion} stays approved until you approve this one.`
              : "Not used in grading until you approve it."}{" "}
          Generated {formatDate(testSet.createdAt)} by {testSet.model} as {classDesign ? "class-design tests" : "console tests"}
          {hasReference ? `, with your reference solution (${testSet.request.referenceFiles.map((f) => f.name).join(", ")})` : ""}.
        </Notice>
      )}
      {save.isError && <ErrorPanel error={save.error} />}
      {approve.isError && <ErrorPanel error={approve.error} />}

      <div className="grid grid-cols-1 gap-3 xl:grid-cols-[360px_minmax(0,1fr)]">
        <div className="flex flex-col gap-3">
          <Card>
            <CardHeader
              icon={<ListChecks />}
              title="Requirements"
              subtitle={`${covered} of ${testable.length} testable ${testable.length === 1 ? "requirement has" : "requirements have"} a test`}
            />
            <ul className="divide-y divide-paper-dark">
              {draft.requirements.map((requirement, index) =>
                editingRequirement === requirement.id ? (
                  <RequirementEditor
                    key={requirement.id}
                    label={label(index, "R")}
                    requirement={requirement}
                    onChange={(change) => updateRequirement(requirement.id, change)}
                    onDone={() => setEditingRequirement(null)}
                  />
                ) : (
                  <RequirementRow
                    key={requirement.id}
                    label={label(index, "R")}
                    requirement={requirement}
                    count={counts.get(requirement.id) ?? 0}
                    onEdit={() => setEditingRequirement(requirement.id)}
                    onDelete={() => deleteRequirement(requirement.id)}
                  />
                ),
              )}
            </ul>
            <button onClick={addRequirement} className="flex w-full items-center gap-1.5 border-t border-paper-dark px-[18px] py-2.5 text-xs font-semibold text-accent hover:bg-paper">
              <Plus className="h-3.5 w-3.5" /> Add requirement
            </button>
          </Card>

          {classDesign && (
            <Card>
              <CardHeader
                icon={<FileCode2 />}
                title="Interface"
                subtitle="The tests call only these methods"
                actions={
                  <Button small variant="ghost" icon={editingInterface ? <Check /> : <Pencil />} onClick={() => setEditingInterface((e) => !e)}>
                    {editingInterface ? "Done" : "Edit"}
                  </Button>
                }
              />
              {editingInterface ? (
                <div className="px-[18px] py-3">
                  <textarea
                    value={draft.interface}
                    onChange={(e) => setDraft((d) => ({ ...d, interface: e.target.value }))}
                    rows={Math.min(30, Math.max(8, draft.interface.split("\n").length + 1))}
                    className={codeClass}
                    spellCheck={false}
                  />
                </div>
              ) : (
                <pre className="overflow-x-auto px-[18px] py-3 font-mono text-[11.5px] leading-relaxed text-ink-soft">{draft.interface || "No interface yet."}</pre>
              )}
              <div className="border-t border-paper-dark px-[18px] py-2.5 text-xs text-ink-muted">
                Each student gets a small adapter that maps these methods onto their own classes, so their class and method names don't matter.
              </div>
            </Card>
          )}
        </div>

        <Card className="min-w-0">
          <CardHeader
            icon={<FlaskConical />}
            title="Test cases"
            subtitle={`${draft.cases.length} ${draft.cases.length === 1 ? "case" : "cases"}`}
            actions={
              <Button small icon={<Plus />} onClick={addCase}>
                Add case
              </Button>
            }
          />
          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-left">
              <thead>
                <tr className="border-b border-line bg-paper">
                  <th className="eyebrow px-3 py-2">#</th>
                  <th className="eyebrow px-3 py-2">Case</th>
                  <th className="eyebrow px-3 py-2">{classDesign ? "Steps" : "Input"}</th>
                  <th className="eyebrow px-3 py-2">Expected</th>
                  <th className="px-2 py-2" />
                </tr>
              </thead>
              <tbody className="divide-y divide-paper-dark">
                {draft.cases.map((testCase, index) =>
                  editingCase === testCase.id ? (
                    <CaseEditor
                      key={testCase.id}
                      label={label(index, "T")}
                      testCase={testCase}
                      requirements={draft.requirements}
                      requirementLabel={requirementLabel}
                      classDesign={classDesign}
                      onChange={(change) => updateCase(testCase.id, change)}
                      onDone={() => setEditingCase(null)}
                    />
                  ) : (
                    <CaseRow
                      key={testCase.id}
                      label={label(index, "T")}
                      testCase={testCase}
                      requirementLabel={requirementLabel}
                      hasReference={hasReference}
                      onEdit={() => setEditingCase(testCase.id)}
                      onDuplicate={() => duplicateCase(testCase)}
                      onDelete={() => deleteCase(testCase.id)}
                    />
                  ),
                )}
              </tbody>
            </table>
          </div>
        </Card>
      </div>

      {approved && classDesign && (
        <TestCodeCard code={testSet.code ?? null} job={codeJob} testSetId={testSet.id} courseId={courseId} assignmentId={assignmentId} />
      )}
    </>
  );
}

function HeaderActions({
  dirty,
  busy,
  saving,
  saveLabel,
  onSave,
  onDiscard,
  onRegenerate,
  approve,
}: {
  dirty: boolean;
  busy: boolean;
  saving: boolean;
  saveLabel: string;
  onSave: () => void;
  onDiscard: () => void;
  onRegenerate: () => void;
  approve?: ReactNode;
}) {
  if (dirty) {
    return (
      <div className="flex shrink-0 gap-2">
        <Button small onClick={onDiscard} disabled={saving}>
          Discard
        </Button>
        <Button small variant="primary" icon={<Check />} onClick={onSave} disabled={saving}>
          {saving ? "Saving…" : saveLabel}
        </Button>
      </div>
    );
  }

  return (
    <div className="flex shrink-0 gap-2">
      <Button small icon={<RefreshCw />} onClick={onRegenerate} disabled={busy}>
        Generate again
      </Button>
      {approve}
    </div>
  );
}

function RowActions({ onEdit, onDuplicate, onDelete, label }: { onEdit: () => void; onDuplicate?: () => void; onDelete: () => void; label: string }) {
  return (
    <div className="flex shrink-0 gap-0.5 text-ink-faint">
      <button aria-label={`Edit ${label}`} onClick={onEdit} className="rounded p-1 hover:bg-paper-dark hover:text-ink">
        <Pencil className="h-3.5 w-3.5" />
      </button>
      {onDuplicate && (
        <button aria-label={`Duplicate ${label}`} onClick={onDuplicate} className="rounded p-1 hover:bg-paper-dark hover:text-ink">
          <Copy className="h-3.5 w-3.5" />
        </button>
      )}
      <button aria-label={`Delete ${label}`} onClick={onDelete} className="rounded p-1 hover:bg-danger-tint hover:text-danger-ink">
        <Trash2 className="h-3.5 w-3.5" />
      </button>
    </div>
  );
}

function RequirementRow({
  label,
  requirement,
  count,
  onEdit,
  onDelete,
}: {
  label: string;
  requirement: TestRequirement;
  count: number;
  onEdit: () => void;
  onDelete: () => void;
}) {
  const missing = requirement.testable && count === 0;
  return (
    <li className={`group flex items-start justify-between gap-2 px-[18px] py-2 ${missing ? "bg-danger-tint/40" : ""}`}>
      <div className="min-w-0">
        <span className="mr-1.5 font-semibold text-ink-muted">{label}</span>
        {requirement.text || <span className="text-ink-faint">No text</span>}
        {!requirement.testable && requirement.reason && <div className="mt-0.5 text-xs text-ink-muted">{requirement.reason}</div>}
      </div>
      <div className="flex shrink-0 items-center gap-1">
        {!requirement.testable ? (
          <Chip tone="muted">Grader judges</Chip>
        ) : missing ? (
          <Chip tone="danger">No test</Chip>
        ) : (
          <Chip tone="success">
            {count} {count === 1 ? "test" : "tests"}
          </Chip>
        )}
        <div className="opacity-0 transition-opacity group-hover:opacity-100 focus-within:opacity-100">
          <RowActions label={label} onEdit={onEdit} onDelete={onDelete} />
        </div>
      </div>
    </li>
  );
}

function RequirementEditor({
  label,
  requirement,
  onChange,
  onDone,
}: {
  label: string;
  requirement: TestRequirement;
  onChange: (change: Partial<TestRequirement>) => void;
  onDone: () => void;
}) {
  return (
    <li className="flex flex-col gap-2 bg-accent-tint/30 px-[18px] py-2.5">
      <div className="flex items-center gap-2">
        <span className="font-semibold text-ink-muted">{label}</span>
        <input autoFocus value={requirement.text} onChange={(e) => onChange({ text: e.target.value })} className={inputClass}
          placeholder="Withdrawing more than the balance is refused" />
      </div>
      <label className="flex items-center gap-2 text-xs">
        <input type="checkbox" checked={requirement.testable} onChange={(e) => onChange({ testable: e.target.checked })} className="accent-accent" />
        Can be tested automatically
      </label>
      {!requirement.testable && (
        <input value={requirement.reason ?? ""} onChange={(e) => onChange({ reason: e.target.value })} className={inputClass}
          placeholder="Why only the grader can judge it" />
      )}
      <div className="flex justify-end">
        <Button small icon={<Check />} onClick={onDone}>
          Done
        </Button>
      </div>
    </li>
  );
}

function CaseRow({
  label,
  testCase,
  requirementLabel,
  hasReference,
  onEdit,
  onDuplicate,
  onDelete,
}: {
  label: string;
  testCase: TestCase;
  requirementLabel: Map<string, string>;
  hasReference: boolean;
  onEdit: () => void;
  onDuplicate: () => void;
  onDelete: () => void;
}) {
  const category = categoryInfo[testCase.category];
  return (
    <tr className="group align-top">
      <td className="px-3 py-2.5 text-ink-muted">{label}</td>
      <td className="px-3 py-2.5">
        <div className="font-semibold">{testCase.title || <span className="text-ink-faint">No title</span>}</div>
        <div className="mt-1 flex flex-wrap gap-1">
          <Chip tone={category.tone}>{category.label}</Chip>
          {testCase.requirementIds.map((id) => (
            <Chip key={id} tone="muted">
              {requirementLabel.get(id) ?? id}
            </Chip>
          ))}
        </div>
      </td>
      <td className="px-3 py-2.5">
        <pre className="whitespace-pre-wrap font-mono text-[11.5px] leading-relaxed text-ink-soft">{testCase.steps}</pre>
      </td>
      <td className="px-3 py-2.5">
        {testCase.expected ? (
          <pre className="whitespace-pre-wrap font-mono text-[11.5px] leading-relaxed text-ink-soft">{testCase.expected}</pre>
        ) : hasReference ? (
          <span className="text-xs text-ink-muted">From the reference solution</span>
        ) : (
          <Chip tone="warn">Needs an expected result</Chip>
        )}
      </td>
      <td className="px-2 py-2.5">
        <div className="opacity-0 transition-opacity group-hover:opacity-100 focus-within:opacity-100">
          <RowActions label={label} onEdit={onEdit} onDuplicate={onDuplicate} onDelete={onDelete} />
        </div>
      </td>
    </tr>
  );
}

function CaseEditor({
  label,
  testCase,
  requirements,
  requirementLabel,
  classDesign,
  onChange,
  onDone,
}: {
  label: string;
  testCase: TestCase;
  requirements: TestRequirement[];
  requirementLabel: Map<string, string>;
  classDesign: boolean;
  onChange: (change: Partial<TestCase>) => void;
  onDone: () => void;
}) {
  function toggleRequirement(id: string) {
    const has = testCase.requirementIds.includes(id);
    onChange({ requirementIds: has ? testCase.requirementIds.filter((r) => r !== id) : [...testCase.requirementIds, id] });
  }

  return (
    <tr className="align-top bg-accent-tint/30">
      <td className="px-3 py-2.5 text-ink-muted">{label}</td>
      <td className="w-[30%] px-3 py-2.5">
        <input autoFocus value={testCase.title} onChange={(e) => onChange({ title: e.target.value })} className={inputClass} placeholder="Overdraft is refused" />
        <select value={testCase.category} onChange={(e) => onChange({ category: e.target.value as TestCategory })} className={`${inputClass} mt-2`}>
          {(Object.keys(categoryInfo) as TestCategory[]).map((category) => (
            <option key={category} value={category}>
              {categoryInfo[category].label}
            </option>
          ))}
        </select>
        <div className="mt-2 flex flex-wrap gap-1">
          {requirements
            .filter((r) => r.testable)
            .map((r) => {
              const on = testCase.requirementIds.includes(r.id);
              return (
                <button
                  key={r.id}
                  title={r.text}
                  onClick={() => toggleRequirement(r.id)}
                  className={`rounded-[5px] border px-1.5 py-0.5 text-[11px] font-semibold ${on ? "border-accent bg-accent-tint text-accent-strong" : "border-line text-ink-muted hover:bg-paper"}`}
                >
                  {requirementLabel.get(r.id)}
                </button>
              );
            })}
        </div>
      </td>
      <td className="px-3 py-2.5">
        <textarea
          value={testCase.steps}
          onChange={(e) => onChange({ steps: e.target.value })}
          rows={Math.min(16, Math.max(4, testCase.steps.split("\n").length + 1))}
          className={codeClass}
          spellCheck={false}
          placeholder={classDesign ? "a = newIntList()\na.addLast(4)\na.get(0)" : "3\n5 7 9"}
        />
      </td>
      <td className="px-3 py-2.5">
        <textarea
          value={testCase.expected ?? ""}
          onChange={(e) => onChange({ expected: e.target.value })}
          rows={Math.min(16, Math.max(4, (testCase.expected ?? "").split("\n").length + 1))}
          className={codeClass}
          spellCheck={false}
          placeholder="get(0) returns 4"
        />
      </td>
      <td className="px-2 py-2.5">
        <Button small icon={<Check />} onClick={onDone}>
          Done
        </Button>
      </td>
    </tr>
  );
}
