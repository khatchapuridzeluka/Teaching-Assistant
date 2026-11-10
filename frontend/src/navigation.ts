import {
  Activity,
  ChartColumn,
  ClipboardList,
  FlaskConical,
  Hand,
  Inbox,
  LayoutDashboard,
  type LucideIcon,
  ScrollText,
  Sparkles,
  Table2,
  Users,
} from "lucide-react";

export type Section = {
  path: string;
  label: string;
  icon: LucideIcon;
  ready: boolean;
  description: string;
  plans?: string[];
};

export const courseSections: Section[] = [
  {
    path: "",
    label: "Overview",
    icon: LayoutDashboard,
    ready: true,
    description: "The course at a glance: assignments, deadlines and what needs your attention.",
  },
  {
    path: "assignments",
    label: "Assignments",
    icon: ClipboardList,
    ready: true,
    description: "Published assignments with their instructions; fetch, preview and grade submissions.",
  },
  {
    path: "students",
    label: "Students",
    icon: Users,
    ready: true,
    description: "Every student's grades per assignment, initial and final, and their overall score.",
  },
  {
    path: "drafts",
    label: "AI drafts",
    icon: Sparkles,
    ready: true,
    description: "Every AI-drafted review of the course, by assignment or by student; where you will edit and approve them.",
  },
  {
    path: "gradebook",
    label: "Gradebook",
    icon: Table2,
    ready: true,
    description: "Initial and final grades for every student and assignment, with class averages and a CSV export.",
  },
  {
    path: "mistakes",
    label: "Common mistakes",
    icon: ChartColumn,
    ready: true,
    description: "An assignment's mistakes grouped by meaning across the class, so one explanation covers many students.",
  },
  {
    path: "rubric",
    label: "Rubric & policy",
    icon: ScrollText,
    ready: false,
    description: "The rubric and your grading rules, applied to every grading in this course.",
    plans: [
      "Base rubric shared by all assignments, with per-assignment additions",
      "Your judgement calls as rules, e.g. \"typo in printed text: not a mistake\"",
      "Rules suggested from the corrections you make while reviewing",
    ],
  },
  {
    path: "participation",
    label: "Participation",
    icon: Hand,
    ready: false,
    description: "Lecture participation alongside homework results (later).",
    plans: ["Attendance and in-class activity next to each student's homework progress"],
  },
];

export const workspaceSections: Section[] = [
  {
    path: "review",
    label: "Review queue",
    icon: Inbox,
    ready: true,
    description: "Every AI draft waiting for your decision and every turned-in submission not graded yet, across all courses.",
  },
  {
    path: "evaluation",
    label: "Evaluation",
    icon: FlaskConical,
    ready: false,
    description: "How often the agent agrees with your final grades, measured on past homework.",
    plans: [
      "Agreement rate between AI drafts and your final grades",
      "Where the agent tends to be too strict or too lenient",
      "Tracked over time as the rubric and rules improve",
    ],
  },
  {
    path: "system",
    label: "System",
    icon: Activity,
    ready: true,
    description: "Background jobs with cancel and retry, and every model call with its tokens, cache use and duration.",
  },
];

export const coursePath = (courseId: string, sectionPath = "") =>
  sectionPath ? `/courses/${courseId}/${sectionPath}` : `/courses/${courseId}`;

// "Java II N1" → "J1": the first letter plus the group number when the name ends in one, otherwise two initials.
export function courseCode(name: string): string {
  const words = name.split(/\s+/).filter(Boolean);
  if (words.length === 0) {
    return "?";
  }
  const first = words[0]![0]!.toUpperCase();
  const digits = words[words.length - 1]!.match(/\d+/)?.[0];
  if (words.length > 1 && digits) {
    return `${first}${digits}`.slice(0, 3);
  }
  return words.length > 1 ? `${first}${words[1]![0]!.toUpperCase()}` : name.slice(0, 2).toUpperCase();
}
