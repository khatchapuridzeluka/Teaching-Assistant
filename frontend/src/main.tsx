import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createBrowserRouter, RouterProvider } from "react-router";
import "./index.css";
import "./monaco";
import { AssignmentTitle, CourseName, type CrumbHandle, Layout } from "./components/Layout";
import { CoursesPage } from "./pages/CoursesPage";
import { CoursePage } from "./pages/CoursePage";
import { AssignmentPage } from "./pages/AssignmentPage";
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
        path: "/courses/:courseId",
        handle: {
          crumbs: ({ courseId }) => [{ label: <CourseName courseId={courseId!} />, to: `/courses/${courseId}` }],
        } satisfies CrumbHandle,
        children: [
          { index: true, element: <CoursePage /> },
          {
            path: "assignments/:assignmentId",
            element: <AssignmentPage />,
            handle: {
              crumbs: ({ courseId, assignmentId }) => [
                { label: <AssignmentTitle courseId={courseId!} assignmentId={assignmentId!} /> },
              ],
            } satisfies CrumbHandle,
          },
        ],
      },
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
