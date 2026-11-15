import { useState } from "react";
import { Gavel, Plus } from "lucide-react";
import type { GradingGuidance, PolicyEffect, PolicyRule } from "../api/client";
import { useAddRule, useDeleteRule, useUpdateRule } from "../api/hooks";
import { formatPoints } from "../format";
import { ConfirmDialog } from "./ConfirmDialog";
import { EntryList, EntryRow, LayerAside, ListToolbar, matchesSearch, NoMatches, useOpenRows } from "./RubricShared";
import { Button, Card, CardHeader, Chip, EmptyState, ErrorPanel, Modal, Segmented } from "./ui";

type EffectFilter = "all" | PolicyEffect;

export function RubricRules({ courseId, data }: { courseId: string; data: GradingGuidance }) {
  const [editing, setEditing] = useState<PolicyRule | "new" | null>(null);
  const [deleting, setDeleting] = useState<PolicyRule | null>(null);
  const [query, setQuery] = useState("");
  const [effect, setEffect] = useState<EffectFilter>("all");
  const remove = useDeleteRule();
  const rows = useOpenRows(data.rules.length);
  const search = query.trim().toLowerCase();
  const deducting = data.rules.filter((rule) => rule.effect === "Deduction").length;
  const free = data.rules.length - deducting;
  const shown = data.rules.filter(
    (rule) => (effect === "all" || rule.effect === effect) && matchesSearch(search, rule.title, rule.covers, rule.detail),
  );
  const allOpen = shown.length > 0 && shown.every((rule) => rows.isOpen(rule.id));
  const filtered = search !== "" || effect !== "all";

  return (
    <>
      <div className="@container/page">
        <div className="grid items-start gap-4 @4xl/page:grid-cols-[minmax(0,1fr)_300px]">
          <Card className="overflow-hidden">
            <CardHeader
              icon={<Gavel />}
              title="Your rules"
              subtitle={
                filtered
                  ? `${shown.length} of ${data.rules.length} rules`
                  : `${data.rules.length} ${data.rules.length === 1 ? "rule" : "rules"}, applied the same way for every student`
              }
              actions={
                <Button small variant="primary" icon={<Plus />} onClick={() => setEditing("new")}>
                  Add rule
                </Button>
              }
            />
            {data.rules.length > 0 && (
              <ListToolbar query={query} onQuery={setQuery} placeholder="Search rules" allOpen={allOpen} onToggleAll={() => rows.setAll(!allOpen)}>
                <Segmented
                  label="Effect"
                  value={effect}
                  onChange={setEffect}
                  options={[
                    { value: "all", label: "All", count: data.rules.length },
                    { value: "Deduction", label: "Deduct points", count: deducting },
                    { value: "NoDeduction", label: "No deduction", count: free },
                  ]}
                />
              </ListToolbar>
            )}
            {remove.isError && (
              <div className="px-[18px] pt-3">
                <ErrorPanel error={remove.error} />
              </div>
            )}
            {data.rules.length === 0 ? (
              <EmptyState icon={<Gavel />}>
                <div className="font-semibold text-ink">No rules yet</div>
                A rule settles a judgement call once, for example “a typo in printed text is not a mistake” or “a bad variable name costs 5
                points”.
                <div className="mt-3">
                  <Button small variant="primary" icon={<Plus />} onClick={() => setEditing("new")}>
                    Add rule
                  </Button>
                </div>
              </EmptyState>
            ) : shown.length === 0 ? (
              search !== "" ? (
                <NoMatches query={query} onClear={() => setQuery("")} />
              ) : (
                <div className="px-[18px] py-10 text-center text-ink-muted">No rule has this effect.</div>
              )
            ) : (
              <EntryList name="Rule" effect>
                {shown.map((rule) => (
                  <EntryRow
                    key={rule.id}
                    n={data.rules.indexOf(rule) + 1}
                    title={rule.title}
                    meta={<>Covers: {rule.covers}</>}
                    detail={rule.detail}
                    effect={<EffectChip rule={rule} />}
                    updatedAt={rule.updatedAt}
                    open={search !== "" || rows.isOpen(rule.id)}
                    onToggle={() => rows.toggle(rule.id)}
                    onEdit={() => setEditing(rule)}
                    onDelete={() => setDeleting(rule)}
                  />
                ))}
              </EntryList>
            )}
          </Card>

          <LayerAside courseId={courseId} layerKey="rules">
            <p>
              A rule is a judgement call the grader must make the same way for every student. Rules override the built-in approach and
              the course guidance; an assignment note can still change a rule for one assignment.
            </p>
            <dl className="grid grid-cols-3 gap-px overflow-hidden rounded-lg border border-line bg-line text-center">
              <Count label="Rules" value={data.rules.length} />
              <Count label="Deduct" value={deducting} />
              <Count label="No deduction" value={free} />
            </dl>
            <p className="rounded-lg bg-paper px-3 py-2 text-xs text-ink-muted">
              A change here applies to the next grading in <b className="font-semibold text-ink-soft">every course</b>. Earlier drafts are
              not changed.
            </p>
          </LayerAside>
        </div>
      </div>

      {editing && <RuleModal rule={editing === "new" ? null : editing} position={data.rules.length + 1} onClose={() => setEditing(null)} />}
      {deleting && (
        <ConfirmDialog
          title={`Delete "${deleting.title}"?`}
          confirmLabel="Delete rule"
          danger
          onCancel={() => setDeleting(null)}
          onConfirm={() => {
            remove.mutate(deleting.id);
            setDeleting(null);
          }}
        >
          <p>
            The grader stops applying it from the next grading, in every course. Earlier drafts are not changed, and it stays in the
            database marked as deleted.
          </p>
        </ConfirmDialog>
      )}
    </>
  );
}

