import { renderAsync } from "docx-preview";
import { useEffect, useRef, useState } from "react";
import type { UseQueryResult } from "@tanstack/react-query";
import type { StoredFile } from "../api/client";
import { useStoredFileBlob } from "../api/hooks";
import { ErrorPanel, Spinner } from "./ui";

/**
 * Shows a stored Word document (.docx) in the page. The browser lays it out with docx-preview; nothing is converted on
 * the server. Display only: what the grader reads from documents is prepared by the backend.
 */
export function DocxPreview({ file }: { file: StoredFile }) {
  return <DocxView blob={useStoredFileBlob(file.id)} />;
}

/** Renders a Word document from any loaded blob (a student's file or an assignment's instructions). */
export function DocxView({ blob, className = "max-h-[720px]" }: { blob: UseQueryResult<Blob>; className?: string }) {
  const container = useRef<HTMLDivElement>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    const target = container.current;
    if (!blob.data || !target) {
      return;
    }

    // Each render gets its own element, removed on cleanup, so a render that finishes late (React runs effects twice in
    // development) lands in a detached element instead of showing the document twice.
    const host = document.createElement("div");
    target.appendChild(host);
    let cancelled = false;
    setFailed(false);

    // Word pages have a fixed width (A4 is about 800px); in a narrower viewer (a phone) they are scaled down to fit
    // instead of scrolling sideways. Never scaled up.
    const fit = () => {
      const page = host.querySelector<HTMLElement>("section.docx");
      if (!page) {
        return;
      }
      // The page plus the grey margin around it; the page is centred, so its overflow would be cut off on both sides.
      const wrapper = host.querySelector<HTMLElement>(".docx-wrapper");
      const margin = wrapper ? parseFloat(getComputedStyle(wrapper).paddingLeft) * 2 : 0;
      const needed = page.offsetWidth + margin;
      const available = target.clientWidth;
      host.style.zoom = needed > available ? String(available / needed) : "1";
    };
    const observer = new ResizeObserver(fit);
    observer.observe(target);

    renderAsync(blob.data, host, host, {
      inWrapper: true, // grey background with page-like sheets, like Word
      breakPages: true,
      ignoreLastRenderedPageBreak: true,
      useBase64URL: true, // images as data URLs, so no object URLs to clean up
      renderAltChunks: false, // alt chunks embed raw HTML from the file; student files are untrusted
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
    <div className={`overflow-auto rounded-[10px] border border-line bg-white ${className}`}>
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
