import { type ReactNode, useEffect, useState } from "react";
import { type StoredFile, type StoredSubmission, storedFileUrl } from "../api/client";
import { formatDate } from "../format";
import { CodeBrowser } from "./CodeBrowser";
import { DocxPreview } from "./DocxPreview";
import { Button, Chip, type ChipTone } from "./ui";

const docxMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

// By type or by extension: Drive sometimes reports a generic type (application/octet-stream) for uploaded files.
const isPdf = (file: StoredFile) => file.mimeType === "application/pdf" || file.name.toLowerCase().endsWith(".pdf");
const isDocx = (file: StoredFile) => file.mimeType === docxMimeType || file.name.toLowerCase().endsWith(".docx");

const versionChip: Record<string, { tone: ChipTone; label: string }> = {
  Ready: { tone: "tint", label: "All files stored" },
  PartiallySupported: { tone: "outline", label: "Some files not stored" },
  Failed: { tone: "ink", label: "Nothing stored" },
};

/**
 * Shows a stored submission from our own copy (not Google Drive): the extracted code in a viewer, images as a gallery,
 * PDFs (including exported Google Docs) and Word documents (.docx) inline, and everything else as a download.
 */
export function PreviewPanel({ submission, onClose }: { submission: StoredSubmission; onClose: () => void }) {
  const [versionId, setVersionId] = useState(submission.versions[0]?.id);
  const [enlarged, setEnlarged] = useState<StoredFile | null>(null);
  const version = submission.versions.find((v) => v.id === versionId) ?? submission.versions[0];

  // Escape closes the enlarged image first, then the panel.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && (enlarged ? setEnlarged(null) : onClose());
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [enlarged, onClose]);

  if (!version) {
    return null;
  }

  const stored = version.files.filter((f) => f.stored);
  const images = stored.filter((f) => f.kind === "Image");
  const pdfs = stored.filter((f) => !images.includes(f) && isPdf(f));
  const docs = stored.filter((f) => !images.includes(f) && isDocx(f));
  const others = stored.filter((f) => !images.includes(f) && !pdfs.includes(f) && !docs.includes(f));
  const notStored = version.files.filter((f) => !f.stored);
  const chip = versionChip[version.status];

  return (
    <div className="fixed inset-0 z-40 flex justify-end bg-ink/40" onClick={onClose}>
      <aside className="flex h-full w-full max-w-[920px] flex-col bg-paper shadow-xl" onClick={(e) => e.stopPropagation()}>
        <header className="flex items-center justify-between gap-4 border-b border-line bg-white px-6 py-4">
          <div className="min-w-0">
            <div className="truncate text-[15px] font-bold">{submission.studentName}</div>
            <div className="text-xs text-ink-muted">
              Fetched {formatDate(version.fetchedAt)} · turned in {formatDate(version.turnedInAt)} · our stored copy
            </div>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            {chip && <Chip tone={chip.tone}>{chip.label}</Chip>}
            {submission.versions.map((v) => (
              <button
                key={v.id}
                onClick={() => setVersionId(v.id)}
                className={`h-7 rounded-lg border px-2.5 text-xs font-semibold ${
                  v.id === version.id ? "border-ink bg-ink text-white" : "border-line bg-white hover:bg-paper"
                }`}
              >
                v{v.number}
              </button>
            ))}
            <Button small onClick={onClose}>Close</Button>
          </div>
        </header>

        <div className="flex-1 overflow-y-auto px-6 py-5">
          <div className="flex flex-col gap-6">
            {version.extractionDetail && (
              <div className="rounded-[10px] border border-line border-l-4 border-l-ink bg-white px-4 py-2.5 text-xs">
                <b>Extraction problem:</b> <span className="text-ink-muted">{version.extractionDetail}</span>
              </div>
            )}

            {version.hasCode && (
              <Section title="Code">
                <CodeBrowser key={version.id} versionId={version.id} />
              </Section>
            )}

            {images.length > 0 && (
              <Section title={`Images (${images.length})`}>
                <div className="grid grid-cols-2 gap-3 md:grid-cols-3">
                  {images.map((file) => (
                    <button
                      key={file.id}
                      onClick={() => setEnlarged(file)}
                      className="group overflow-hidden rounded-[10px] border border-line bg-white text-left hover:border-accent"
                    >
                      <img src={storedFileUrl(file.id)} alt={file.name} loading="lazy" className="h-40 w-full bg-paper-dark object-contain" />
                      <div className="truncate border-t border-line px-2.5 py-1.5 text-xs font-semibold">{file.name}</div>
                    </button>
                  ))}
                </div>
              </Section>
            )}

            {pdfs.map((file) => (
              <Section key={file.id} title={file.name} action={<DownloadLink file={file} />}>
                <iframe src={storedFileUrl(file.id)} title={file.name} className="h-[640px] w-full rounded-[10px] border border-line bg-white" />
              </Section>
            ))}

            {docs.map((file) => (
              <Section key={file.id} title={file.name} action={<DownloadLink file={file} />}>
                <DocxPreview file={file} />
              </Section>
            ))}

            {others.length > 0 && (
              <Section title="Other files">
                <ul className="flex flex-col divide-y divide-paper-dark rounded-[10px] border border-line bg-white">
                  {others.map((file) => (
                    <li key={file.id} className="flex items-center justify-between gap-4 px-4 py-2.5">
                      <div className="min-w-0">
                        <div className="truncate font-semibold">{file.name}</div>
                        <div className="text-xs text-ink-muted">{describe(file, version.hasCode)}</div>
                      </div>
                      <DownloadLink file={file} />
                    </li>
                  ))}
                </ul>
              </Section>
            )}

            {notStored.length > 0 && (
              <Section title="Not stored">
                <ul className="flex flex-col gap-1 text-xs text-ink-muted">
                  {notStored.map((file) => (
                    <li key={file.id}>
                      <b className="text-ink">{file.name}</b> · {file.status === "TooLarge" ? "too large" : "not downloadable yet"}
                    </li>
                  ))}
                </ul>
              </Section>
            )}

            {version.files.length === 0 && <div className="text-ink-muted">This version has no files.</div>}
          </div>
        </div>
      </aside>

      {enlarged && (
        <div className="fixed inset-0 z-50 flex flex-col bg-ink/90 p-6" onClick={(e) => (e.stopPropagation(), setEnlarged(null))}>
          <div className="mb-3 flex items-center justify-between text-white">
            <b className="truncate">{enlarged.name}</b>
            <span className="text-xs text-[#a9abac]">Click anywhere or press Esc to close</span>
          </div>
          <img src={storedFileUrl(enlarged.id)} alt={enlarged.name} className="min-h-0 flex-1 object-contain" />
        </div>
      )}
    </div>
  );
}

