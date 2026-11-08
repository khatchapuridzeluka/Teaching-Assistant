import { type ReactNode, useEffect, useState } from "react";
import { Link, NavLink, Outlet, useLocation, useMatch, useMatches } from "react-router";
import { useApiHealth, useAssignments, useCourses, useGoogleStatus } from "../api/hooks";
import { coursePath, courseSections, workspaceSections } from "../navigation";
import { ChatPanel } from "./ChatPanel";
import { Button } from "./ui";

export type Crumb = { label: ReactNode; to?: string };
export type CrumbHandle = { crumbs: (params: Record<string, string | undefined>) => Crumb[] };

const navClass = ({ isActive }: { isActive: boolean }) =>
  `flex items-center justify-between rounded-[7px] px-2.5 py-2 text-[13px] ${
    isActive ? "bg-accent font-semibold text-white" : "text-[#d4d5d6] hover:bg-ink-soft"
  }`;

export function Layout() {
  const [chatOpen, setChatOpen] = useState(false);
  const [navOpen, setNavOpen] = useState(false);
  const page = usePageDescription();
  const location = useLocation();

  useEffect(() => {
    setNavOpen(false);
  }, [location.pathname]);

  return (
    <div className="flex h-dvh overflow-hidden">
      <Sidebar className="hidden lg:flex" />
      {navOpen && (
        <div className="fixed inset-0 z-50 flex lg:hidden">
          <div className="absolute inset-0 bg-ink/50" onClick={() => setNavOpen(false)} />
          <Sidebar className="relative flex shadow-xl" />
        </div>
      )}
      <div className="flex min-w-0 flex-1 flex-col">
        <TopBar chatOpen={chatOpen} onToggleChat={() => setChatOpen(!chatOpen)} onOpenNav={() => setNavOpen(true)} />
        <main className="flex-1 overflow-y-auto px-4 py-4 lg:px-7 lg:py-6">
          <div className="flex flex-col gap-[18px]">
            <Outlet />
          </div>
        </main>
      </div>
      <div
        className={chatOpen ? "fixed inset-0 z-50 flex justify-end bg-ink/40 xl:static xl:z-auto xl:bg-transparent" : "hidden"}
        onClick={(e) => e.target === e.currentTarget && setChatOpen(false)}
      >
        <ChatPanel page={page} onClose={() => setChatOpen(false)} />
      </div>
    </div>
  );
}

function usePageDescription(): string | null {
  const matches = useMatches();
  const { courseId = "", assignmentId = "" } = (matches[matches.length - 1]?.params ?? {}) as Record<string, string | undefined>;
  const course = useCourses().data?.find((c) => c.id === courseId);
  const assignment = useAssignments(courseId).data?.find((a) => a.id === assignmentId);

  if (assignment && course) {
    return `Assignment '${assignment.title}' in course '${course.name}'`;
  }
  if (course) {
    return `Course '${course.name}' (list of assignments)`;
  }
  return null;
}

const sectionClass = ({ isActive }: { isActive: boolean }) =>
  `flex items-center justify-between gap-2 rounded-[6px] px-2.5 py-1.5 text-xs ${
    isActive ? "bg-accent font-semibold text-white" : "text-[#c4c6c7] hover:bg-ink-soft"
  }`;