function Count({ label, value }: { label: string; value: number }) {
  return (
    <div className="bg-white px-1.5 py-2">
      <dd className="num text-[17px] leading-none text-ink">{value}</dd>
      <dt className="mt-1 truncate text-[10.5px] text-ink-muted">{label}</dt>
    </div>
  );
}

export function EffectChip({ rule }: { rule: PolicyRule }) {
  if (rule.effect !== "Deduction") {
    return <Chip tone="success">No deduction</Chip>;
  }
  return (
    <Chip tone="warn" className="min-w-0 max-w-full">
      <span className="truncate" title={`−${formatPoints(rule.points ?? 0)} points ${rule.pointsPer ?? ""}`.trim()}>
        −{formatPoints(rule.points ?? 0)} pts{rule.pointsPer ? ` ${rule.pointsPer}` : ""}
      </span>
    </Chip>
  );
}

function RuleModal({ rule, position, onClose }: { rule: PolicyRule | null; position: number; onClose: () => void }) {
  const [title, setTitle] = useState(rule?.title ?? "");
  const [covers, setCovers] = useState(rule?.covers ?? "");
  const [effect, setEffect] = useState<PolicyEffect>(rule?.effect ?? "NoDeduction");
  const [points, setPoints] = useState(rule?.points != null ? String(rule.points) : "");
  const [pointsPer, setPointsPer] = useState(rule?.pointsPer ?? "");
  const [detail, setDetail] = useState(rule?.detail ?? "");
  const [tried, setTried] = useState(false);
  const add = useAddRule();
  const update = useUpdateRule();
  const saving = add.isPending || update.isPending;
  const failed = add.isError ? add.error : update.isError ? update.error : null;

  const deducts = effect === "Deduction";
  const value = Number(points);
  const pointsInvalid = deducts && !(points.trim() !== "" && Number.isFinite(value) && value > 0 && value <= 100);
  const missing = !title.trim() || !covers.trim() || !detail.trim();

  function save() {
    setTried(true);
    if (missing || pointsInvalid) {
      return;
    }
    const body = {
      title: title.trim(),
      effect,
      points: deducts ? value : null,
      pointsPer: deducts && pointsPer.trim() ? pointsPer.trim() : null,
      covers: covers.trim(),
      detail: detail.trim(),
    };
    if (rule) {
      update.mutate({ id: rule.id, ...body }, { onSuccess: onClose });
    } else {
      add.mutate(body, { onSuccess: onClose });
    }
  }

  return (
    <Modal
      title={rule ? "Edit rule" : `Add rule ${position}`}
      subtitle={rule ? "Applies to the next grading in every course" : "Added at the end; applies to the next grading in every course"}
      onClose={onClose}
      busy={saving}
      size="max-w-xl"
      footer={
        <>
          <Button onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button variant="primary" onClick={save} disabled={saving}>
            {saving ? "Saving…" : rule ? "Save" : "Add rule"}
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
            placeholder="Changing a name the instructions fix"
            autoFocus
          />
        </label>
        <label className="flex flex-col gap-1.5">
          <span className="eyebrow">What it covers</span>
          <input
            value={covers}
            onChange={(e) => setCovers(e.target.value)}
            maxLength={300}
            className="field"
            placeholder="A class, method or variable whose exact name the instructions give"
          />
          <span className="text-[11px] text-ink-muted">Short, for you; the grader does not see it</span>
        </label>
        <div className="flex flex-col gap-1.5">
          <span className="eyebrow">Effect</span>
          <div className="flex flex-wrap items-center gap-2">
            <Segmented
              label="Effect"
              value={effect}
              onChange={setEffect}
              options={[
                { value: "NoDeduction", label: "No deduction" },
                { value: "Deduction", label: "Deduct points" },
              ]}
            />
            {deducts && (
              <>
                <span className="text-xs text-ink-muted">−</span>
                <input
                  value={points}
                  onChange={(e) => setPoints(e.target.value)}
                  inputMode="decimal"
                  aria-label="Points"
                  className="field w-20"
                  placeholder="3"
                />
                <span className="text-xs text-ink-muted">points</span>
                <input
                  value={pointsPer}
                  onChange={(e) => setPointsPer(e.target.value)}
                  maxLength={40}
                  aria-label="Counted per"
                  className="field w-36"
                  placeholder="per name"
                />
              </>
            )}
          </div>
          <span className="text-[11px] text-ink-muted">
            {deducts
              ? "Shown in the list; the text below is what tells the grader how many points to take"
              : "Listed as a suggestion with severity Style; costs no points"}
          </span>
        </div>
        <label className="flex flex-col gap-1.5">
          <span className="eyebrow">What the grader is told</span>
          <textarea
            value={detail}
            onChange={(e) => setDetail(e.target.value)}
            maxLength={2000}
            rows={6}
            placeholder="When the instructions give the exact name of a class, method or variable and the student used another one, deduct 3 points for each such name and say so in the mistake."
            className="field min-h-[150px] resize-y py-2 leading-relaxed"
          />
          <span className="text-[11px] text-ink-muted">{detail.trim().length} / 2000 characters</span>
        </label>
        {tried && missing && <p className="text-xs text-danger">Enter a title, what it covers and the text the grader gets.</p>}
        {tried && !missing && pointsInvalid && <p className="text-xs text-danger">Enter points between 0 and 100.</p>}
        {failed && <ErrorPanel error={failed} />}
      </form>
    </Modal>
  );
}
