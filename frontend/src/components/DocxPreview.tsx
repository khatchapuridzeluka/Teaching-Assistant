import { renderAsync } from "docx-preview";
import { useEffect, useRef, useState } from "react";
import type { UseQueryResult } from "@tanstack/react-query";
import type { StoredFile } from "../api/client";
import { useStoredFileBlob } from "../api/hooks";
import { ErrorPanel, Spinner } from "./ui";

export function DocxPreview({ file }: { file: StoredFile }) {
  return <DocxView blob={useStoredFileBlob(file.id)} />;
}

export function DocxView({ blob, className = "max-h-[720px]" }: { blob: UseQueryResult<Blob>; className?: string }) {
  const container = useRef<HTMLDivElement>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    const target = container.current;
    if (!blob.data || !target) {
      return;
    }

    const host = document.createElement("div");
    target.appendChild(host);
    let cancelled = false;
    setFailed(false);

    const fit = () => {
      const page = host.querySelector<HTMLElement>("section.docx");
      if (!page) {
        return;
      }
      const wrapper = host.querySelector<HTMLElement>(".docx-wrapper");
      const margin = wrapper ? parseFloat(getComputedStyle(wrapper).paddingLeft) * 2 : 0;
      const needed = page.offsetWidth + margin;
      const available = target.clientWidth;
      host.style.zoom = needed > available ? String(available / needed) : "1";
    };
    const observer = new ResizeObserver(fit);
    observer.observe(target);

    renderAsync(blob.data, host, host, {
      inWrapper: true,
      breakPages: true,
      ignoreLastRenderedPageBreak: true,
      useBase64URL: true,
      renderAltChunks: false,
    })
      .then(() => {
        if (!cancelled) {
          fit();
        }
      })
      .catch(() => {
        if (!cancelled) {
          setFailed(true);
        }
      });

    return () => {
      cancelled = true;
      observer.disconnect();
      host.remove();
    };
  }, [blob.data]);

  if (blob.isError) {
    return <ErrorPanel error={blob.error} />;
  }

  return (
    <div className={`overflow-auto surface ${className}`}>
      {blob.isLoading && (
        <div className="px-4 py-3">
          <Spinner label="Loading document…" />
        </div>
      )}
      {failed && (
        <div className="px-4 py-3 text-ink-muted">This document could not be shown. Download it to open it in Word.</div>
      )}
      <div ref={container} />
    </div>
  );
}
