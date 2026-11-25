import { useEffect, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { ApiError } from "../api/client";
import {
  type AgentConversation,
  type AgentLink,
  type AgentMessage,
  type AgentRun,
  isRunFinished,
  useAgentConversation,
  useAgentRun,
  useCancelAgentRun,
  useStartAgentRun,
} from "../api/chat";
import { useJob } from "../api/hooks";
import { ArrowRight, ArrowUp, CircleCheck, FileDown, Hourglass, MessageSquarePlus, Sparkles, Square, X } from "lucide-react";
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
  grade_all_students: "Started Grade all",
  grade_all_assignments_of_student: "Started grading all the student's assignments",
  analyze_common_mistakes: "Started the Common mistakes analysis",
  analyze_student: "Analyzed the student",
  export_grading_pdf: "Prepared the PDF report",
  wait_for_jobs: "Waiting for the background work",
};

export type OpenPage = { description: string | null; courseId?: string; assignmentId?: string };

export function ChatPanel({ page, onClose }: { page: OpenPage; onClose: () => void }) {
  const queryClient = useQueryClient();
  const { conversationId, setConversationId, clear } = useChatConversationId();
  const conversation = useAgentConversation(conversationId);
  const start = useStartAgentRun();
  const cancel = useCancelAgentRun();
  const [runId, setRunId] = useState<string | null>(null);
  const run = useAgentRun(runId);
  const [input, setInput] = useState("");
  const [failure, setFailure] = useState<string | null>(null);
  const bottom = useRef<HTMLDivElement>(null);

  const stored = conversation.data?.messages ?? [];
  const storedIds = new Set(stored.map((m) => m.id));
  const live = (run.data?.messages ?? []).filter((m) => !storedIds.has(m.id));
  const messages = [...stored, ...live].sort((a, b) => a.sequence - b.sequence);
  const lastOfRun = new Map(messages.map((m) => [m.runId, m.id]));
  const last = messages[messages.length - 1];
  const quickReplies = last && last.role === "Assistant" ? repliesOf(messages, last.runId) : [];
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
    if (run.data.status === "Canceled") {
      setFailure("Stopped. Work it had already started keeps running in the background.");
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
              stored submissions, fetch new submissions, grade one student or a whole assignment, analyze an assignment's common
              mistakes, and wait for that work to finish.
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
              <div key={message.id} className="contents">
                <Message message={message} />
                {lastOfRun.get(message.runId) === message.id && <RunLinks links={linksOf(messages, message.runId)} onOpen={onClose} />}
              </div>
            ))}
            {!waiting && !failure && quickReplies.length > 0 && <QuickReplies replies={quickReplies} onPick={send} />}
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
            {waiting ? (
              <Button
                small
                type="button"
                icon={<Square />}
                onClick={() => runId && cancel.mutate(runId)}
                disabled={runId === null || cancel.isPending}
                title="Stop the agent; work it already started keeps running"
              >
                {cancel.isPending ? "Stopping…" : "Stop"}
              </Button>
            ) : (
              <Button small variant="primary" type="submit" icon={<ArrowUp />} disabled={!input.trim()}>
                Send
              </Button>
            )}
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
    const finished = message.text.startsWith("The background work you waited for has finished");
    return (
      <details className="pl-8 text-[11px] text-ink-muted">
        <summary className="flex cursor-pointer list-none items-center gap-1.5">
          {finished ? <CircleCheck className="h-3.5 w-3.5 text-success" /> : <Hourglass className="h-3.5 w-3.5" />}
          {finished ? "The background work finished" : "Still waiting for the background work"}
          <span className="text-accent">· details</span>
        </summary>
        <p className="mt-1 whitespace-pre-wrap rounded-lg bg-paper px-2.5 py-2">{message.text}</p>
      </details>
    );
  }

  return (
    <>
      {message.toolCalls.filter((tool) => tool !== "suggest_replies").map((tool, i) => (
        <div key={i} className="flex items-center gap-1.5 pl-8 text-[11px] text-ink-muted">
          <CircleCheck className="h-3.5 w-3.5 text-success" />
          {toolLabels[tool] ?? `Used ${tool}`}
        </div>
      ))}
      {message.text && <Reply text={message.text} />}
    </>
  );
}

/** The answers the agent offered with its question: those of the run's last suggest_replies call. */
function repliesOf(messages: AgentMessage[], runId: string): string[] {
  const offered = messages.filter((m) => m.runId === runId && (m.replies?.length ?? 0) > 0);
  return offered.length === 0 ? [] : offered[offered.length - 1].replies;
}

function QuickReplies({ replies, onPick }: { replies: string[]; onPick: (text: string) => void }) {
  return (
    <div className="flex flex-wrap gap-1.5 pl-8" role="group" aria-label="Quick answers">
      {replies.map((reply, i) => (
        <button
          key={reply}
          onClick={() => onPick(reply)}
          className={`rounded-full border px-3 py-1 text-xs font-semibold transition-colors ${
            i === 0
              ? "border-accent bg-accent text-white hover:bg-accent-strong"
              : "border-line bg-white text-ink hover:border-accent hover:text-accent"
          }`}
          title="Send this answer"
        >
          {reply}
        </button>
      ))}
    </div>
  );
}

