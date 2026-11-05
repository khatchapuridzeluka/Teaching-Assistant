import { useEffect, useMemo, useState } from "react";
import Editor from "@monaco-editor/react";
import { type CodeFile, codeFileUrl } from "../api/client";
import { useCodeFileText, useCodeFiles } from "../api/hooks";
import { ErrorPanel, Spinner } from "./ui";

const languages: Record<string, string> = {
  java: "java", kt: "kotlin", c: "c", h: "cpp", cpp: "cpp", cc: "cpp", hpp: "cpp", cs: "csharp",
  py: "python", js: "javascript", ts: "typescript", sql: "sql", xml: "xml", json: "json", md: "markdown",
  html: "html", css: "css", properties: "ini", gradle: "groovy", txt: "plaintext",
};

const imageExtensions = new Set(["png", "jpg", "jpeg", "gif", "webp", "bmp", "svg"]);

const extension = (path: string) => path.split(".").pop()?.toLowerCase() ?? "";

/** The extracted code of one version: a file list on the left, the selected file on the right (read-only). */
export function CodeBrowser({ versionId }: { versionId: string }) {
  const files = useCodeFiles(versionId, true);
  const [selected, setSelected] = useState<string | null>(null);

  // Open the most likely entry point first: Main.java, else the first source file.
  useEffect(() => {
    if (!files.data || files.data.length === 0) return;
    if (selected && files.data.some((f) => f.path === selected)) return;
    const main = files.data.find((f) => /(^|\/)main\.java$/i.test(f.path));
    const source = files.data.find((f) => languages[extension(f.path)] && extension(f.path) !== "txt");
    setSelected((main ?? source ?? files.data[0]!).path);
  }, [files.data, selected]);

  if (files.isLoading) return <Spinner label="Loading code…" />;
  if (files.isError) return <ErrorPanel error={files.error} />;
  if (!files.data || files.data.length === 0) return <div className="text-ink-muted">No code was extracted for this version.</div>;

  return (
    <div className="flex h-[560px] overflow-hidden rounded-[10px] border border-line bg-white">
      <FileList files={files.data} selected={selected} onSelect={setSelected} />
      <div className="flex min-w-0 flex-1 flex-col">
        {selected && <FileView versionId={versionId} path={selected} />}
      </div>
    </div>
  );
}

function FileList({ files, selected, onSelect }: { files: CodeFile[]; selected: string | null; onSelect: (path: string) => void }) {
  // Group by folder so nested projects (src/library/…) read like a tree.
  const groups = useMemo(() => {
    const byFolder = new Map<string, CodeFile[]>();
    for (const file of files) {
      const folder = file.path.includes("/") ? file.path.slice(0, file.path.lastIndexOf("/")) : "";
      byFolder.set(folder, [...(byFolder.get(folder) ?? []), file]);
    }
    return [...byFolder.entries()];
  }, [files]);

  return (
    <nav className="w-[230px] shrink-0 overflow-y-auto border-r border-line py-2">
      {groups.map(([folder, folderFiles]) => (
        <div key={folder} className="mb-1">
          {folder && <div className="truncate px-3 pb-0.5 pt-2 text-[10px] font-bold uppercase tracking-[0.06em] text-ink-muted">{folder}/</div>}
          {folderFiles.map((file) => {
            const name = file.path.slice(file.path.lastIndexOf("/") + 1);
            return (
              <button
                key={file.path}
                onClick={() => onSelect(file.path)}
                title={file.path}
                className={`block w-full truncate px-3 py-1 text-left text-xs ${
                  file.path === selected ? "bg-accent-tint font-bold text-accent" : "hover:bg-paper"
                }`}
              >
                {name}
              </button>
            );
          })}
        </div>
      ))}
    </nav>
  );
}

function FileView({ versionId, path }: { versionId: string; path: string }) {
  const ext = extension(path);
  const isImage = imageExtensions.has(ext);
  const text = useCodeFileText(versionId, isImage ? null : path);

  return (
    <>
      <div className="truncate border-b border-line px-4 py-2 text-xs font-bold">{path}</div>
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
            options={{
              readOnly: true,
              domReadOnly: true,
              minimap: { enabled: false },
              fontSize: 12.5,
              scrollBeyondLastLine: false,
              renderLineHighlight: "none",
              automaticLayout: true,
            }}
          />
        </div>
      )}
    </>
  );
}
