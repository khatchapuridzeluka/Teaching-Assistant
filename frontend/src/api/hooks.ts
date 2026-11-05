import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, unwrap } from "./client";

// One hook per endpoint. Components only call these; no fetch logic lives in pages.

export const useCourses = () =>
  useQuery({ queryKey: ["courses"], queryFn: () => unwrap(api.GET("/courses")) });

export const useAssignments = (courseId: string) =>
  useQuery({
    queryKey: ["courses", courseId, "assignments"],
    queryFn: () => unwrap(api.GET("/courses/{courseId}/assignments", { params: { path: { courseId } } })),
  });

export const useSubmissions = (courseId: string, assignmentId: string) =>
  useQuery({
    queryKey: ["courses", courseId, "assignments", assignmentId, "submissions"],
    queryFn: () =>
      unwrap(
        api.GET("/courses/{courseId}/assignments/{assignmentId}/submissions", {
          params: { path: { courseId, assignmentId } },
        }),
      ),
  });

// What fetch has stored (our database), as opposed to the live Classroom list above.
export const useStoredSubmissions = (courseId: string, assignmentId: string) =>
  useQuery({
    queryKey: ["courses", courseId, "assignments", assignmentId, "submissions", "stored"],
    queryFn: () =>
      unwrap(
        api.GET("/courses/{courseId}/assignments/{assignmentId}/submissions/stored", {
          params: { path: { courseId, assignmentId } },
        }),
      ),
  });

export const useFetchSubmissions = (courseId: string, assignmentId: string) => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () =>
      unwrap(
        api.POST("/courses/{courseId}/assignments/{assignmentId}/submissions/fetch", {
          params: { path: { courseId, assignmentId } },
        }),
      ),
    // Refreshes both the live list and the stored versions.
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["courses", courseId, "assignments", assignmentId, "submissions"] }),
  });
};

export const useReExtract = (courseId: string, assignmentId: string) => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () =>
      unwrap(
        api.POST("/courses/{courseId}/assignments/{assignmentId}/submissions/extract", {
          params: { path: { courseId, assignmentId } },
        }),
      ),
    // Stored versions and every version's code list change.
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["courses", courseId, "assignments", assignmentId, "submissions"] });
      queryClient.invalidateQueries({ queryKey: ["versions"] });
    },
  });
};

export const useCodeFiles = (versionId: string, enabled: boolean) =>
  useQuery({
    queryKey: ["versions", versionId, "code"],
    queryFn: () => unwrap(api.GET("/versions/{versionId}/code", { params: { path: { versionId } } })),
    enabled,
  });

// Reads one extracted file as text for the code viewer.
export const useCodeFileText = (versionId: string, path: string | null) =>
  useQuery({
    queryKey: ["versions", versionId, "code", path],
    queryFn: () =>
      unwrap(
        api.GET("/versions/{versionId}/code/file", {
          params: { path: { versionId }, query: { path: path! } },
          parseAs: "text",
        }),
      ),
    enabled: path !== null,
    staleTime: Infinity, // a stored version's code never changes until it is re-extracted
  });

export const useGoogleStatus = () =>
  useQuery({ queryKey: ["google-status"], queryFn: () => unwrap(api.GET("/auth/google/status")) });

export const useConnectGoogle = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => unwrap(api.POST("/auth/google/connect")),
    onSettled: () => queryClient.invalidateQueries(),
  });
};

export const useDisconnectGoogle = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => unwrap(api.POST("/auth/google/disconnect")),
    onSettled: () => queryClient.invalidateQueries(),
  });
};

export const useApiHealth = () =>
  useQuery({
    queryKey: ["health"],
    queryFn: () => unwrap(api.GET("/health", { parseAs: "text" })),
    refetchInterval: 30_000,
    retry: false,
  });

// Sends a real prompt (costs tokens), so it only runs when the teacher clicks.
export const useModelHealth = () =>
  useMutation({ mutationFn: () => unwrap(api.GET("/health/model")) });