function Section({ title, action, children }: { title: string; action?: ReactNode; children: ReactNode }) {
  return (
    <section>
      <div className="mb-2 flex items-center justify-between gap-4">
        <h3 className="truncate text-[11px] font-bold uppercase tracking-[0.06em] text-accent">{title}</h3>
        {action}
      </div>
      {children}
    </section>
  );
}

function DownloadLink({ file }: { file: StoredFile }) {
  return (
    <a
      href={storedFileUrl(file.id, true)}
      className="inline-flex h-7 shrink-0 items-center rounded-lg border border-line bg-white px-2.5 text-xs font-semibold hover:bg-paper"
    >
      Download
    </a>
  );
}

function describe(file: StoredFile, hasCode: boolean): string {
  const size = file.sizeBytes ? ` · ${Math.max(1, Math.round(file.sizeBytes / 1024))} KB` : "";
  const extracted = file.status === "Extracted";
  switch (file.kind) {
    case "CodeArchive":
      return `Archive${size} · ${extracted ? "extracted, see Code above" : "could not be extracted"}`;
    case "SourceFile":
      return `Source file${size}${extracted && hasCode ? " · see Code above" : ""}`;
    case "Document":
      return `Document${size} · no inline preview for this format, download to open`;
    default:
      return `File${size}`;
  }
}
