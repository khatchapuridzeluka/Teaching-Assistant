import { useEffect, useRef, useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { sendChat } from "../api/chat";
import { Button } from "./ui";
import { useChatHistory } from "./useChatHistory";

const suggestions = [
  "Draft rubric criteria for a Strategy pattern assignment",
  "Explain composition vs inheritance for my students",
  "Write short feedback for a student who made all fields public",
];

export function ChatPanel({ page, onClose }: { page: string | null; onClose: () => void }) {
  const { turns, add, clear } = useChatHistory();
  const [input, setInput] = useState("");
  const [waiting, setWaiting] = useState(false);
  const abort = useRef<AbortController | null>(null);
  const bottom = useRef<HTMLDivElement>(null);

  useEffect(() => {
    bottom.current?.scrollIntoView({ block: "end" });
  }, [turns, waiting]);

  async function send(text: string) {
    const question = text.trim();
    if (!question || waiting) {
      return;
    }

    const prompt = { role: "User" as const, text: question };
    const history = turns.filter((turn) => !turn.error).map(({ role, text }) => ({ role, text }));
    add(prompt);
    setInput("");
    setWaiting(true);
    abort.current = new AbortController();

    try {
      const reply = await sendChat({ messages: [...history, prompt], page }, abort.current.signal);
      add({ role: "Assistant", text: reply });
    } catch (error) {
      const stopped = error instanceof DOMException && error.name === "AbortError";
      const message = stopped ? "Stopped." : error instanceof Error ? error.message : "Something went wrong.";
      add({ role: "Assistant", text: message, error: true });
    } finally {
      setWaiting(false);
      abort.current = null;
    }
  }

  return (
    <aside className="flex h-full w-full max-w-[400px] shrink-0 flex-col border-l border-line bg-white xl:w-[400px]">
      <header className="flex items-center justify-between gap-3 border-b border-line px-[18px] py-3.5">
        <div className="flex min-w-0 items-center gap-2.5">
          <AgentMark size="h-7 w-7" text="text-[11px]" />
          <div className="min-w-0">
            <div className="text-sm font-bold">Ask agent</div>
            <div className="truncate text-[11px] text-ink-muted">{page ?? "General question"}</div>
          </div>
        </div>
        <div className="flex shrink-0 gap-1.5">
          <Button small onClick={clear} disabled={waiting || turns.length === 0}>New chat</Button>
          <Button small onClick={onClose}>Close</Button>
        </div>
      </header>

      <div className="flex-1 overflow-y-auto px-[18px] py-4">
        {turns.length === 0 ? (
          <div className="flex flex-col gap-3">
            <p className="text-ink-muted">
              Ask about teaching, rubrics or feedback wording, or paste code to review. The agent cannot see your courses
              or submissions yet.
            </p>
            <div className="flex flex-wrap gap-1.5">
              {suggestions.map((suggestion) => (
                <button
                  key={suggestion}
                  onClick={() => send(suggestion)}
                  className="rounded-full border border-line bg-white px-2.5 py-1 text-left text-[11px] font-semibold hover:border-accent"
                >
                  {suggestion}
                </button>
              ))}
            </div>
          </div>
        ) : (
          <div className="flex flex-col gap-3.5">
            {turns.map((turn, i) =>
              turn.role === "User" ? (
                <div key={i} className="max-w-[88%] self-end whitespace-pre-wrap rounded-xl rounded-br-sm bg-ink px-3 py-2.5 text-white">
                  {turn.text}
                </div>
              ) : (
                <div key={i} className="flex min-w-0 items-start gap-2">
                  <AgentMark size="mt-0.5 h-6 w-6" />
                  <div
                    className={`min-w-0 flex-1 rounded-xl rounded-tl-sm px-3.5 py-3 ${turn.error ? "border border-l-4 border-line border-l-ink bg-white" : "bg-paper"}`}
                  >
                    <div className="markdown">
                      <ReactMarkdown remarkPlugins={[remarkGfm]}>{turn.text}</ReactMarkdown>
                    </div>
                  </div>
                </div>
              ),
            )}
            {waiting && (
              <div className="flex items-start gap-2">
                <AgentMark size="mt-0.5 h-6 w-6" />
                <div className="flex items-center gap-2 rounded-xl rounded-tl-sm bg-paper px-3.5 py-3 text-ink-muted">
                  <span className="h-3.5 w-3.5 animate-spin rounded-full border-2 border-line border-t-accent" />
                  Thinking…
                </div>
              </div>
            )}
            <div ref={bottom} />
          </div>
        )}
      </div>

      <form
        className="border-t border-line px-[18px] py-3"
        onSubmit={(e) => {
          e.preventDefault();
          send(input);
        }}
      >
        <div className="rounded-[10px] border border-line focus-within:border-accent">
          <textarea
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                send(input);
              }
            }}
            rows={3}
            placeholder="Ask the agent…"
            className="block w-full resize-none rounded-[10px] px-3 pt-2.5 outline-none"
          />
          <div className="flex justify-end px-2 pb-2">
            {waiting ? (
              <Button small type="button" onClick={() => abort.current?.abort()}>Stop</Button>
            ) : (
              <Button small variant="primary" type="submit" disabled={!input.trim()}>Send</Button>
            )}
          </div>
        </div>
        <div className="mt-1.5 text-[11px] text-ink-muted">
          Enter to send · Shift+Enter for a new line. The agent remembers this conversation until New chat or until you close the tab.
        </div>
      </form>
    </aside>
  );
}

function AgentMark({ size, text = "text-[10px]" }: { size: string; text?: string }) {
  return (
    <span
      aria-hidden="true"
      className={`flex shrink-0 items-center justify-center rounded-md bg-accent font-bold text-white ${size} ${text}`}
    >
      TA
    </span>
  );
}
