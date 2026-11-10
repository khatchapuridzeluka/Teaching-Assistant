import { useEffect, useRef, useState } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { sendChat } from "../api/chat";
import { ArrowUp, MessageSquarePlus, Sparkles, Square, X } from "lucide-react";
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
    <aside className="flex h-full w-full max-w-[400px] shrink-0 animate-slide-left flex-col border-l border-line bg-white xl:w-[400px]">
      <header className="flex items-center justify-between gap-3 border-b border-line px-4 py-3">
        <div className="flex min-w-0 items-center gap-2.5">
          <AgentMark size="h-8 w-8" />
          <div className="min-w-0">
            <div className="font-display text-[17px] font-semibold leading-tight">Ask agent</div>
            <div className="truncate text-[11px] text-ink-muted">{page ?? "General question"}</div>
          </div>
        </div>
        <div className="flex shrink-0 gap-1">
          <Button small variant="ghost" icon={<MessageSquarePlus />} onClick={clear} disabled={waiting || turns.length === 0} title="Start a new conversation">
            New
          </Button>
          <Button small variant="ghost" className="px-1.5" onClick={onClose} aria-label="Close" title="Close">
            <X className="h-4 w-4" />
          </Button>
        </div>
      </header>

      <div className="flex-1 overflow-y-auto px-[18px] py-4">
        {turns.length === 0 ? (
          <div className="flex flex-col gap-3">
            <p className="text-ink-muted">
              Ask about teaching, rubrics or feedback wording, or paste code to review. The agent cannot see your courses
              or submissions yet.
            </p>
            <div className="eyebrow mt-1">Try</div>
            <div className="flex flex-col gap-1.5">
              {suggestions.map((suggestion) => (
                <button
                  key={suggestion}
                  onClick={() => send(suggestion)}
                  className="flex items-start gap-2 rounded-lg border border-line bg-white px-3 py-2 text-left text-xs font-medium shadow-card transition-all hover:-translate-y-px hover:border-accent hover:shadow-raised"
                >
                  <Sparkles className="mt-0.5 h-3.5 w-3.5 shrink-0 text-warn" />
                  {suggestion}
                </button>
              ))}
            </div>
          </div>
        ) : (
          <div className="flex flex-col gap-3.5">
            {turns.map((turn, i) =>
              turn.role === "User" ? (
                <div key={i} className="max-w-[88%] animate-rise self-end whitespace-pre-wrap rounded-xl rounded-br-sm bg-navy px-3 py-2.5 text-white">
                  {turn.text}
                </div>
              ) : (
                <div key={i} className="flex min-w-0 animate-rise items-start gap-2">
                  <AgentMark size="mt-0.5 h-6 w-6" />
                  <div
                    className={`min-w-0 flex-1 rounded-xl rounded-tl-sm px-3.5 py-3 ${turn.error ? "border border-danger/30 bg-danger-tint/50 text-danger-ink" : "bg-paper"}`}
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
        <div className="rounded-[10px] border border-line bg-white transition-shadow focus-within:border-accent focus-within:shadow-[0_0_0_3px_rgb(24_87_214/0.16)]">
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
            className="block w-full resize-none rounded-[10px] bg-transparent px-3 pt-2.5 outline-none placeholder:text-ink-faint"
          />
          <div className="flex justify-end px-2 pb-2">
            {waiting ? (
              <Button small type="button" icon={<Square />} onClick={() => abort.current?.abort()}>
                Stop
              </Button>
            ) : (
              <Button small variant="primary" type="submit" icon={<ArrowUp />} disabled={!input.trim()}>
                Send
              </Button>
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

function AgentMark({ size }: { size: string; text?: string }) {
  return (
    <span aria-hidden="true" className={`flex shrink-0 items-center justify-center rounded-lg bg-accent text-white ${size}`}>
      <Sparkles className="h-[55%] w-[55%]" />
    </span>
  );
}
