import { type ReactNode, useEffect, useRef, useState } from "react";
import { Link, NavLink, Outlet, useLocation, useMatch, useMatches, useNavigate } from "react-router";
import { Check, ChevronRight, ChevronsUpDown, House, Lock, Menu as MenuIcon, Search, Settings, Sparkles, X } from "lucide-react";
import { useApiHealth, useAssignments, useCourses, useGoogleStatus, useJobOverview, useReviewQueue } from "../api/hooks";
import { formatDay } from "../format";
import { courseCode, coursePath, courseSections, workspaceSections } from "../navigation";
import { courseTone } from "../theme";
import { ChatPanel } from "./ChatPanel";
import { CommandPalette } from "./CommandPalette";
import { JobToastProvider } from "./JobToasts";
import { Kbd } from "./ui";

export type Crumb = { label: ReactNode; to?: string };
export type CrumbHandle = { crumbs: (params: Record<string, string | undefined>) => Crumb[] };

export function Layout() {
  const [chatOpen, setChatOpen] = useState(false);
  const [navOpen, setNavOpen] = useState(false);
  const [paletteOpen, setPaletteOpen] = useState(false);
  const page = usePageDescription();
  const location = useLocation();
  const courseId = useMatch("/courses/:courseId/*")?.params.courseId;

  useEffect(() => {
    setNavOpen(false);
  }, [location.pathname]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setPaletteOpen((open) => !open);
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  return (
    <JobToastProvider>
      <div className="flex h-dvh overflow-hidden">
        <Sidebar courseId={courseId} onOpenPalette={() => setPaletteOpen(true)} className="hidden lg:flex" />
        <div className="flex min-w-0 flex-1 flex-col">
          <MobileBar onOpenNav={() => setNavOpen(true)} onOpenPalette={() => setPaletteOpen(true)} />
          <div className="flex min-h-0 flex-1">
            <main className="min-w-0 flex-1 overflow-y-auto px-4 py-4 lg:px-6 lg:py-5">
              <Breadcrumbs />
              <div key={location.pathname} className="stagger flex flex-col gap-4">
                <Outlet />
              </div>
            </main>
            <div
              className={chatOpen ? "modal-overlay fixed inset-0 z-50 flex justify-end xl:static xl:z-auto xl:bg-transparent xl:backdrop-filter-none" : "hidden"}
              onClick={(e) => e.target === e.currentTarget && setChatOpen(false)}
            >
              <ChatPanel page={page} onClose={() => setChatOpen(false)} />
            </div>
          </div>
        </div>
      </div>

      {!chatOpen && (
        <button
          onClick={() => setChatOpen(true)}
          className="fixed bottom-5 right-5 z-40 flex h-11 items-center gap-2 rounded-full bg-accent px-4 text-[13px] font-semibold text-white shadow-raised transition-all hover:brightness-95 active:translate-y-px"
          title="Ask the agent about this page, teaching, rubrics or feedback wording"
        >
          <Sparkles className="h-4 w-4" />
          Ask agent
        </button>
      )}

      {navOpen && (
        <div className="fixed inset-0 z-50 flex lg:hidden">
          <div className="modal-overlay absolute inset-0" onClick={() => setNavOpen(false)} />
          <div className="relative flex w-[280px] max-w-[86vw] animate-slide-right">
            <Sidebar
              courseId={courseId}
              onOpenPalette={() => {
                setNavOpen(false);
                setPaletteOpen(true);
              }}
              onClose={() => setNavOpen(false)}
              className="flex w-full shadow-pop"
            />
          </div>
        </div>
      )}

      {paletteOpen && <CommandPalette courseId={courseId} onClose={() => setPaletteOpen(false)} />}
    </JobToastProvider>
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

function LogoMark({ className = "h-7 w-7" }: { className?: string }) {
  return (
    <svg viewBox="0 0 32 32" className={`shrink-0 ${className}`} aria-hidden>
      <rect width="32" height="32" rx="8" fill="#1857d6" />
      <path d="M10.5 9.5 5.8 16l4.7 6.5" fill="none" stroke="#ffffff" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M21.5 9.5 26.2 16l-4.7 6.5" fill="none" stroke="#ffffff" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />
      <path d="m12.6 16.4 2.5 2.6 4.5-5.6" fill="none" stroke="#bcd0ff" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function Wordmark() {
  return (
    <Link to="/" className="flex min-w-0 items-center gap-2.5" title="Home">
      <LogoMark />
      <span className="truncate text-[15px] font-semibold leading-none tracking-[-0.01em] text-ink">Teaching Assistant</span>
    </Link>
  );
}

function MobileBar({ onOpenNav, onOpenPalette }: { onOpenNav: () => void; onOpenPalette: () => void }) {
  return (
    <header className="flex h-12 shrink-0 items-center gap-2 border-b border-line bg-white px-3 lg:hidden">
      <button
        onClick={onOpenNav}
        aria-label="Open navigation"
        className="flex h-8 w-8 shrink-0 items-center justify-center rounded-md text-ink-soft hover:bg-paper"
      >
        <MenuIcon className="h-[18px] w-[18px]" />
      </button>
      <Wordmark />
      <div className="flex-1" />
      <button
        onClick={onOpenPalette}
        aria-label="Jump to a course, assignment or student"
        className="flex h-8 w-8 shrink-0 items-center justify-center rounded-md text-ink-soft hover:bg-paper"
      >
        <Search className="h-4 w-4" />
      </button>
    </header>
  );
}

function Sidebar({
  courseId,
  onOpenPalette,
  onClose,
  className = "",
}: {
  courseId: string | undefined;
  onOpenPalette: () => void;
  onClose?: () => void;
  className?: string;
}) {
  const waiting = useReviewQueue().data?.drafts.length ?? 0;
  const counts = useJobOverview(undefined, undefined).data?.counts;
  const active = counts ? counts.queued + counts.running : 0;

  return (
    <aside className={`w-[248px] shrink-0 flex-col overflow-y-auto border-r border-line bg-white ${className}`}>
      <div className="flex items-center justify-between gap-2 px-4 pb-2 pt-4">
        <Wordmark />
        {onClose && (
          <button onClick={onClose} aria-label="Close navigation" className="rounded-md p-1 text-ink-muted hover:bg-paper hover:text-ink">
            <X className="h-4 w-4" />
          </button>
        )}
      </div>

      <div className="px-2 pb-1">
        <button
          onClick={onOpenPalette}
          className="flex h-8 w-full items-center gap-2 rounded-md border border-line bg-paper/70 px-2.5 text-xs text-ink-muted transition-colors hover:border-ink-faint hover:text-ink"
          title="Jump to a course, assignment or student"
        >
          <Search className="h-3.5 w-3.5 shrink-0" />
          <span className="flex-1 text-left">Jump to…</span>
          <span className="flex items-center gap-1 text-ink-faint">
            <Kbd>Ctrl</Kbd>
            <Kbd>K</Kbd>
          </span>
        </button>
      </div>

      <nav className="flex flex-col gap-0.5 px-2 py-2" aria-label="Workspace">
        <NavLink to="/" end className={({ isActive }) => sideLinkClass(isActive)}>
          <House className="h-4 w-4 shrink-0" />
          <span className="flex-1 truncate">Home</span>
        </NavLink>
        {workspaceSections.map((section) => (
          <NavLink key={section.path} to={`/${section.path}`} className={({ isActive }) => sideLinkClass(isActive, !section.ready)}>
            <section.icon className="h-4 w-4 shrink-0" />
            <span className="flex-1 truncate">{section.label}</span>
            {!section.ready && <SoonBadge />}
            {section.path === "review" && waiting > 0 && (
              <span className="num rounded-full bg-accent-tint px-1.5 text-[11px] leading-[18px] text-accent-strong" title={`${waiting} drafts waiting for your review`}>
                {waiting}
              </span>
            )}
            {section.path === "system" && active > 0 && (
              <span className="num flex items-center gap-1 text-[11px] text-accent" title={`${active} background jobs queued or running`}>
                <span className="live-dot h-1.5 w-1.5 rounded-full bg-accent" />
                {active}
              </span>
            )}
          </NavLink>
        ))}
      </nav>

      <div className="border-t border-line">{courseId ? <CourseNav courseId={courseId} /> : <CourseList />}</div>

      <div className="flex-1" />

      <div className="flex flex-col gap-0.5 border-t border-line px-2 py-2">
        <ConnectionStatus />
        <NavLink to="/settings" className={({ isActive }) => sideLinkClass(isActive)}>
          <Settings className="h-4 w-4 shrink-0" />
          <span className="flex-1 truncate">Settings</span>
        </NavLink>
      </div>
      <div className="mx-2 mb-2 flex items-start gap-2 rounded-lg bg-paper px-3 py-2.5 text-[11px] leading-snug text-ink-muted">
        <Lock className="mt-px h-3.5 w-3.5 shrink-0 text-ink-faint" />
        <span>
          <b className="font-semibold text-ink-soft">Read-only.</b> Grades and drafts stay in this tool; nothing is ever written to Classroom.
        </span>
      </div>
    </aside>
  );
}

function ConnectionStatus() {
  const status = useGoogleStatus();
  const health = useApiHealth();
  const labels = {
    Connected: "Google Classroom connected · read-only",
    NotConnected: "Google Classroom is not connected · click to connect",
    ReconnectRequired: "Google access expired · click to reconnect",
    NotConfigured: "Google Classroom is not configured",
  } as const;

  if (health.isError) {
    return (
      <span className="flex items-center gap-2.5 rounded-md bg-danger-tint px-2.5 py-[7px] text-[13px] font-semibold text-danger-ink" title="The API does not answer; start it and this clears on its own">
        <span className="ml-1 h-2 w-2 rounded-full bg-danger" />
        API offline
      </span>
    );
  }

  const state = status.data?.state;
  const connected = state === "Connected";
  const dot = connected ? "bg-success" : state === undefined ? "bg-ink-faint" : "bg-warn";

  return (
    <Link
      to="/settings"
      title={status.isLoading ? "Checking Google Classroom…" : state ? labels[state] : "Google Classroom status unavailable"}
      className={`flex items-center gap-2.5 rounded-md px-2.5 py-[7px] text-[13px] transition-colors ${
        connected || state === undefined ? "text-ink-soft hover:bg-paper hover:text-ink" : "bg-warn-tint font-semibold text-warn-ink"
      }`}
    >
      <span className={`ml-1 h-2 w-2 shrink-0 rounded-full ${dot}`} />
      <span className="flex-1 truncate">
        {connected ? "Classroom · read-only" : state === undefined ? "Classroom" : state === "ReconnectRequired" ? "Reconnect Classroom" : "Connect Classroom"}
      </span>
    </Link>
  );
}

function sideLinkClass(isActive: boolean, soon?: boolean) {
  return `relative flex items-center gap-2.5 rounded-md px-2.5 py-[7px] text-[13px] transition-colors ${
    isActive
      ? "bg-accent-tint font-semibold text-accent-strong before:absolute before:inset-y-1.5 before:-left-2 before:w-[3px] before:rounded-r-full before:bg-accent"
      : soon
        ? "text-ink-faint hover:bg-paper hover:text-ink-muted"
        : "text-ink-soft hover:bg-paper hover:text-ink"
  }`;
}

function CourseList() {
  const courses = useCourses();

  if (!courses.data || courses.data.length === 0) {
    return null;
  }

  return (
    <div className="px-2 py-2.5">
      <div className="eyebrow px-2.5 pb-1.5">Courses</div>
      <div className="flex flex-col gap-0.5">
        {courses.data.map((course) => (
          <Link key={course.id} to={coursePath(course.id, "")} className="flex items-center gap-2.5 rounded-md px-2 py-1.5 text-[13px] text-ink-soft hover:bg-paper hover:text-ink">
            <CourseTile courseId={course.id} name={course.name} size="sm" />
            <span className="min-w-0 flex-1 truncate">{course.name}</span>
          </Link>
        ))}
      </div>
    </div>
  );
}

function CourseNav({ courseId }: { courseId: string }) {
  const assignments = useAssignments(courseId);
  const currentAssignmentId = useMatch("/courses/:courseId/assignments/:assignmentId/*")?.params.assignmentId;
  const ready = courseSections.filter((section) => section.ready);
  const planned = courseSections.filter((section) => !section.ready);

  return (
    <>
      <CourseSwitcher courseId={courseId} />

      <nav className="flex flex-col gap-0.5 px-2 pb-2" aria-label="Course sections">
        {ready.map((section) => (
          <NavLink
            key={section.path}
            to={coursePath(courseId, section.path)}
            end={section.path === ""}
            className={({ isActive }) => sideLinkClass(isActive)}
          >
            <section.icon className="h-4 w-4 shrink-0" />
            <span className="flex-1 truncate">{section.label}</span>
            {section.path === "assignments" && assignments.data && (
              <span className="num text-[11px] text-ink-faint">{assignments.data.length}</span>
            )}
          </NavLink>
        ))}
      </nav>

      {(assignments.data?.length ?? 0) > 0 && (
        <div className="border-t border-line px-2 py-2.5">
          <div className="eyebrow px-2.5 pb-1.5">Jump to assignment</div>
          <div className="flex flex-col gap-px">
            {assignments.data!.slice(0, 6).map((assignment) => {
              const current = assignment.id === currentAssignmentId;
              return (
                <Link
                  key={assignment.id}
                  to={`${coursePath(courseId, "assignments")}/${assignment.id}`}
                  className={`flex items-center gap-2 rounded-md px-2.5 py-1.5 text-xs transition-colors ${
                    current ? "bg-paper-dark font-semibold text-ink" : "text-ink-muted hover:bg-paper hover:text-ink"
                  }`}
                >
                  <span className={`h-1.5 w-1.5 shrink-0 rounded-full ${current ? "bg-accent" : "bg-line"}`} />
                  <span className="min-w-0 flex-1 truncate">{assignment.title}</span>
                  {assignment.dueAt && <span className="shrink-0 text-[10.5px] text-ink-faint">{formatDay(assignment.dueAt)}</span>}
                </Link>
              );
            })}
          </div>
        </div>
      )}

      {planned.length > 0 && (
        <div className="border-t border-line px-2 py-2.5">
          <div className="eyebrow px-2.5 pb-1.5">Planned</div>
          <nav className="flex flex-col gap-0.5" aria-label="Planned sections">
            {planned.map((section) => (
              <NavLink key={section.path} to={coursePath(courseId, section.path)} className={({ isActive }) => sideLinkClass(isActive, true)}>
                <section.icon className="h-4 w-4 shrink-0" />
                <span className="flex-1 truncate">{section.label}</span>
                <SoonBadge />
              </NavLink>
            ))}
          </nav>
        </div>
      )}
    </>
  );
}

export function CourseTile({ courseId, name, size = "md" }: { courseId: string; name: string; size?: "sm" | "md" | "lg" }) {
  const sizes = { sm: "h-6 w-6 rounded-md text-[10px]", md: "h-9 w-9 rounded-lg text-[13px]", lg: "h-12 w-12 rounded-[10px] text-[17px]" };
  return (
    <span
      className={`flex shrink-0 items-center justify-center font-mono font-bold text-white shadow-[inset_0_-2px_0_rgb(0_0_0/0.18)] ${sizes[size]}`}
      style={{ backgroundColor: courseTone(courseId) }}
      aria-hidden
    >
      {courseCode(name)}
    </span>
  );
}

function CourseSwitcher({ courseId }: { courseId: string }) {
  const courses = useCourses();
  const navigate = useNavigate();
  const location = useLocation();
  const [open, setOpen] = useState(false);
  const root = useRef<HTMLDivElement>(null);
  const course = courses.data?.find((c) => c.id === courseId);
  const section = location.pathname.split("/")[3] ?? "";

  useEffect(() => {
    if (!open) {
      return;
    }
    const onDown = (e: MouseEvent) => {
      if (!root.current?.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    window.addEventListener("mousedown", onDown);
    return () => window.removeEventListener("mousedown", onDown);
  }, [open]);

  function pick(id: string) {
    setOpen(false);
    const keep = courseSections.some((s) => s.path === section) ? section : "";
    navigate(coursePath(id, keep));
  }

  return (
    <div ref={root} className="relative p-2">
      <button
        onClick={() => setOpen(!open)}
        aria-haspopup="listbox"
        aria-expanded={open}
        title="Switch course"
        className="flex w-full items-center gap-2.5 rounded-lg px-2 py-2 text-left transition-colors hover:bg-paper"
      >
        <CourseTile courseId={courseId} name={course?.name ?? "Course"} />
        <span className="min-w-0 flex-1">
          <span className="block truncate text-[13.5px] font-semibold leading-tight">{course?.name ?? "Course"}</span>
          <span className="block truncate text-[11px] text-ink-muted">{course?.section ?? "Google Classroom course"}</span>
        </span>
        <ChevronsUpDown className="h-3.5 w-3.5 shrink-0 text-ink-faint" />
      </button>
      {open && (
        <div role="listbox" className="absolute inset-x-2 top-[calc(100%-2px)] z-30 animate-pop rounded-[10px] border border-line bg-white p-1 shadow-raised">
          <div className="eyebrow px-2.5 py-1.5">Your courses</div>
          {courses.data?.map((c) => (
            <button
              key={c.id}
              role="option"
              aria-selected={c.id === courseId}
              onClick={() => pick(c.id)}
              className="flex w-full items-center gap-2.5 rounded-md px-2 py-1.5 text-left hover:bg-paper"
            >
              <CourseTile courseId={c.id} name={c.name} size="sm" />
              <span className="min-w-0 flex-1 truncate text-[13px] font-medium">{c.name}</span>
              {c.id === courseId && <Check className="h-3.5 w-3.5 shrink-0 text-accent" />}
            </button>
          ))}
          <Link to="/" onClick={() => setOpen(false)} className="mt-1 block border-t border-line px-2.5 py-2 text-xs font-semibold text-accent hover:underline">
            All courses →
          </Link>
        </div>
      )}
    </div>
  );
}

function SoonBadge() {
  return <span className="shrink-0 rounded border border-line px-1 font-mono text-[9px] font-medium uppercase tracking-wider text-ink-faint">soon</span>;
}

function Breadcrumbs() {
  const matches = useMatches();
  const crumbs = matches.flatMap((match) => (match.handle as CrumbHandle | undefined)?.crumbs(match.params) ?? []);

  if (crumbs.length < 2) {
    return null;
  }

  return (
    <nav className="mb-3 flex h-5 min-w-0 items-center gap-1 overflow-hidden text-xs text-ink-muted" aria-label="Breadcrumb">
      {crumbs.map((crumb, i) => (
        <span key={i} className={`min-w-0 items-center gap-1 ${i < crumbs.length - 2 ? "hidden md:flex" : "flex"}`}>
          {i > 0 && <ChevronRight className={`h-3 w-3 shrink-0 text-ink-faint ${i === crumbs.length - 2 ? "hidden md:block" : ""}`} />}
          {crumb.to && i < crumbs.length - 1 ? (
            <Link to={crumb.to} className="truncate rounded px-1 py-0.5 hover:bg-paper-dark hover:text-ink">
              {crumb.label}
            </Link>
          ) : (
            <span className="truncate px-1 font-semibold text-ink">{crumb.label}</span>
          )}
        </span>
      ))}
    </nav>
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
