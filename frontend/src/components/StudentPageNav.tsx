import { Link } from "react-router";
import { ArrowLeft, Award, CircleAlert, LayoutDashboard } from "lucide-react";
import { buttonClass } from "./ui";

export type StudentPage = "overview" | "mistakes" | "strengths";

const pages: { page: StudentPage; label: string; icon: typeof Award }[] = [
  { page: "overview", label: "Overview", icon: LayoutDashboard },
  { page: "mistakes", label: "Mistakes", icon: CircleAlert },
  { page: "strengths", label: "Strengths", icon: Award },
];

/** The same three buttons on a student's overview, mistakes and strengths pages; the open page's button is colored. */
export function StudentPageNav({
  courseId,
  userId,
  studentId,
  active,
}: {
  courseId: string;
  userId: string;
  studentId?: string;
  active: StudentPage;
}) {
  return (
    <span className="flex flex-wrap gap-2">
      {pages.map(({ page, label, icon: Icon }) => (
        <Link
          key={page}
          to={`/courses/${courseId}/students/${userId}/${page}`}
          className={buttonClass(page === active ? "primary" : "outline")}
          aria-current={page === active ? "page" : undefined}
        >
          <Icon className="h-4 w-4" /> {label}
        </Link>
      ))}
      <Link to={`/courses/${courseId}/students${studentId ? `?student=${studentId}` : ""}`} className={buttonClass("ghost")}>
        <ArrowLeft className="h-4 w-4" /> Profile
      </Link>
    </span>
  );
}
