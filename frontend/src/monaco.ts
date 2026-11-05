import * as monaco from "monaco-editor";
// The package maps "monaco-editor/*" to its esm/vs/*.js files.
import EditorWorker from "monaco-editor/editor/editor.worker?worker";
import { loader } from "@monaco-editor/react";

// Use the bundled Monaco instead of the default CDN download, so the app works offline and loads nothing external.
self.MonacoEnvironment = { getWorker: () => new EditorWorker() };
loader.config({ monaco });
