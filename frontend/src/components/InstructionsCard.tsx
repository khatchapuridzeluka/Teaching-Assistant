import { useEffect, useState } from "react";
import { Link } from "react-router";
import { type Assignment, type InstructionsFile, instructionsFileUrl } from "../api/client";
import { useInstructions, useInstructionsFileBlob } from "../api/hooks";
import { formatDate } from "../format";
import { DocxView } from "./DocxPreview";
import { Button, Chip } from "./ui";

type FileType = "pdf" | "docx" | "image" | "video" | "link" | "other";

// By type or by extension: Drive sometimes reports a generic type for uploaded files.
function fileType(file: InstructionsFile): FileType {
  const name = file.name.toLowerCase();
  if (file.url != null) {
    return "link";
  }
  if (file.mimeType === "application/pdf" || name.endsWith(".pdf")) {
    return "pdf";
  }
  if (name.endsWith(".docx") || file.mimeType?.includes("wordprocessingml")) {
    return "docx";
  }
  if (file.mimeType?.startsWith("image/")) {
    return "image";
  }
  if (file.mimeType?.startsWith("video/") || /\.(mp4|mov|webm|mkv|avi)$/.test(name)) {
    return "video";
  }
  return "other";
}

const typeLabel: Record<FileType, string> = { pdf: "PDF", docx: "DOCX", image: "IMG", video: "VIDEO", link: "LINK", other: "FILE" };

