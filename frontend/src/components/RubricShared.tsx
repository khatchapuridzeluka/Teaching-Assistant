import { type ReactNode, useState } from "react";
import { Link } from "react-router";
import {
  BookOpen,
  ChevronDown,
  ChevronsDownUp,
  ChevronsUpDown,
  ClipboardList,
  FileText,
  Gavel,
  type LucideIcon,
  NotebookPen,
  Pencil,
  Trash2,
} from "lucide-react";
import { formatDate, formatDay } from "../format";
import { Button, Card, Chip, type ChipTone, ErrorPanel, Modal, SearchField } from "./ui";

export type RubricTab = "overview" | "approach" | "guidance" | "rules" | "assignments";
export type LayerKey = "approach" | "guidance" | "rules" | "instructions" | "notes";
export type LayerScope = "Every course" | "This course" | "Per assignment";
export type LayerInfo = { key: LayerKey; tab: RubricTab; label: string; icon: LucideIcon; scope: LayerScope; purpose: string };

export const rubricLayers: LayerInfo[] = [
  {
    key: "approach",
    tab: "approach",
    label: "Built-in approach",
    icon: BookOpen,
    scope: "Every course",
    purpose: "How the grader works in every course; part of the tool",
  },
  {
    key: "guidance",
    tab: "guidance",
    label: "Course guidance",
    icon: NotebookPen,
    scope: "This course",
    purpose: "Your own words for this course: how strict to be, what matters most",
  },
  {
    key: "rules",
    tab: "rules",
    label: "Your rules",
    icon: Gavel,
    scope: "Every course",
    purpose: "Judgement calls applied the same way for every student",
  },
  {
    key: "instructions",
    tab: "assignments",
    label: "Assignment instructions",
    icon: FileText,
    scope: "Per assignment",
    purpose: "Each assignment's statement and files, fetched from Classroom",
  },
  {
    key: "notes",
    tab: "assignments",
    label: "Assignment notes",
    icon: ClipboardList,
    scope: "Per assignment",
    purpose: "Extra criteria or a different rule for one assignment",
  },
];

export function rubricPath(courseId: string, tab: RubricTab = "overview", assignmentId?: string) {
  const params = new URLSearchParams();
  if (tab !== "overview") {
    params.set("tab", tab);
  }
  if (assignmentId) {
    params.set("assignment", assignmentId);
  }
  const query = params.toString();
  return `/courses/${courseId}/rubric${query ? `?${query}` : ""}`;
}

export function matchesSearch(search: string, ...texts: (string | null | undefined)[]) {
  return search === "" || texts.some((text) => (text ?? "").toLowerCase().includes(search));
}

export function plural(count: number, one: string, many = `${one}s`) {
  return `${count} ${count === 1 ? one : many}`;
}

const scopeTones: Record<LayerScope, ChipTone> = { "Every course": "muted", "This course": "tint", "Per assignment": "outline" };

export function ScopeChip({ scope }: { scope: LayerScope }) {
  return <Chip tone={scopeTones[scope]}>{scope}</Chip>;
}

export function Step({ n, empty = false }: { n: number; empty?: boolean }) {
  return (
    <span
      className={`num flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs ${
        empty ? "border border-dashed border-ink-faint bg-white text-ink-muted" : "bg-accent-tint text-accent-strong"
      }`}
    >
      {n}
    </span>
  );
}

export function IconButton({ label, danger, onClick, children }: { label: string; danger?: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={label}
      title={label}
      className={`flex h-7 w-7 items-center justify-center rounded-md text-ink-muted transition-colors hover:bg-paper-dark [&>svg]:h-3.5 [&>svg]:w-3.5 ${
        danger ? "hover:text-danger" : "hover:text-ink"
      }`}
    >
      {children}
    </button>
  );
}

