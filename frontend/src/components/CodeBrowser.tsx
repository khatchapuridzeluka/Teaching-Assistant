import { useEffect, useMemo, useState } from "react";
import Editor, { type OnMount } from "@monaco-editor/react";
import { type CodeFile, type CodeMistake, codeFileUrl } from "../api/client";
import { useCodeFileText, useCodeFiles } from "../api/hooks";
import { ErrorPanel, Spinner } from "./ui";

const languages: Record<string, string> = {
  java: "java", kt: "kotlin", c: "c", h: "cpp", cpp: "cpp", cc: "cpp", hpp: "cpp", cs: "csharp",
  py: "python", js: "javascript", ts: "typescript", sql: "sql", xml: "xml", json: "json", md: "markdown",
  html: "html", css: "css", properties: "ini", gradle: "groovy", txt: "plaintext",
};

const imageExtensions = new Set(["png", "jpg", "jpeg", "gif", "webp", "bmp", "svg"]);

const extension = (path: string) => path.split(".").pop()?.toLowerCase() ?? "";

type Severity = CodeMistake["severity"];

const severityOrder: Severity[] = ["Major", "Minor", "Style"];
export const severityColor: Record<Severity, string> = { Major: "#dc2626", Minor: "#d97706", Style: "#64748b" };

export type CodeFocus = { file: string; line: number; key: number };

export function CodeBrowser({
  versionId,
  className = "h-[560px]",
  mistakes = [],
  focus,
}: {
  versionId: string;
  className?: string;
  mistakes?: CodeMistake[];
  focus?: CodeFocus;
}) {
  const files = useCodeFiles(versionId, true);
  const [selected, setSelected] = useState<string | null>(null);

  useEffect(() => {
    if (!files.data || files.data.length === 0) {
      return;
    }
    if (selected && files.data.some((f) => f.path === selected)) {
      return;
    }
    const main = files.data.find((f) => /(^|\/)main\.java$/i.test(f.path));
    const source = files.data.find((f) => languages[extension(f.path)] && extension(f.path) !== "txt");
    setSelected((main ?? source ?? files.data[0]!).path);
  }, [files.data, selected]);

  useEffect(() => {
    if (focus) {
      setSelected(focus.file);
    }
  }, [focus]);

  if (files.isLoading) {
    return <Spinner label="Loading code…" />;
  }
  if (files.isError) {
    return <ErrorPanel error={files.error} />;
  }
  if (!files.data || files.data.length === 0) {
    return <div className="text-ink-muted">No code was extracted for this version.</div>;
  }

  return (
    <div className={`@container flex flex-col overflow-hidden rounded-[10px] border border-line bg-white @lg:flex-row ${className}`}>
      <FileList files={files.data} selected={selected} onSelect={setSelected} mistakes={mistakes} />
      <div className="flex min-w-0 flex-1 flex-col">
        {selected && (
          <FileView
            versionId={versionId}
            path={selected}
            mistakes={mistakes.filter((m) => m.file === selected && m.startLine != null)}
            focus={focus?.file === selected ? focus : undefined}
          />
        )}
      </div>
    </div>
  );
}

