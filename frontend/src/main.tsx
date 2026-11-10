import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createBrowserRouter, RouterProvider } from "react-router";
import "./index.css";
import "./monaco";
import { AssignmentTitle, CourseName, type CrumbHandle, Layout } from "./components/Layout";
import { courseSections, workspaceSections } from "./navigation";
import { AssignmentPage } from "./pages/AssignmentPage";
import { AssignmentsPage } from "./pages/AssignmentsPage";
import { ComingSoonPage } from "./pages/ComingSoonPage";
import { CourseOverviewPage } from "./pages/CourseOverviewPage";
import { CommonMistakesPage } from "./pages/CommonMistakesPage";
import { CoursesPage } from "./pages/CoursesPage";
import { DraftsPage } from "./pages/DraftsPage";
import { GradebookPage } from "./pages/GradebookPage";
import { InstructionsPage } from "./pages/InstructionsPage";
import { ReviewQueuePage } from "./pages/ReviewQueuePage";
import { SystemPage } from "./pages/SystemPage";
import { SettingsPage } from "./pages/SettingsPage";
import { StudentMistakesPage } from "./pages/StudentMistakesPage";
import { StudentStrengthsPage } from "./pages/StudentStrengthsPage";
import { StudentsPage } from "./pages/StudentsPage";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 60_000, refetchOnWindowFocus: false, retry: 1 },
  },
});

const router = createBrowserRouter([
  {
    element: <Layout />,
    handle: { crumbs: () => [{ label: "Courses", to: "/" }] } satisfies CrumbHandle,
    children: [
      { path: "/", element: <CoursesPage /> },
      {
        path: "/courses/:courseId",
        handle: {
          crumbs: ({ courseId }) => [{ label: <CourseName courseId={courseId!} />, to: `/courses/${courseId}` }],
        } satisfies CrumbHandle,
        children: [
          { index: true, element: <CourseOverviewPage /> },
          {
            path: "assignments",
            handle: {
              crumbs: ({ courseId }) => [{ label: "Assignments", to: `/courses/${courseId}/assignments` }],
            } satisfies CrumbHandle,
            children: [
              { index: true, element: <AssignmentsPage /> },
              {
                path: ":assignmentId",
                handle: {
                  crumbs: ({ courseId, assignmentId }) => [
                    {
                      label: <AssignmentTitle courseId={courseId!} assignmentId={assignmentId!} />,
                      to: `/courses/${courseId}/assignments/${assignmentId}`,
                    },
                  ],
                } satisfies CrumbHandle,
                children: [
                  { index: true, element: <AssignmentPage /> },
                  {
                    path: "instructions",
                    element: <InstructionsPage />,
                    handle: { crumbs: () => [{ label: "Instructions" }] } satisfies CrumbHandle,
                  },
                ],
              },
            ],
          },
          {
            path: "students",
            element: <StudentsPage />,
            handle: { crumbs: () => [{ label: "Students" }] } satisfies CrumbHandle,
          },
          {
            path: "students/:userId/mistakes",
            element: <StudentMistakesPage />,
            handle: {
              crumbs: ({ courseId }) => [{ label: "Students", to: `/courses/${courseId}/students` }, { label: "Mistakes" }],
            } satisfies CrumbHandle,
          },
          {
            path: "students/:userId/strengths",
            element: <StudentStrengthsPage />,
            handle: {
              crumbs: ({ courseId }) => [{ label: "Students", to: `/courses/${courseId}/students` }, { label: "Strengths" }],
            } satisfies CrumbHandle,
          },
          {
            path: "drafts",
            element: <DraftsPage />,
            handle: { crumbs: () => [{ label: "AI drafts" }] } satisfies CrumbHandle,
          },
          {
            path: "mistakes",
            element: <CommonMistakesPage />,
            handle: { crumbs: () => [{ label: "Common mistakes" }] } satisfies CrumbHandle,
          },
          {
            path: "gradebook",
            element: <GradebookPage />,
            handle: { crumbs: () => [{ label: "Gradebook" }] } satisfies CrumbHandle,
          },
          ...courseSections
            .filter((section) => !section.ready)
            .map((section) => ({
              path: section.path,
              element: <ComingSoonPage section={section} />,
              handle: { crumbs: () => [{ label: section.label }] } satisfies CrumbHandle,
            })),
        ],
      },
      {
        path: "/system",
        element: <SystemPage />,
        handle: { crumbs: () => [{ label: "System" }] } satisfies CrumbHandle,
      },
      {
        path: "/review",
        element: <ReviewQueuePage />,
        handle: { crumbs: () => [{ label: "Review queue" }] } satisfies CrumbHandle,
      },
      ...workspaceSections
        .filter((section) => !section.ready)
        .map((section) => ({
          path: `/${section.path}`,
          element: <ComingSoonPage section={section} />,
          handle: { crumbs: () => [{ label: section.label }] } satisfies CrumbHandle,
        })),
      {
        path: "/settings",
        element: <SettingsPage />,
        handle: { crumbs: () => [{ label: "Settings" }] } satisfies CrumbHandle,
      },
    ],
  },
]);

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>,
);