function Sidebar({ className = "" }: { className?: string }) {
  const courses = useCourses();
  const [coursesOpen, setCoursesOpen] = useState(true);
  const currentCourseId = useMatch("/courses/:courseId/*")?.params.courseId;

  return (
    <aside className={`w-[232px] shrink-0 flex-col overflow-y-auto bg-ink px-3.5 py-5 text-paper ${className}`}>
      <Link to="/" className="flex items-center gap-2.5 px-2 pb-[18px]">
        <img src="/favicon.svg" alt="" className="h-8 w-8 shrink-0" />
        <span>
          <b className="block text-sm text-white">Teaching Assistant</b>
          <span className="text-[11px] text-[#a9abac]">Grading agent</span>
        </span>
      </Link>

      <div className="px-2.5 pb-1.5 pt-3.5 text-[10px] uppercase tracking-[0.08em] text-[#8a8d8f]">Workspace</div>

      <div className="flex items-center gap-1">
        <NavLink to="/" end className={(state) => `${navClass(state)} flex-1`}>Courses</NavLink>
        <button
          onClick={() => setCoursesOpen(!coursesOpen)}
          aria-expanded={coursesOpen}
          aria-label={coursesOpen ? "Hide courses" : "Show courses"}
          className="flex h-8 w-7 shrink-0 items-center justify-center rounded-[7px] text-[#a9abac] hover:bg-ink-soft hover:text-white"
        >
          {coursesOpen ? "▾" : "▸"}
        </button>
      </div>

      {coursesOpen && (
        <div className="mb-1 ml-3 flex flex-col gap-0.5 border-l border-ink-soft pl-2">
          {courses.isLoading && <div className="px-2.5 py-1 text-[11px] text-[#a9abac]">Loading…</div>}
          {courses.isError && <div className="px-2.5 py-1 text-[11px] text-[#a9abac]">Connect Google to see courses</div>}
          {courses.data?.map((course) => {
            const current = course.id === currentCourseId;
            return (
              <div key={course.id}>
                <Link
                  to={coursePath(course.id)}
                  className={`block truncate rounded-[6px] px-2.5 py-1.5 text-[13px] ${
                    current ? "font-semibold text-white" : "text-[#d4d5d6] hover:bg-ink-soft"
                  }`}
                >
                  {course.name}
                </Link>
                {current && (
                  <div className="mb-1 ml-2 flex flex-col gap-0.5">
                    {courseSections.map((section) => (
                      <NavLink key={section.path} to={coursePath(course.id, section.path)} end={section.path === ""} className={sectionClass}>
                        <span className="truncate">{section.label}</span>
                        {!section.ready && <SoonBadge />}
                      </NavLink>
                    ))}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}

      {workspaceSections.map((section) => (
        <NavLink key={section.path} to={`/${section.path}`} className={navClass}>
          <span>{section.label}</span>
          {!section.ready && <SoonBadge />}
        </NavLink>
      ))}
      <NavLink to="/settings" className={navClass}>Settings</NavLink>

      <div className="flex-1" />
      <GoogleCard />
    </aside>
  );
}

function SoonBadge() {
  return (
    <span className="shrink-0 rounded-full border border-[#5a5f63] px-1.5 text-[9px] font-semibold uppercase tracking-[0.06em] text-[#8a8d8f]">
      Soon
    </span>
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

export function CourseName({ courseId }: { courseId: string }) {
  const courses = useCourses();
  return <>{courses.data?.find((c) => c.id === courseId)?.name ?? "Course"}</>;
}

export function AssignmentTitle({ courseId, assignmentId }: { courseId: string; assignmentId: string }) {
  const assignments = useAssignments(courseId);
  return <>{assignments.data?.find((a) => a.id === assignmentId)?.title ?? "Assignment"}</>;
}

function TopBar({ chatOpen, onToggleChat, onOpenNav }: { chatOpen: boolean; onToggleChat: () => void; onOpenNav: () => void }) {
  const matches = useMatches();
  const health = useApiHealth();
  const crumbs = matches.flatMap((match) =>
    (match.handle as CrumbHandle | undefined)?.crumbs(match.params) ?? [],
  );

  return (
    <header className="flex h-16 shrink-0 items-center justify-between gap-3 border-b border-line bg-white px-4 lg:px-7">
      <div className="flex min-w-0 items-center gap-3">
        <button
          onClick={onOpenNav}
          aria-label="Open navigation"
          className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg border border-line text-base lg:hidden"
        >
          ☰
        </button>
        <nav className="flex min-w-0 items-center gap-2 overflow-hidden text-xs text-ink-muted">
          {crumbs.map((crumb, i) => (
            <span key={i} className={`min-w-0 items-center gap-2 ${i < crumbs.length - 2 ? "hidden md:flex" : "flex"}`}>
              {i > 0 && <span>/</span>}
              {crumb.to && i < crumbs.length - 1 ? (
                <Link to={crumb.to} className="truncate hover:text-ink">{crumb.label}</Link>
              ) : (
                <b className="truncate font-semibold text-ink">{crumb.label}</b>
              )}
            </span>
          ))}
        </nav>
      </div>
      <div className="flex shrink-0 items-center gap-4">
        <span className="flex items-center gap-2 text-xs text-ink-muted" title={health.isSuccess ? "API online" : "API offline"}>
          <span className={`h-2 w-2 rounded-full ${health.isSuccess ? "bg-accent" : "bg-line"}`} />
          <span className="hidden sm:inline">{health.isSuccess ? "API online" : health.isLoading ? "Checking API…" : "API offline"}</span>
        </span>
        <Button variant={chatOpen ? "outline" : "dark"} onClick={onToggleChat}>
          {chatOpen ? "Hide agent" : "Ask agent"}
        </Button>
      </div>
    </header>
  );
}
