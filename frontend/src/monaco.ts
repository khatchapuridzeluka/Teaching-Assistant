import * as monaco from "monaco-editor";
import EditorWorker from "monaco-editor/editor/editor.worker?worker";
import { loader } from "@monaco-editor/react";

self.MonacoEnvironment = { getWorker: () => new EditorWorker() };
loader.config({ monaco });

// The code viewer in the app's own colours: navy ink, cobalt keywords, green strings, red-pen numbers.
monaco.editor.defineTheme("marking-desk", {
  base: "vs",
  inherit: true,
  rules: [
    { token: "", foreground: "16203a" },
    { token: "comment", foreground: "5d6985" },
    { token: "keyword", foreground: "1857d6", fontStyle: "bold" },
    { token: "string", foreground: "1c8a4e" },
    { token: "number", foreground: "b4321f" },
    { token: "type", foreground: "0e7490" },
    { token: "type.identifier", foreground: "0e7490" },
    { token: "annotation", foreground: "b86e00" },
    { token: "delimiter", foreground: "5d6985" },
    { token: "operator", foreground: "5d6985" },
  ],
  colors: {
    "editor.background": "#ffffff",
    "editor.foreground": "#16203a",
    "editorLineNumber.foreground": "#b3bbcd",
    "editorLineNumber.activeForeground": "#5d6985",
    "editor.selectionBackground": "#ffe58a",
    "editor.inactiveSelectionBackground": "#fff3c2",
    "editorIndentGuide.background1": "#eef0f6",
    "editorGutter.background": "#fafbfd",
    "scrollbarSlider.background": "#5d698533",
    "scrollbarSlider.hoverBackground": "#5d698555",
    "editorOverviewRuler.border": "#00000000",
    "editorWidget.background": "#ffffff",
    "editorWidget.border": "#dcdfea",
    "editorHoverWidget.background": "#ffffff",
    "editorHoverWidget.border": "#dcdfea",
  },
});

// Monaco measures glyph widths once; measure again when the web font arrives.
void document.fonts?.ready.then(() => monaco.editor.remeasureFonts());
