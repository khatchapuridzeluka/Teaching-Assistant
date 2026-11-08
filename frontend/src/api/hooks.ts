import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, unwrap } from "./client";

// One hook per endpoint. Components only call these; no fetch logic lives in pages.

export const useCourses = () =>
  useQuery({ queryKey: ["courses"], queryFn: () => unwrap(api.GET("/courses")) });

export const useAssignments = (courseId: string) =>
  useQuery({
    queryKey: ["courses", courseId, "assignments"],
    queryFn: () => unwrap(api.GET("/courses/{courseId}/assignments", { params: { path: { courseId } } })),
    enabled: courseId !== "", // pages without a course (Courses, Settings) don't ask
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

// A course's students with their grades per assignment and overall score, from our database (filled by a sync).
export const useCourseStudents = (courseId: string) =>
  useQuery({
    queryKey: ["courses", courseId, "students"],
    queryFn: () => unwrap(api.GET("/courses/{courseId}/students", { params: { path: { courseId } } })),
  });

// Copies the course's roster, assignments and grades from Classroom (no files); the students table then shows them.
export const useSyncCourse = (courseId: string) => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => unwrap(api.POST("/courses/{courseId}/sync", { params: { path: { courseId } } })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["courses", courseId, "students"] });
    },
  });
};

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
    // Refreshes the live list, the stored versions and the stored instructions (fetch stores those first).
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["courses", courseId, "assignments", assignmentId, "submissions"] });
      void queryClient.invalidateQueries({ queryKey: ["courses", courseId, "assignments", assignmentId, "instructions"] });
    },
  });
};

// The newest stored instructions (what grading sends), or null before the first fetch.
export const useInstructions = (courseId: string, assignmentId: string) =>
  useQuery({
    queryKey: ["courses", courseId, "assignments", assignmentId, "instructions"],
    // 204 (no body) means "not fetched yet"; any error, including a 404, is a real failure and is shown as one.
    queryFn: async () =>
      (await unwrap(
        api.GET("/courses/{courseId}/assignments/{assignmentId}/instructions", {
          params: { path: { courseId, assignmentId } },
        }),
      )) ?? null,
  });

// One stored instructions file as a blob, for viewers that render in the page (Word documents).
export const useInstructionsFileBlob = (courseId: string, assignmentId: string, fileId: string) =>
  useQuery({
    queryKey: ["courses", courseId, "assignments", assignmentId, "instructions", "files", fileId],
    queryFn: () =>
      unwrap(
        api.GET("/courses/{courseId}/assignments/{assignmentId}/instructions/files/{contentId}", {
          params: { path: { courseId, assignmentId, contentId: fileId } },
          parseAs: "blob",
        }),
      ),
    staleTime: Infinity, // a stored version's files never change
  });

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
    // An empty file (students do turn those in) has no body, which the client reads as undefined; show it as "".
    queryFn: async () =>
      (await unwrap(
        api.GET("/versions/{versionId}/code/file", {
          params: { path: { versionId }, query: { path: path! } },
          parseAs: "text",
        }),
      )) ?? "",
    enabled: path !== null,
    staleTime: Infinity, // a stored version's code never changes until it is re-extracted
  });

// Sends one stored version to the model; the API saves the answer as a new draft and returns it. Costs tokens and can
// take a minute, so it only runs when the teacher clicks.
export const useGradeVersion = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (versionId: string) =>
      unwrap(api.POST("/versions/{versionId}/grade", { params: { path: { versionId } } })),
    onSuccess: (review, versionId) => {
      queryClient.setQueryData(["reviews", versionId], review);
      void queryClient.invalidateQueries({ predicate: (query) => query.queryKey.includes("gradings") });
    },
  });
};

// The current saved draft of a version (its newest grading that was not discarded), or null when it has none.
export const useReview = (versionId: string | undefined) =>
  useQuery({
    queryKey: ["reviews", versionId],
    // 204 (no body) means "not graded yet".
    queryFn: async () =>
      (await unwrap(api.GET("/versions/{versionId}/review", { params: { path: { versionId: versionId! } } }))) ?? null,
    enabled: versionId !== undefined,
  });

// Every saved grading of a version, drafts and discarded, newest first.
export const useVersionGradings = (versionId: string | undefined) =>
  useQuery({
    queryKey: ["versions", versionId, "gradings"],
    queryFn: () => unwrap(api.GET("/versions/{versionId}/gradings", { params: { path: { versionId: versionId! } } })),
    enabled: versionId !== undefined,
  });

// Every saved grading of an assignment, newest first, with student, score and mistake counts.
export const useAssignmentGradings = (courseId: string, assignmentId: string) =>
  useQuery({
    queryKey: ["courses", courseId, "assignments", assignmentId, "gradings"],
    queryFn: () =>
      unwrap(
        api.GET("/courses/{courseId}/assignments/{assignmentId}/gradings", {
          params: { path: { courseId, assignmentId } },
        }),
      ),
  });

// Throws a draft away; it stays in the history and the version's current draft becomes the previous one, if any.
export const useDiscardGrading = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (gradingId: string) =>
      unwrap(api.POST("/gradings/{gradingId}/discard", { params: { path: { gradingId } } })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["reviews"] });
      void queryClient.invalidateQueries({ predicate: (query) => query.queryKey.includes("gradings") });
    },
  });
};

// Downloads a stored file's bytes for viewers that lay the file out in the browser (Word documents).
export const useStoredFileBlob = (fileId: string) =>
  useQuery({
    queryKey: ["stored-files", fileId],
    queryFn: () =>
      unwrap(api.GET("/stored-files/{contentId}", { params: { path: { contentId: fileId } }, parseAs: "blob" })),
    staleTime: Infinity, // stored originals never change
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