function FileList({
  files,
  selected,
  onSelect,
  mistakes,
}: {
  files: CodeFile[];
  selected: string | null;
  onSelect: (path: string) => void;
  mistakes: CodeMistake[];
}) {
  const groups = useMemo(() => {
    const byFolder = new Map<string, CodeFile[]>();
    for (const file of files) {
      const folder = file.path.includes("/") ? file.path.slice(0, file.path.lastIndexOf("/")) : "";
      byFolder.set(folder, [...(byFolder.get(folder) ?? []), file]);
    }
    return [...byFolder.entries()];
  }, [files]);

  return (
    <nav className="max-h-[132px] w-full shrink-0 overflow-y-auto border-b border-line py-2 @lg:max-h-none @lg:w-[160px] @lg:border-b-0 @lg:border-r @2xl:w-[220px]">
      {groups.map(([folder, folderFiles]) => (
        <div key={folder} className="mb-1">
          {folder && <div className="truncate px-3 pb-0.5 pt-2 text-[10px] font-bold uppercase tracking-[0.06em] text-ink-muted">{folder}/</div>}
          {folderFiles.map((file) => {
            const name = file.path.slice(file.path.lastIndexOf("/") + 1);
            const own = mistakes.filter((m) => m.file === file.path);
            const worst = severityOrder.find((s) => own.some((m) => m.severity === s));
            return (
              <button
                key={file.path}
                onClick={() => onSelect(file.path)}
                title={own.length > 0 ? `${file.path} · ${own.length} mistake(s)` : file.path}
                className={`flex w-full items-center justify-between gap-2 px-3 py-1 text-left text-xs ${
                  file.path === selected ? "bg-accent-tint font-bold text-accent" : "hover:bg-paper"
                }`}
              >
                <span className="truncate">{name}</span>
                {worst && (
                  <span
                    className="shrink-0 rounded-full px-1.5 text-[10px] font-bold leading-4 text-white"
                    style={{ backgroundColor: severityColor[worst] }}
                  >
                    {own.length}
                  </span>
                )}
              </button>
            );
          })}
        </div>
      ))}
    </nav>
  );
}

type MonacoEditor = Parameters<OnMount>[0];
type Monaco = Parameters<OnMount>[1];

function FileView({
  versionId,
  path,
  mistakes,
  focus,
}: {
  versionId: string;
  path: string;
  mistakes: CodeMistake[];
  focus: CodeFocus | undefined;
}) {
  const ext = extension(path);
  const isImage = imageExtensions.has(ext);
  const text = useCodeFileText(versionId, isImage ? null : path);
  const [editor, setEditor] = useState<{ editor: MonacoEditor; monaco: Monaco } | null>(null);

  useEffect(() => {
    if (!editor || text.data === undefined) {
      return;
    }
    const { editor: ed, monaco } = editor;
    const collection = ed.createDecorationsCollection(
      mistakes.map((m) => {
        const hover = { value: `**${m.severity} · ${m.category}**\n\n${m.message}` };
        return {
          range: new monaco.Range(m.startLine!, 1, m.endLine ?? m.startLine!, 1),
          options: {
            isWholeLine: true,
            className: `mistake-line-${m.severity}`,
            glyphMarginClassName: `mistake-glyph mistake-glyph-${m.severity}`,
            glyphMarginHoverMessage: hover,
            hoverMessage: hover,
            overviewRuler: { color: severityColor[m.severity], position: monaco.editor.OverviewRulerLane.Right },
          },
        };
      }),
    );
    return () => collection.clear();
  }, [editor, mistakes, text.data]);

  useEffect(() => {
    if (!editor || !focus || text.data === undefined) {
      return;
    }
    editor.editor.revealLineInCenter(focus.line);
    editor.editor.setPosition({ lineNumber: focus.line, column: 1 });
  }, [editor, focus, text.data]);

  return (
    <>
      <div className="flex items-center justify-between gap-3 border-b border-line px-4 py-2 text-xs">
        <b className="truncate">{path}</b>
        {mistakes.length > 0 && <span className="shrink-0 text-ink-muted">Hover a dot to read the mistake</span>}
      </div>
      {isImage ? (
        <div className="flex min-h-0 flex-1 items-center justify-center bg-paper-dark p-4">
          <img src={codeFileUrl(versionId, path)} alt={path} className="max-h-full max-w-full object-contain" />
        </div>
      ) : text.isLoading ? (
        <div className="p-4"><Spinner /></div>
      ) : text.isError ? (
        <div className="p-4"><ErrorPanel error={text.error} /></div>
      ) : (
        <div className="min-h-0 flex-1">
          <Editor
            value={text.data ?? ""}
            language={languages[ext] ?? "plaintext"}
            onMount={(ed, monaco) => setEditor({ editor: ed, monaco })}
            options={{
              readOnly: true,
              domReadOnly: true,
              minimap: { enabled: false },
              fontSize: 12.5,
              scrollBeyondLastLine: false,
              renderLineHighlight: "none",
              automaticLayout: true,
              glyphMargin: true,
            }}
          />
        </div>
      )}
    </>
  );
}