function linksOf(messages: AgentMessage[], runId: string): AgentLink[] {
  const seen = new Set<string>();
  return messages
    .filter((m) => m.runId === runId)
    .flatMap((m) => m.links ?? [])
    .filter((link) => {
      const key = `${link.kind}:${link.courseId}:${link.assignmentId ?? ""}:${link.submissionId ?? ""}:${link.studentId ?? ""}`;
      if (seen.has(key)) {
        return false;
      }
      seen.add(key);
      return true;
    });
}

function linkPath(link: AgentLink): string {
  const course = `/courses/${link.courseId}`;
  switch (link.kind) {
    case "CommonMistakes":
      return `${course}/mistakes?assignment=${link.assignmentId ?? ""}`;
    case "Submission":
      return `${course}/assignments/${link.assignmentId ?? ""}?student=${link.submissionId ?? ""}`;
    case "Assignment":
      return `${course}/assignments/${link.assignmentId ?? ""}`;
    case "Student":
      return `${course}/students?student=${link.studentId ?? ""}`;
    case "GradingPdf":
      return `/api/gradings/${link.gradingId ?? ""}/export/pdf`;
    case "StudentOverview":
      return `${course}/students/${link.studentId ?? ""}/overview`;
    case "StudentMistakes":
      return `${course}/students/${link.studentId ?? ""}/mistakes`;
    case "StudentStrengths":
      return `${course}/students/${link.studentId ?? ""}/strengths`;
    case "Tests":
      return `${course}/assignments/${link.assignmentId ?? ""}?view=tests`;
  }
}

function RunLinks({ links, onOpen }: { links: AgentLink[]; onOpen: () => void }) {
  if (links.length === 0) {
    return null;
  }

  return (
    <div className="flex flex-wrap gap-1.5 pl-8">
      {links.map((link) =>
        link.kind === "GradingPdf" ? (
          <a
            key={linkPath(link)}
            href={linkPath(link)}
            download
            className="flex items-center gap-1 rounded-full border border-accent bg-accent px-2.5 py-1 text-[11px] font-semibold text-white transition-colors hover:bg-accent-strong"
            title="Download the PDF report of this grading"
          >
            <FileDown className="h-3 w-3" />
            {link.label}
          </a>
        ) : (
          <Link
            key={linkPath(link)}
            to={linkPath(link)}
            onClick={() => {
              if (window.matchMedia("(max-width: 1279px)").matches) {
                onOpen();
              }
            }}
            className="flex items-center gap-1 rounded-full border border-line bg-white px-2.5 py-1 text-[11px] font-semibold text-accent transition-colors hover:border-accent"
          >
            {link.label}
            <ArrowRight className="h-3 w-3" />
          </Link>
        ),
      )}
    </div>
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
  const jobIds = run?.status === "Waiting" ? (run.waitingJobIds ?? []) : [];
  if (jobIds.length > 0) {
    return (
      <div className="flex items-start gap-2">
        <AgentMark size="mt-0.5 h-6 w-6" />
        <div className="min-w-0 flex-1 rounded-xl rounded-tl-sm bg-paper px-3.5 py-3">
          <div className="flex items-center gap-2 text-ink-muted">
            <Hourglass className="h-3.5 w-3.5" />
            Waiting for {jobIds.length === 1 ? "a background job" : `${jobIds.length} background jobs`}; it continues when they finish
          </div>
          <ul className="mt-2 flex flex-col gap-1">
            {jobIds.map((jobId) => (
              <WaitedJob key={jobId} jobId={jobId} />
            ))}
          </ul>
        </div>
      </div>
    );
  }

  return (
    <div className="flex items-start gap-2">
      <AgentMark size="mt-0.5 h-6 w-6" />
      <div className="flex items-center gap-2 rounded-xl rounded-tl-sm bg-paper px-3.5 py-3 text-ink-muted">
        <span className="h-3.5 w-3.5 animate-spin rounded-full border-2 border-line border-t-accent" />
        {run?.status === "Running" ? "Working…" : "Queued…"}
      </div>
    </div>
  );
}

const jobLabels: Record<string, string> = {
  GradeVersion: "Grading",
  GradeAssignment: "Grade all",
  GradeStudent: "Grade student",
  AnalyzeMistakes: "Common mistakes",
  AnalyzeStudent: "Student analysis",
};

function WaitedJob({ jobId }: { jobId: string }) {
  const job = useJob(jobId).data;
  if (!job) {
    return <li className="text-[11px] text-ink-muted">Loading…</li>;
  }

  const progress = job.progressTotal ? ` · ${job.progressDone ?? 0} of ${job.progressTotal}` : "";
  const state =
    job.status === "Succeeded" ? "done" : job.status === "Failed" ? "failed" : job.status === "Canceled" ? "canceled" : job.status === "Running" ? "running" : "queued";
  return (
    <li className="flex items-center gap-1.5 text-[11px]">
      {job.status === "Succeeded" ? (
        <CircleCheck className="h-3.5 w-3.5 text-success" />
      ) : job.status === "Failed" || job.status === "Canceled" ? (
        <X className="h-3.5 w-3.5 text-danger" />
      ) : (
        <span className="h-3 w-3 animate-spin rounded-full border-2 border-line border-t-accent" />
      )}
      <span>
        {jobLabels[job.type] ?? job.type} {state}
        {progress}
      </span>
    </li>
  );
}

function AgentMark({ size }: { size: string }) {
  return (
    <span aria-hidden="true" className={`flex shrink-0 items-center justify-center rounded-lg bg-accent text-white ${size}`}>
      <Sparkles className="h-[55%] w-[55%]" />
    </span>
  );
}