/** Drive's embeddable player for a Drive file link (…/file/d/{id}/view → …/file/d/{id}/preview). */
function drivePreviewUrl(url: string | null | undefined): string | null {
  const id = url?.match(/\/file\/d\/([^/?#]+)/)?.[1];
  return id ? `https://drive.google.com/file/d/${id}/preview` : null;
}

function formatSize(bytes: number | null | undefined): string {
  if (bytes == null) {
    return "";
  }
  return bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

/** One material with everything the viewer needs: our stored copy when there is one, otherwise Drive's link. */
export type Material = { file: InstructionsFile; type: FileType; storedUrl: string | null; driveUrl: string | null };

/** The page path of an assignment's instructions. */
export const instructionsPath = (courseId: string, assignmentId: string) =>
  `/courses/${courseId}/assignments/${assignmentId}/instructions`;

/**
 * The instructions as grading sees them: the stored version (or, before the first fetch, the live statement from
 * Classroom) and its materials. Shared by the card, the modal and the instructions page.
 */
export function useInstructionMaterials(courseId: string, assignment: Assignment | undefined) {
  const instructions = useInstructions(courseId, assignment?.id ?? "");
  const stored = instructions.data;
  const text = stored?.description ?? assignment?.description ?? "";
  // Classroom's statement differs from what grading would send: the teacher edited it since the last fetch.
  const changed = stored != null && assignment !== undefined && (stored.description ?? "") !== (assignment.description ?? "");

  const materials: Material[] = (stored?.files ?? []).map((file) => ({
    file,
    type: fileType(file),
    storedUrl: file.stored && assignment ? instructionsFileUrl(courseId, assignment.id, file.id) : null,
    // Files we do not store (videos) still play from Drive, through Classroom's link for the material.
    driveUrl: assignment?.materials.find((m) => m.title === file.name)?.url ?? null,
  }));
  // The task document first: what the student reads, and what grading sends.
  const main = materials.find((m) => m.storedUrl && (m.type === "pdf" || m.type === "docx")) ?? materials[0];

  return { instructions, stored, text, changed, materials, main };
}

type InstructionsState = ReturnType<typeof useInstructionMaterials>;

/** The version chip and what the shown instructions are. */
export function InstructionsStatus({ state }: { state: InstructionsState }) {
  const { stored, instructions } = state;
  return (
    <>
      {stored ? (
        <Chip tone="tint">
          v{stored.number} · stored {formatDate(stored.fetchedAt)}
        </Chip>
      ) : (
        stored === null && <Chip tone="dashed">Not stored yet</Chip>
      )}
      <span className="text-xs text-ink-muted">
        {stored
          ? "Exactly what grading sends to the AI"
          : instructions.isError
            ? "Showing the live text from Classroom"
            : "Live from Classroom · Fetch stores it for grading"}
      </span>
    </>
  );
}

/** Load failures and Classroom edits since the stored version. */
export function InstructionsNotes({ state }: { state: InstructionsState }) {
  const { instructions, stored, changed } = state;
  return (
    <>
      {instructions.isError && (
        <div className="border-b border-line bg-paper/60 px-[18px] py-2 text-xs">
          <b>Could not load the stored instructions</b>{" "}
          <span className="text-ink-muted">
            ({instructions.error.message}). If you just updated the app, restart the API. Grading is not affected.
          </span>
        </div>
      )}
      {changed && stored && (
        <div className="border-b border-line bg-paper/60 px-[18px] py-2 text-xs">
          <b>Changed in Classroom</b> <span className="text-ink-muted">since v{stored.number}. Fetch to store the new version.</span>
        </div>
      )}
    </>
  );
}

/**
 * On the assignment page: the instructions in one row — version, the statement's first line (click to read it all),
 * and the materials as chips that open the instructions modal, where everything is previewed.
 */
export function InstructionsBar({ courseId, assignment }: { courseId: string; assignment: Assignment }) {
  const state = useInstructionMaterials(courseId, assignment);
  const { text, materials, stored, main } = state;
  const [expanded, setExpanded] = useState(false);
  const [open, setOpen] = useState<Material | null>(null);

  return (
    <div className="border-y border-line">
      <div className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1.5 py-2.5 md:flex-nowrap">
        <span className="shrink-0 text-[11px] font-bold uppercase tracking-[0.06em] text-ink-muted">Instructions</span>
        {stored ? (
          <Chip tone="tint">
            v{stored.number} · stored {formatDate(stored.fetchedAt)}
          </Chip>
        ) : (
          stored === null && <Chip tone="dashed">Not stored yet</Chip>
        )}
        <button
          onClick={() => setExpanded(!expanded)}
          aria-expanded={expanded}
          title={expanded ? "Hide the task statement" : "Read the whole task statement"}
          // A line of its own on phones, between the chip row and the links.
          className="order-last w-full truncate text-left text-ink-muted hover:text-ink md:order-none md:w-auto md:min-w-[140px] md:flex-1"
        >
          {text || "No task statement; the task is in the attached materials."}
        </button>
        {/* The first few materials as chips (cut off when there is no room); the modal has all of them. */}
        <div className="hidden min-w-0 shrink items-center gap-1.5 overflow-hidden lg:flex">
          {materials.slice(0, 3).map((m) => (
            <button
              key={m.file.id}
              onClick={() => setOpen(m)}
              title={`${m.file.name} · ${m.storedUrl ? "sent to the AI" : "not sent to the AI"}`}
              className="flex h-7 max-w-[170px] shrink-0 items-center gap-1.5 rounded-lg border border-line bg-white px-2 text-xs hover:border-accent"
            >
              <span className="text-[9px] font-bold tracking-wide text-accent">{typeLabel[m.type]}</span>
              <span className="truncate">{m.file.name}</span>
            </button>
          ))}
        </div>
        <div className="flex shrink-0 items-center">
          {materials.length > 0 && (
            <button onClick={() => setOpen(main ?? null)} className="px-1.5 text-xs font-semibold text-accent">
              {/* The chips are hidden on smaller screens, so there the link names all materials. */}
              <span className="lg:hidden">Materials · {materials.length}</span>
              <span className="hidden lg:inline">{materials.length > 3 ? `+${materials.length - 3} · View all` : "View all"}</span>
            </button>
          )}
          <Link to={instructionsPath(courseId, assignment.id)} className="px-1.5 text-xs font-semibold text-accent" title="Open the instructions page">
            Page →
          </Link>
        </div>
      </div>
      <InstructionsNotes state={state} />
      {expanded && (
        <p className="mb-3 max-h-[220px] overflow-y-auto whitespace-pre-wrap rounded-[10px] border border-line bg-white px-[18px] py-3 leading-relaxed">
          {text || "No task statement; the task is in the attached materials."}
        </p>
      )}
      {open && (
        <InstructionsModal courseId={courseId} assignment={assignment} state={state} initial={open} onClose={() => setOpen(null)} />
      )}
    </div>
  );
}

/** A dialog over the assignment page with every material previewable; Escape or the backdrop closes it. */
function InstructionsModal({
  courseId,
  assignment,
  state,
  initial,
  onClose,
}: {
  courseId: string;
  assignment: Assignment;
  state: InstructionsState;
  initial: Material;
  onClose: () => void;
}) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        onClose();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  return (
    <div className="fixed inset-0 z-40 flex items-center justify-center bg-ink/50 p-2 sm:p-6" onClick={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-label={`${assignment.title} instructions`}
        onClick={(e) => e.stopPropagation()}
        className="flex h-[94dvh] w-full max-w-[1100px] flex-col overflow-hidden rounded-xl border border-line bg-white shadow-xl sm:h-[88vh]"
      >
        <header className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 border-b border-line px-4 py-3 sm:px-[18px]">
          <div className="flex min-w-0 flex-wrap items-center gap-2.5">
            <b className="truncate text-[15px]">{assignment.title} · instructions</b>
            <InstructionsStatus state={state} />
          </div>
          <div className="flex shrink-0 items-center gap-2">
            <Link to={instructionsPath(courseId, assignment.id)}>
              <Button small>Open as page</Button>
            </Link>
            <Button small onClick={onClose}>
              Close
            </Button>
          </div>
        </header>
        <InstructionsViewer courseId={courseId} assignmentId={assignment.id} materials={state.materials} initial={initial} />
      </div>
    </div>
  );
}

/** Tabs for every material, the selected one shown in the remaining height, and what grading does with it. */
export function InstructionsViewer({
  courseId,
  assignmentId,
  materials,
  initial,
}: {
  courseId: string;
  assignmentId: string;
  materials: Material[];
  initial: Material | undefined;
}) {
  const [selectedId, setSelectedId] = useState(initial?.file.id);
  const selected = materials.find((m) => m.file.id === selectedId) ?? initial;

  if (!selected) {
    return <div className="px-[18px] py-10 text-center text-ink-muted">This assignment has no materials.</div>;
  }

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      {/* Tabs scroll sideways when there are many materials. */}
      <nav className="flex shrink-0 gap-1 overflow-x-auto bg-paper/50 px-[18px] pt-2" aria-label="Materials">
        {materials.map((m) => {
          const active = m.file.id === selected.file.id;
          return (
            <button
              key={m.file.id}
              onClick={() => setSelectedId(m.file.id)}
              aria-pressed={active}
              className={`flex shrink-0 items-center gap-1.5 rounded-t-lg border border-b-0 px-3 py-2 text-xs font-semibold ${
                active ? "border-line bg-white text-ink" : "border-transparent text-ink-muted hover:text-ink"
              }`}
            >
              <span className={`text-[9px] font-bold tracking-wide ${active ? "text-accent" : ""}`}>{typeLabel[m.type]}</span>
              <span className="max-w-[240px] truncate">{m.file.name}</span>
            </button>
          );
        })}
      </nav>
      <div className="min-h-0 flex-1 border-t border-line bg-paper">
        <MaterialView key={selected.file.id} courseId={courseId} assignmentId={assignmentId} material={selected} />
      </div>
      <MaterialFooter courseId={courseId} assignmentId={assignmentId} material={selected} />
    </div>
  );
}

/** Shows one material in the space it is given: our stored copy when there is one, otherwise Drive's own player. */
function MaterialView({ courseId, assignmentId, material }: { courseId: string; assignmentId: string; material: Material }) {
  const { file, type, storedUrl, driveUrl } = material;
  const drivePreview = drivePreviewUrl(driveUrl);

  if (storedUrl && type === "pdf") {
    return <iframe src={storedUrl} title={file.name} className="h-full w-full bg-white" />;
  }
  if (storedUrl && type === "docx") {
    // Rendered in the browser from the stored .docx, the same file grading reads; no PDF conversion needed.
    return <InstructionsDocx courseId={courseId} assignmentId={assignmentId} file={file} />;
  }
  if (storedUrl && type === "image") {
    return (
      <div className="flex h-full items-center justify-center p-4">
        <img src={storedUrl} alt={file.name} className="max-h-full max-w-full rounded-lg border border-line bg-white object-contain" />
      </div>
    );
  }
  if (drivePreview) {
    // Drive's player: needs the browser to be signed in to a Google account that can see the file.
    return <iframe src={drivePreview} title={file.name} allow="autoplay; fullscreen" className="h-full w-full bg-black" />;
  }

  return (
    <div className="flex h-full flex-col items-center justify-center gap-2 px-6 text-center text-ink-muted">
      <div className="font-semibold text-ink">{file.name}</div>
      <div className="text-xs">{type === "link" ? "Web pages open in a new tab." : "This file cannot be shown in the page."}</div>
      {file.url && (
        <a href={file.url} target="_blank" rel="noreferrer">
          <Button small>Open link ↗</Button>
        </a>
      )}
      {storedUrl && (
        <a href={instructionsFileUrl(courseId, assignmentId, file.id, true)}>
          <Button small>Download</Button>
        </a>
      )}
    </div>
  );
}

/** What happens to the material in grading, and where else it can be opened. */
function MaterialFooter({ courseId, assignmentId, material }: { courseId: string; assignmentId: string; material: Material }) {
  const { file, storedUrl, driveUrl } = material;
  const sent = storedUrl !== null && file.status !== "Unsupported";

  return (
    <div className="flex shrink-0 flex-wrap items-center justify-between gap-3 border-t border-line px-[18px] py-2 text-xs">
      <span className="text-ink-muted">
        {sent ? (
          <>
            <b className="text-accent">Sent to the AI</b> with every grading · our stored copy · {formatSize(file.sizeBytes)}
          </>
        ) : (
          <>
            <b className="text-ink">Not sent to the AI</b> · {file.url ? "a web link" : "videos and unsupported files are not stored"}
          </>
        )}
      </span>
      <span className="flex gap-3">
        {storedUrl && (
          <a href={instructionsFileUrl(courseId, assignmentId, file.id, true)} className="font-semibold text-accent">
            Download
          </a>
        )}
        {driveUrl && (
          <a href={driveUrl} target="_blank" rel="noreferrer" className="font-semibold text-accent">
            Open in Drive ↗
          </a>
        )}
      </span>
    </div>
  );
}

function InstructionsDocx({ courseId, assignmentId, file }: { courseId: string; assignmentId: string; file: InstructionsFile }) {
  return <DocxView blob={useInstructionsFileBlob(courseId, assignmentId, file.id)} className="h-full rounded-none border-0" />;
}
