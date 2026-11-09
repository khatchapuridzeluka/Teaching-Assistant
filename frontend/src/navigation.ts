export type Section = {
  path: string;
  label: string;
  ready: boolean;
  description: string;
  plans?: string[];
};

export const courseSections: Section[] = [
  {
    path: "",
    label: "Overview",
    ready: true,
    description: "The course at a glance: assignments, deadlines and what needs your attention.",
  },
  {
    path: "assignments",
    label: "Assignments",
    ready: true,
    description: "Published assignments with their instructions; fetch, preview and grade submissions.",
  },
  {
    path: "students",
    label: "Students",
    ready: true,
    description: "Every student's grades per assignment, initial and final, and their overall score.",
  },
  {
    path: "drafts",
    label: "AI drafts",
    ready: true,
    description: "Every AI-drafted review of the course, by assignment or by student; where you will edit and approve them.",
  },
  {
    path: "gradebook",
    label: "Gradebook",
    ready: true,
    description: "Initial and final grades for every student and assignment, with class averages and a CSV export.",
  },
  {
    path: "mistakes",
    label: "Common mistakes",
    ready: false,
    description: "Mistakes grouped across the class, so one explanation covers many students.",
    plans: [
      "Clusters of the same mistake across all submissions of an assignment",
      "Approve or edit one explanation per cluster instead of per student",
      "Topics the class struggles with, to revisit in the next lecture",
    ],
  },
  {
    path: "rubric",
    label: "Rubric & policy",
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
    ready: false,
    description: "Lecture participation alongside homework results (later).",
    plans: ["Attendance and in-class activity next to each student's homework progress"],
  },
];

export const workspaceSections: Section[] = [
  {
    path: "review",
    label: "Review queue",
    ready: true,
    description: "Every AI draft waiting for your decision and every turned-in submission not graded yet, across all courses.",
  },
  {
    path: "evaluation",
    label: "Evaluation",
    ready: false,
    description: "How often the agent agrees with your final grades, measured on past homework.",
    plans: [
      "Agreement rate between AI drafts and your final grades",
      "Where the agent tends to be too strict or too lenient",
      "Tracked over time as the rubric and rules improve",
    ],
  },
];

export const coursePath = (courseId: string, sectionPath = "") =>
  sectionPath ? `/courses/${courseId}/${sectionPath}` : `/courses/${courseId}`;
