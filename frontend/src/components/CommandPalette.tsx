import { type ReactNode, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router";
import { ClipboardList, CornerDownLeft, GraduationCap, House, Search, Settings } from "lucide-react";
import { useAssignments, useCourses, useCourseStudents } from "../api/hooks";
import { initials } from "../format";
import { coursePath, courseSections, workspaceSections } from "../navigation";
import { Avatar, Kbd } from "./ui";

type Entry = { id: string; group: string; label: string; hint?: string; to: string; icon: ReactNode };

// Ctrl+K: jump to a page, course, assignment or student. It only navigates; everything it lists is already loaded.
export function CommandPalette({ courseId, onClose }: { courseId: string | undefined; onClose: () => void }) {
  return courseId ? <CoursePalette courseId={courseId} onClose={onClose} /> : <Palette courseId={undefined} students={[]} onClose={onClose} />;
}

function CoursePalette({ courseId, onClose }: { courseId: string; onClose: () => void }) {
  const students = useCourseStudents(courseId);
  const entries: Entry[] = (students.data?.students ?? []).map((student) => ({
    id: `student-${student.id}`,
    group: "Students",
    label: student.name,
    hint: student.active ? undefined : "left the course",
    to: `${coursePath(courseId, "students")}?student=${student.id}`,
    icon: <Avatar text={initials(student.name)} userId={student.externalUserId} size="xs" />,
  }));

  return <Palette courseId={courseId} students={entries} onClose={onClose} />;
}

function Palette({ courseId, students, onClose }: { courseId: string | undefined; students: Entry[]; onClose: () => void }) {
  const navigate = useNavigate();
  const courses = useCourses();
  const assignments = useAssignments(courseId ?? "");
  const [query, setQuery] = useState("");
  const [index, setIndex] = useState(0);
  const list = useRef<HTMLDivElement>(null);
  const course = courses.data?.find((c) => c.id === courseId);

  const entries = useMemo(() => {
    const all: Entry[] = [
      { id: "home", group: "Go to", label: "Home", to: "/", icon: <House /> },
      ...workspaceSections
        .filter((s) => s.ready)
        .map((s) => ({ id: `ws-${s.path}`, group: "Go to", label: s.label, to: `/${s.path}`, icon: <s.icon /> })),
      { id: "settings", group: "Go to", label: "Settings", hint: "Google Classroom, grading model", to: "/settings", icon: <Settings /> },
    ];

    if (courseId) {
      for (const section of courseSections.filter((s) => s.ready)) {
        all.push({
          id: `section-${section.path}`,
          group: course?.name ?? "This course",
          label: section.label,
          to: coursePath(courseId, section.path),
          icon: <section.icon />,
        });
      }
      for (const assignment of assignments.data ?? []) {
        all.push({
          id: `assignment-${assignment.id}`,
          group: "Assignments",
          label: assignment.title,
          hint: course?.name,
          to: `${coursePath(courseId, "assignments")}/${assignment.id}`,
          icon: <ClipboardList />,
        });
      }
    }

    for (const c of courses.data ?? []) {
      all.push({ id: `course-${c.id}`, group: "Courses", label: c.name, hint: c.section ?? undefined, to: coursePath(c.id), icon: <GraduationCap /> });
    }

    return all;
  }, [courseId, course?.name, courses.data, assignments.data]);

  const search = query.trim().toLowerCase();
  const shown = [...entries, ...students].filter((e) => !search || `${e.label} ${e.group} ${e.hint ?? ""}`.toLowerCase().includes(search)).slice(0, 40);
  const active = Math.min(index, Math.max(shown.length - 1, 0));

  useEffect(() => {
    list.current?.querySelector(`[data-index="${active}"]`)?.scrollIntoView({ block: "nearest" });
  }, [active]);

  function go(entry: Entry | undefined) {
    if (entry) {
      onClose();
      navigate(entry.to);
    }
  }

  return (
    <div className="modal-overlay fixed inset-0 z-[70] flex items-start justify-center px-3 pt-[12vh]" onClick={(e) => e.target === e.currentTarget && onClose()}>
      <div role="dialog" aria-modal="true" aria-label="Jump to" className="modal-panel flex max-h-[70vh] w-full max-w-[560px] flex-col overflow-hidden rounded-xl border border-line bg-white">
        <div className="flex items-center gap-2.5 border-b border-line px-4">
          <Search className="h-4 w-4 shrink-0 text-ink-faint" />
          <input
            autoFocus
            value={query}
            onChange={(e) => {
              setQuery(e.target.value);
              setIndex(0);
            }}
            onKeyDown={(e) => {
              if (e.key === "ArrowDown") {
                e.preventDefault();
                setIndex(Math.min(active + 1, shown.length - 1));
              } else if (e.key === "ArrowUp") {
                e.preventDefault();
                setIndex(Math.max(active - 1, 0));
              } else if (e.key === "Enter") {
                e.preventDefault();
                go(shown[active]);
              } else if (e.key === "Escape") {
                onClose();
              }
            }}
            placeholder={courseId ? "Jump to a page, assignment or student…" : "Jump to a page or course…"}
            aria-label="Jump to"
            className="h-12 min-w-0 flex-1 bg-transparent text-[14px] outline-none placeholder:text-ink-faint"
          />
          <Kbd>Esc</Kbd>
        </div>

        <div ref={list} className="min-h-0 flex-1 overflow-y-auto p-1.5">
          {shown.length === 0 && <div className="px-3 py-8 text-center text-ink-muted">Nothing matches “{query}”.</div>}
          {shown.map((entry, i) => (
            <div key={entry.id}>
              {(i === 0 || shown[i - 1]!.group !== entry.group) && <div className="eyebrow px-2.5 pb-1 pt-2.5">{entry.group}</div>}
              <button
                data-index={i}
                onClick={() => go(entry)}
                onMouseMove={() => setIndex(i)}
                className={`flex w-full items-center gap-2.5 rounded-md px-2.5 py-2 text-left ${i === active ? "bg-accent-tint text-accent-strong" : ""}`}
              >
                <span className={`flex shrink-0 items-center [&>svg]:h-4 [&>svg]:w-4 ${i === active ? "text-accent" : "text-ink-muted"}`}>{entry.icon}</span>
                <span className="min-w-0 flex-1 truncate font-medium">{entry.label}</span>
                {entry.hint && <span className="shrink-0 truncate text-[11px] text-ink-muted">{entry.hint}</span>}
                {i === active && <CornerDownLeft className="h-3.5 w-3.5 shrink-0" />}
              </button>
            </div>
          ))}
        </div>

        <div className="flex items-center gap-3 border-t border-line bg-paper px-4 py-2 text-[11px] text-ink-muted">
          <span className="flex items-center gap-1">
            <Kbd>↑</Kbd>
            <Kbd>↓</Kbd> move
          </span>
          <span className="flex items-center gap-1">
            <Kbd>Enter</Kbd> open
          </span>
          <span className="ml-auto">{courseId ? "Students and assignments of the open course" : "Open a course to search its students"}</span>
        </div>
      </div>
    </div>
  );
}
