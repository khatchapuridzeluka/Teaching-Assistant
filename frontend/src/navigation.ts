// The app's sections in one place: the sidebar, the course overview and the routes all read these lists, so adding a
// section means adding one entry here. "ready: false" sections open a "planned" page that says what they will do.

export type Section = {
  /** Path below /courses/{courseId} (course sections) or from the root (workspace sections); "" is the course overview. */
  path: string;
  label: string;
  ready: boolean;
  /** One sentence for the overview card and the planned page: what the teacher gets from it. */
  description: string;
  /** For planned sections: what it will show, a few short points. */
  plans?: string[];
};

/** What every course has. */
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
    ready: false,
    description: "Each student's work across assignments and how they grow over time.",
    plans: [
      "Score trend per student, initial and final scores side by side",
      "Mistakes labelled new, repeated or fixed since earlier assignments",
      "A short written overview of each student, drafted by the agent for you to edit",
    ],
  },
  {
    path: "gradebook",
    label: "Gradebook",
    ready: false,
    description: "Initial and final scores for every student and assignment, ready to export.",
    plans: [
      "One row per student, one column per assignment",
      "Which version counts as final, always chosen by you",
      "Export to your own file; nothing is ever written back to Classroom",
    ],
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

/** Across all courses, shown under Courses in the sidebar. */
export const workspaceSections: Section[] = [
  {
    path: "review",
    label: "Review queue",
    ready: false,
    description: "Every AI draft waiting for your approval, across all courses, in one list.",
    plans: [
      "Drafted grades waiting for review, newest first",
      "New versions submitted after a grade was approved",
      "Approve, edit or regrade without opening each assignment",
    ],
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

/** Link to a course section; the overview is the course's own URL. */
export const coursePath = (courseId: string, sectionPath = "") =>
  sectionPath ? `/courses/${courseId}/${sectionPath}` : `/courses/${courseId}`;
