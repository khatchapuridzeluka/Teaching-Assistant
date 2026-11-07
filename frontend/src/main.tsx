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
import { CoursesPage } from "./pages/CoursesPage";
import { InstructionsPage } from "./pages/InstructionsPage";
import { SettingsPage } from "./pages/SettingsPage";

const queryClient = new QueryClient({
  defaultOptions: {
    // Classroom data changes slowly; avoid refetching on every window focus.
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
        // A course opens on its overview; each section is a page below it (see navigation.ts).
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
          // Planned sections: a page that says what they will do.
          ...courseSections
            .filter((section) => !section.ready)
            .map((section) => ({
              path: section.path,
              element: <ComingSoonPage section={section} />,
              handle: { crumbs: () => [{ label: section.label }] } satisfies CrumbHandle,
            })),
        ],
      },
      ...workspaceSections.map((section) => ({
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
