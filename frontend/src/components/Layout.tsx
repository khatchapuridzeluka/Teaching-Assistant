import { type ReactNode, useState } from "react";
import { Link, NavLink, Outlet, useMatches } from "react-router";
import { useApiHealth, useAssignments, useCourses, useGoogleStatus } from "../api/hooks";
import { ChatPanel } from "./ChatPanel";
import { Button } from "./ui";

// Pages describe their breadcrumb via route handles; the top bar shows them.
export type Crumb = { label: ReactNode; to?: string };
export type CrumbHandle = { crumbs: (params: Record<string, string | undefined>) => Crumb[] };

const navClass = ({ isActive }: { isActive: boolean }) =>
  `flex items-center justify-between rounded-[7px] px-2.5 py-2 text-[13px] ${
    isActive ? "bg-accent font-semibold text-white" : "text-[#d4d5d6] hover:bg-ink-soft"
  }`;

export function Layout() {
  const [chatOpen, setChatOpen] = useState(false);
  const page = usePageDescription();

  return (
    <div className="flex h-screen overflow-hidden">
      <Sidebar />
      <div className="flex min-w-0 flex-1 flex-col">
        <TopBar chatOpen={chatOpen} onToggleChat={() => setChatOpen(!chatOpen)} />
        <main className="flex-1 overflow-y-auto px-7 py-6">
          <div className="mx-auto flex max-w-[1180px] flex-col gap-[18px]">
            <Outlet />
          </div>
        </main>
      </div>
      {/* Hidden rather than removed when closed, so the conversation survives closing and navigating. */}
      <div className={chatOpen ? "flex" : "hidden"}>
        <ChatPanel page={page} onClose={() => setChatOpen(false)} />
      </div>
    </div>
  );
}

// Where the teacher is, in words, for the agent: course and assignment names only, never student data.
function usePageDescription(): string | null {
  const matches = useMatches();
  const { courseId = "", assignmentId = "" } = (matches[matches.length - 1]?.params ?? {}) as Record<string, string | undefined>;
  const course = useCourses().data?.find((c) => c.id === courseId);
  const assignment = useAssignments(courseId).data?.find((a) => a.id === assignmentId);

  if (assignment && course) return `Assignment '${assignment.title}' in course '${course.name}'`;
  if (course) return `Course '${course.name}' (list of assignments)`;
  return null;
}

function Sidebar() {
  const courses = useCourses();

  return (
    <aside className="flex w-[232px] shrink-0 flex-col bg-ink px-3.5 py-5 text-paper">
      <Link to="/" className="flex items-center gap-2.5 px-2 pb-[18px]">
        <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-accent text-xs font-bold text-white">TA</span>
        <span>
          <b className="block text-sm text-white">Teaching Assistant</b>
          <span className="text-[11px] text-[#a9abac]">Grading agent</span>
        </span>
      </Link>

      <div className="px-2.5 pb-1.5 pt-3.5 text-[10px] uppercase tracking-[0.08em] text-[#8a8d8f]">Workspace</div>
      <NavLink to="/" end className={navClass}>Courses</NavLink>
      <NavLink to="/settings" className={navClass}>Settings</NavLink>

      <div className="px-2.5 pb-1.5 pt-3.5 text-[10px] uppercase tracking-[0.08em] text-[#8a8d8f]">Courses</div>
      {courses.data?.map((course) => (
        <NavLink key={course.id} to={`/courses/${course.id}`} className={navClass}>
          <span className="truncate">{course.name}</span>
        </NavLink>
      ))}
      {courses.isError && <div className="px-2.5 text-[11px] text-[#a9abac]">Connect Google to see courses</div>}

      <div className="flex-1" />
      <GoogleCard />
    </aside>
  );
}

function GoogleCard() {
  const status = useGoogleStatus();
  const connected = status.data?.state === "Connected";
  const labels = {
    Connected: "Connected · read-only",
    NotConnected: "Not connected · click to connect",
    ReconnectRequired: "Access expired · click to reconnect",
    NotConfigured: "Not configured",
  } as const;
  const label = status.isLoading ? "Checking…" : status.data ? labels[status.data.state] : "Unavailable";

  return (
    <Link to="/settings" className="rounded-[9px] border border-ink-soft p-3 text-[11px] text-[#a9abac] hover:border-[#8a8d8f]">
      <b className="mb-0.5 flex items-center gap-2 text-xs text-white">
        <span className={`h-2 w-2 rounded-full border-[1.5px] border-white ${connected ? "bg-accent" : "bg-transparent"}`} />
        Google Classroom
      </b>
      {label}
    </Link>
  );
}

// Breadcrumb labels for IDs in the URL; the names come from the already cached lists.
export function CourseName({ courseId }: { courseId: string }) {
  const courses = useCourses();
  return <>{courses.data?.find((c) => c.id === courseId)?.name ?? "Course"}</>;
}

export function AssignmentTitle({ courseId, assignmentId }: { courseId: string; assignmentId: string }) {
  const assignments = useAssignments(courseId);
  return <>{assignments.data?.find((a) => a.id === assignmentId)?.title ?? "Assignment"}</>;
}

function TopBar({ chatOpen, onToggleChat }: { chatOpen: boolean; onToggleChat: () => void }) {
  const matches = useMatches();
  const health = useApiHealth();
  const crumbs = matches.flatMap((match) =>
    (match.handle as CrumbHandle | undefined)?.crumbs(match.params) ?? [],
  );

  return (
    <header className="flex h-16 shrink-0 items-center justify-between border-b border-line bg-white px-7">
      <nav className="flex min-w-0 items-center gap-2 text-xs text-ink-muted">
        {crumbs.map((crumb, i) => (
          <span key={i} className="flex items-center gap-2">
            {i > 0 && <span>/</span>}
            {crumb.to && i < crumbs.length - 1 ? (
              <Link to={crumb.to} className="hover:text-ink">{crumb.label}</Link>
            ) : (
              <b className="truncate font-semibold text-ink">{crumb.label}</b>
            )}
          </span>
        ))}
      </nav>
      <div className="flex shrink-0 items-center gap-4">
        <span className="flex items-center gap-2 text-xs text-ink-muted">
          <span className={`h-2 w-2 rounded-full ${health.isSuccess ? "bg-accent" : "bg-line"}`} />
          {health.isSuccess ? "API online" : health.isLoading ? "Checking API…" : "API offline"}
        </span>
        <Button variant={chatOpen ? "outline" : "dark"} onClick={onToggleChat}>
          {chatOpen ? "Hide agent" : "Ask agent"}
        </Button>
      </div>
    </header>
  );
}