export function Bullet({ title, children }: { title: string; children: ReactNode }) {
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

export function LayerAside({ courseId, layerKey, children }: { courseId: string; layerKey: LayerKey; children: ReactNode }) {
  const index = rubricLayers.findIndex((l) => l.key === layerKey);
  const layer = rubricLayers[index]!;

  return (
    <Card className="@4xl/page:sticky @4xl/page:top-0">
      <div className="flex flex-col gap-3 px-[18px] py-4">
        <div className="flex flex-wrap items-center gap-1.5">
          <Chip tone="ink">
            Layer {index + 1} of {rubricLayers.length}
          </Chip>
          <ScopeChip scope={layer.scope} />
        </div>
        <div className="flex flex-col gap-3 text-[13px] leading-relaxed text-ink-soft">{children}</div>
      </div>
      <div className="border-t border-line px-[18px] py-3.5">
        <div className="eyebrow mb-1.5">Reading order</div>
        <ol className="-mx-2 flex flex-col gap-px">
          {rubricLayers.map((l, i) => {
            const current = l.key === layerKey;
            return (
              <li key={l.key}>
                <Link
                  to={rubricPath(courseId, l.tab)}
                  aria-current={current ? "step" : undefined}
                  className={`flex items-center gap-2.5 rounded-md px-2 py-1.5 text-xs transition-colors ${
                    current ? "bg-accent-tint font-semibold text-accent-strong" : "text-ink-muted hover:bg-paper hover:text-ink"
                  }`}
                >
                  <span className={`num w-3 text-center text-[11px] ${current ? "" : "text-ink-faint"}`}>{i + 1}</span>
                  <l.icon className="h-3.5 w-3.5 shrink-0" />
                  <span className="min-w-0 flex-1 truncate">{l.label}</span>
                </Link>
              </li>
            );
          })}
        </ol>
        <p className="mt-2 text-[11px] leading-snug text-ink-muted">Read top to bottom; a later layer wins where two disagree.</p>
      </div>
    </Card>
  );
}

export function useOpenRows(count: number) {
  const [mode, setMode] = useState<"auto" | "all" | "none">("auto");
  const [overrides, setOverrides] = useState<Record<string, boolean>>({});
  const base = mode === "all" || (mode === "auto" && count <= 3);

  return {
    isOpen: (id: string) => overrides[id] ?? base,
    toggle: (id: string) => setOverrides((current) => ({ ...current, [id]: !(current[id] ?? base) })),
    setAll: (open: boolean) => {
      setMode(open ? "all" : "none");
      setOverrides({});
    },
  };
}

export function ListToolbar({
  query,
  onQuery,
  placeholder,
  allOpen,
  onToggleAll,
  children,
}: {
  query: string;
  onQuery: (query: string) => void;
  placeholder: string;
  allOpen: boolean;
  onToggleAll: () => void;
  children?: ReactNode;
}) {
  return (
    <div className="flex flex-wrap items-center gap-2 border-b border-line px-[18px] py-2.5">
      <SearchField value={query} onChange={onQuery} placeholder={placeholder} className="w-full sm:w-[240px]" />
      {children}
      <Button
        variant="ghost"
        small
        className="ml-auto"
        icon={allOpen ? <ChevronsDownUp /> : <ChevronsUpDown />}
        onClick={onToggleAll}
        title={allOpen ? "Show two lines of each text" : "Show every text in full"}
      >
        {allOpen ? "Collapse all" : "Expand all"}
      </Button>
    </div>
  );
}

const plainColumns = "@3xl:grid-cols-[28px_minmax(0,1fr)_64px_88px]";
const effectColumns = "@3xl:grid-cols-[28px_minmax(0,1fr)_minmax(0,150px)_64px_88px]";

export function EntryList({ name, effect, children }: { name: string; effect?: boolean; children: ReactNode }) {
  return (
    <div className="@container">
      <div
        className={`hidden items-center gap-x-3 border-b border-line bg-paper/60 px-[18px] py-1.5 text-[11px] font-semibold text-ink-muted @3xl:grid ${
          effect ? effectColumns : plainColumns
        }`}
      >
        <span className="text-center">#</span>
        <span>{name}</span>
        {effect && <span>Effect</span>}
        <span>Changed</span>
        <span />
      </div>
      <ul className="divide-y divide-paper-dark">{children}</ul>
    </div>
  );
}

export function EntryRow({
  n,
  title,
  meta,
  detail,
  effect,
  updatedAt,
  open,
  onToggle,
  onEdit,
  onDelete,
}: {
  n: number;
  title: string;
  meta?: ReactNode;
  detail: string;
  effect?: ReactNode;
  updatedAt: string;
  open: boolean;
  onToggle: () => void;
  onEdit: () => void;
  onDelete: () => void;
}) {
  return (
    <li
      className={`grid grid-cols-[28px_minmax(0,1fr)_auto] items-start gap-x-3 gap-y-1.5 px-[18px] py-3 transition-colors hover:bg-paper/60 ${
        effect !== undefined ? effectColumns : plainColumns
      }`}
    >
      <Step n={n} />
      <button type="button" onClick={onToggle} aria-expanded={open} className="min-w-0 rounded text-left">
        <span className="block text-[13.5px] font-semibold leading-snug">{title}</span>
        {meta && <span className="mt-0.5 block text-xs text-ink-muted">{meta}</span>}
        <span className={`mt-1 whitespace-pre-line text-[13px] leading-relaxed text-ink-soft ${open ? "block" : "line-clamp-2"}`}>{detail}</span>
      </button>
      <span className="col-start-2 row-start-2 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 @3xl:contents">
        {effect !== undefined && <span className="flex min-w-0 @3xl:pt-[3px]">{effect}</span>}
        <span className="whitespace-nowrap text-[11px] text-ink-muted @3xl:pt-[5px]" title={`Last changed ${formatDate(updatedAt)}`}>
          <span className="@3xl:hidden">changed </span>
          {formatDay(updatedAt)}
        </span>
      </span>
      <span className="col-start-3 row-start-1 -mr-1.5 flex shrink-0 justify-end gap-0.5 @3xl:col-start-auto @3xl:row-start-auto">
        <IconButton label={`Edit "${title}"`} onClick={onEdit}>
          <Pencil />
        </IconButton>
        <IconButton label={`Delete "${title}"`} danger onClick={onDelete}>
          <Trash2 />
        </IconButton>
        <IconButton label={open ? "Show less" : "Show the whole text"} onClick={onToggle}>
          <ChevronDown className={`transition-transform ${open ? "rotate-180" : ""}`} />
        </IconButton>
      </span>
    </li>
  );
}

export function NoMatches({ query, onClear }: { query: string; onClear: () => void }) {
  return (
    <div className="flex flex-col items-center gap-2 px-[18px] py-10 text-center text-ink-muted">
      <span>Nothing matches “{query.trim()}”.</span>
      <Button small onClick={onClear}>
        Clear search
      </Button>
    </div>
  );
}

export function TitleTextModal({
  heading,
  subtitle,
  submitLabel,
  initialTitle,
  initialDetail,
  titlePlaceholder,
  detailPlaceholder,
  saving,
  error,
  onSave,
  onClose,
}: {
  heading: string;
  subtitle: ReactNode;
  submitLabel: string;
  initialTitle: string;
  initialDetail: string;
  titlePlaceholder: string;
  detailPlaceholder: string;
  saving: boolean;
  error: unknown;
  onSave: (body: { title: string; detail: string }) => void;
  onClose: () => void;
}) {
  const [title, setTitle] = useState(initialTitle);
  const [detail, setDetail] = useState(initialDetail);
  const [tried, setTried] = useState(false);
  const missing = title.trim().length === 0 || detail.trim().length === 0;

  function save() {
    setTried(true);
    if (missing) {
      return;
    }
    onSave({ title: title.trim(), detail: detail.trim() });
  }

  return (
    <Modal
      title={heading}
      subtitle={subtitle}
      onClose={onClose}
      busy={saving}
      size="max-w-xl"
      footer={
        <>
          <Button onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button variant="primary" onClick={save} disabled={saving}>
            {saving ? "Saving…" : submitLabel}
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
            placeholder={titlePlaceholder}
            autoFocus
          />
          <span className="text-[11px] text-ink-muted">Short, for you; the grader does not see it</span>
        </label>
        <label className="flex flex-col gap-1.5">
          <span className="eyebrow">What the grader is told</span>
          <textarea
            value={detail}
            onChange={(e) => setDetail(e.target.value)}
            placeholder={detailPlaceholder}
            maxLength={2000}
            rows={7}
            className="field min-h-[170px] resize-y py-2 leading-relaxed"
          />
          <span className="text-[11px] text-ink-muted">{detail.trim().length} / 2000 characters</span>
        </label>
        {tried && missing && <p className="text-xs text-danger">Enter a title and a text.</p>}
        {error != null && <ErrorPanel error={error} />}
      </form>
    </Modal>
  );
}
