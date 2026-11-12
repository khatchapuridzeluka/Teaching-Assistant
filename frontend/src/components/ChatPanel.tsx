import { useEffect, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { ApiError } from "../api/client";
import {
  type AgentConversation,
  type AgentMessage,
  type AgentRun,
  isRunFinished,
  useAgentConversation,
  useAgentRun,
  useStartAgentRun,
} from "../api/chat";
import { ArrowUp, CircleCheck, MessageSquarePlus, Sparkles, Wrench, X } from "lucide-react";
import { Button } from "./ui";
import { useChatConversationId } from "./useChatHistory";

const suggestions = [
  "What courses do I have?",
  "Which assignments are in my Java course?",
  "Draft rubric criteria for a Strategy pattern assignment",
];

const toolLabels: Record<string, string> = {
  list_courses: "Listed your courses",
  list_assignments: "Listed the assignments",
  list_submissions: "Listed the stored submissions",
  fetch_submissions: "Fetched the submissions from Classroom",
  find_students: "Looked up the student",
  list_student_assignments: "Listed the student's assignments",
  grade_student_assignment: "Started a grading",
  regrade_student_assignment: "Started a grading again",
};

export type OpenPage = { description: string | null; courseId?: string; assignmentId?: string };

export function ChatPanel({ page, onClose }: { page: OpenPage; onClose: () => void }) {
  const queryClient = useQueryClient();
  const { conversationId, setConversationId, clear } = useChatConversationId();
  const conversation = useAgentConversation(conversationId);
  const start = useStartAgentRun();
  const [runId, setRunId] = useState<string | null>(null);
  const run = useAgentRun(runId);
  const [input, setInput] = useState("");
  const [failure, setFailure] = useState<string | null>(null);
  const bottom = useRef<HTMLDivElement>(null);

  const messages = conversation.data?.messages ?? [];
  const activeRunId = conversation.data?.activeRunId ?? null;
  const finished = run.data !== undefined && isRunFinished(run.data.status);
  const waiting = start.isPending || (runId !== null && !finished);

  useEffect(() => {
    if (activeRunId !== null && runId === null) {
      setRunId(activeRunId);
    }
  }, [activeRunId, runId]);

  const missing = conversation.error instanceof ApiError && conversation.error.status === 404;
  useEffect(() => {
    if (missing) {
      clear();
    }
  }, [missing, clear]);

  useEffect(() => {
    if (!finished || !run.data) {
      return;
    }

    if (run.data.status === "Failed") {
      setFailure(run.data.error ?? "The agent could not finish.");
    }
    const key = ["agent", "conversations", run.data.conversationId];
    queryClient.setQueryData(key, (current: AgentConversation | undefined) => current && { ...current, activeRunId: null });
    void queryClient.invalidateQueries({ queryKey: key });
    setRunId(null);
  }, [finished, run.data, queryClient]);

  useEffect(() => {
    bottom.current?.scrollIntoView({ block: "end" });
  }, [messages.length, waiting, failure]);

  function send(text: string) {
    const question = text.trim();
    if (!question || waiting) {
      return;
    }

    setFailure(null);
    setInput("");
    start.mutate(
      {
        text: question,
        conversationId,
        page: page.description,
        courseId: page.courseId ?? null,
        assignmentId: page.assignmentId ?? null,
      },
      {
        onSuccess: (started) => {
          setConversationId(started.conversationId);
          queryClient.setQueryData(["agent", "conversations", started.conversationId], (current: AgentConversation | undefined) => ({
            id: started.conversationId,
            activeRunId: started.id,
            messages: [...(current?.messages ?? []), ...started.messages],
          }));
          setRunId(started.id);
        },
        onError: (error) => {
          setInput(question);
          setFailure(error instanceof Error ? error.message : "Something went wrong.");
        },
      },
    );
  }

  function newConversation() {
    clear();
    setRunId(null);
    setFailure(null);
  }

  const empty = messages.length === 0 && !waiting && !failure;

  return (
    <aside className="flex h-full w-full max-w-[400px] shrink-0 animate-slide-left flex-col border-l border-line bg-white xl:w-[400px]">
      <header className="flex items-center justify-between gap-3 border-b border-line px-4 py-3">
        <div className="flex min-w-0 items-center gap-2.5">
          <AgentMark size="h-8 w-8" />
          <div className="min-w-0">
            <div className="font-display text-[17px] font-semibold leading-tight">Ask agent</div>
            <div className="truncate text-[11px] text-ink-muted">{page.description ?? "General question"}</div>
          </div>
        </div>
        <div className="flex shrink-0 gap-1">
          <Button
            small
            variant="ghost"
            icon={<MessageSquarePlus />}
            onClick={newConversation}
            disabled={waiting || conversationId === null}
            title="Start a new conversation"
          >
            New
          </Button>
          <Button small variant="ghost" className="px-1.5" onClick={onClose} aria-label="Close" title="Close">
            <X className="h-4 w-4" />
          </Button>
        </div>
      </header>

      <div className="flex-1 overflow-y-auto px-[18px] py-4">
        {empty ? (
          <div className="flex flex-col gap-3">
            <p className="text-ink-muted">
              Ask about teaching, rubrics or feedback wording, or about your Classroom: the agent can list your courses, assignments, students and
              stored submissions, fetch new submissions and start a grading for a student.
            </p>
            <div className="eyebrow mt-1">Try</div>
            <div className="flex flex-col gap-1.5">
              {suggestions.map((suggestion) => (
                <button
                  key={suggestion}
                  onClick={() => send(suggestion)}
                  className="flex items-start gap-2 rounded-lg border border-line bg-white px-3 py-2 text-left text-xs font-medium shadow-card transition-all hover:-translate-y-px hover:border-accent hover:shadow-raised"
                >
                  <Sparkles className="mt-0.5 h-3.5 w-3.5 shrink-0 text-accent" />
                  {suggestion}
                </button>
              ))}
            </div>
          </div>
        ) : (
          <div className="flex flex-col gap-3.5">
            {messages.map((message) => (
              <Message key={message.id} message={message} />
            ))}
            {waiting && <Working run={run.data} />}
            {failure && <Reply text={failure} error />}
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
            placeholder={waiting ? "The agent is working…" : "Ask the agent…"}
            className="block w-full resize-none rounded-[10px] bg-transparent px-3 pt-2.5 outline-none placeholder:text-ink-faint"
          />
          <div className="flex justify-end px-2 pb-2">
            <Button small variant="primary" type="submit" icon={<ArrowUp />} disabled={!input.trim() || waiting}>
              Send
            </Button>
          </div>
        </div>
        <div className="mt-1.5 text-[11px] text-ink-muted">
          Enter to send · Shift+Enter for a new line. The conversation is saved; the agent keeps working in the background if you close
          the panel.
        </div>
      </form>
    </aside>
  );
}

function Message({ message }: { message: AgentMessage }) {
  if (message.role === "User") {
    return (
      <div className="max-w-[88%] animate-rise self-end whitespace-pre-wrap rounded-xl rounded-br-sm bg-navy px-3 py-2.5 text-white">
        {message.text}
      </div>
    );
  }

  if (message.role === "Tool") {
    return null;
  }

  if (message.role === "Notice") {
    return <div className="pl-8 text-[11px] text-ink-muted">{message.text}</div>;
  }

  return (
    <>
      {message.toolCalls.map((tool, i) => (
        <div key={i} className="flex items-center gap-1.5 pl-8 text-[11px] text-ink-muted">
          <CircleCheck className="h-3.5 w-3.5 text-success" />
          {toolLabels[tool] ?? `Used ${tool}`}
        </div>
      ))}
      {message.text && <Reply text={message.text} />}
    </>
  );
}

function Reply({ text, error = false }: { text: string; error?: boolean }) {
  return (
    <div className="flex min-w-0 animate-rise items-start gap-2">
      <AgentMark size="mt-0.5 h-6 w-6" />
      <div
        className={`min-w-0 flex-1 rounded-xl rounded-tl-sm px-3.5 py-3 ${error ? "border border-danger/30 bg-danger-tint/50 text-danger-ink" : "bg-paper"}`}
      >
        <div className="markdown">
          <ReactMarkdown remarkPlugins={[remarkGfm]}>{text}</ReactMarkdown>
        </div>
      </div>
    </div>
  );
}

function Working({ run }: { run: AgentRun | undefined }) {
  const label = run?.status === "Running" ? "Working…" : run?.status === "Waiting" ? "Waiting for a background job…" : "Queued…";
  return (
    <div className="flex items-start gap-2">
      <AgentMark size="mt-0.5 h-6 w-6" />
      <div className="flex items-center gap-2 rounded-xl rounded-tl-sm bg-paper px-3.5 py-3 text-ink-muted">
        {run?.status === "Waiting" ? (
          <Wrench className="h-3.5 w-3.5" />
        ) : (
          <span className="h-3.5 w-3.5 animate-spin rounded-full border-2 border-line border-t-accent" />
        )}
        {label}
      </div>
    </div>
  );
}

function AgentMark({ size }: { size: string }) {
  return (
    <span aria-hidden="true" className={`flex shrink-0 items-center justify-center rounded-lg bg-accent text-white ${size}`}>
      <Sparkles className="h-[55%] w-[55%]" />
    </span>
  );
}
