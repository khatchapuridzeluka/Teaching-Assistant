import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type JobStatus, type PolicyRuleRequest, type JobType, type ModelCallPurpose, type SaveReportRequest, type StudentMistakeFilter,
  type StudentStrengthFilter, type StudentAnalysisKind, unwrap } from "./client";
import { forgetMissingPhotos } from "../components/ui";

export const useCourses = () =>
  useQuery({ queryKey: ["courses"], queryFn: () => unwrap(api.GET("/courses")) });

export const useAssignments = (courseId: string) =>
  useQuery({
    queryKey: ["courses", courseId, "assignments"],
    queryFn: () => unwrap(api.GET("/courses/{courseId}/assignments", { params: { path: { courseId } } })),
    enabled: courseId !== "",
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

export const useCourseStudents = (courseId: string) =>
  useQuery({
    queryKey: ["courses", courseId, "students"],
    queryFn: () => unwrap(api.GET("/courses/{courseId}/students", { params: { path: { courseId } } })),
  });

export const useGradebook = (courseId: string) =>
  useQuery({
    queryKey: ["courses", courseId, "gradebook"],
    queryFn: () => unwrap(api.GET("/courses/{courseId}/gradebook", { params: { path: { courseId } } })),
  });

export const useStudentMistakes = (courseId: string, userId: string, filter: StudentMistakeFilter = {}) =>
  useQuery({
    queryKey: ["courses", courseId, "students", userId, "mistakes", "gradings", filter],
    queryFn: () =>
      unwrap(
        api.GET("/courses/{courseId}/students/{userId}/mistakes", {
          params: { path: { courseId, userId }, query: filter },
        }),
      ),
    placeholderData: (previous) => previous,
  });

export const useStudentStrengths = (courseId: string, userId: string, filter: StudentStrengthFilter = {}) =>
  useQuery({
    queryKey: ["courses", courseId, "students", userId, "strengths", "gradings", filter],
    queryFn: () =>
      unwrap(
        api.GET("/courses/{courseId}/students/{userId}/strengths", {
          params: { path: { courseId, userId }, query: filter },
        }),
      ),
    placeholderData: (previous) => previous,
  });

export const useGradingGuidance = (courseId: string) =>
  useQuery({
    queryKey: ["courses", courseId, "grading-guidance"],
    queryFn: () => unwrap(api.GET("/courses/{courseId}/grading-guidance", { params: { path: { courseId } } })),
  });

const isGradingGuidance = (query: { queryKey: readonly unknown[] }) => query.queryKey.includes("grading-guidance");

export const useSetCourseGuidance = (courseId: string) => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (text: string) =>
      unwrap(api.PUT("/courses/{courseId}/grading-guidance/course-guidance", { params: { path: { courseId } }, body: { text } })),
    onSuccess: (guidance) => {
      queryClient.setQueryData(["courses", courseId, "grading-guidance"], guidance);
    },
  });
};

const isAssignmentNotes = (query: { queryKey: readonly unknown[] }) =>
  query.queryKey.includes("notes") || query.queryKey.includes("grading-guidance");

export const useAssignmentNotes = (courseId: string, assignmentId: string) =>
  useQuery({
    queryKey: ["courses", courseId, "assignments", assignmentId, "notes"],
    queryFn: () =>
      unwrap(api.GET("/courses/{courseId}/assignments/{assignmentId}/notes", { params: { path: { courseId, assignmentId } } })),
  });

export const useAddNote = (courseId: string, assignmentId: string) => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { title: string; detail: string }) =>
      unwrap(api.POST("/courses/{courseId}/assignments/{assignmentId}/notes", { params: { path: { courseId, assignmentId } }, body })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ predicate: isAssignmentNotes });
    },
  });
};

export const useUpdateNote = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, title, detail }: { id: string; title: string; detail: string }) =>
      unwrap(api.PUT("/assignment-notes/{id}", { params: { path: { id } }, body: { title, detail } })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ predicate: isAssignmentNotes });
    },
  });
};

export const useDeleteNote = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => unwrap(api.DELETE("/assignment-notes/{id}", { params: { path: { id } } })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ predicate: isAssignmentNotes });
    },
  });
};

export const useAddRule = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: PolicyRuleRequest) => unwrap(api.POST("/policy-rules", { body })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ predicate: isGradingGuidance });
    },
  });
};

export const useUpdateRule = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...body }: PolicyRuleRequest & { id: string }) =>
      unwrap(api.PUT("/policy-rules/{id}", { params: { path: { id } }, body })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ predicate: isGradingGuidance });
    },
  });
};

export const useDeleteRule = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => unwrap(api.DELETE("/policy-rules/{id}", { params: { path: { id } } })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ predicate: isGradingGuidance });
    },
  });
};

export const useAddPrinciple = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ title, detail }: { title: string; detail: string }) =>
      unwrap(api.POST("/grading-principles", { body: { title, detail } })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ predicate: isGradingGuidance });
    },
  });
};

export const useUpdatePrinciple = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, title, detail }: { id: string; title: string; detail: string }) =>
      unwrap(api.PUT("/grading-principles/{id}", { params: { path: { id } }, body: { title, detail } })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ predicate: isGradingGuidance });
    },
  });
};

export const useDeletePrinciple = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => unwrap(api.DELETE("/grading-principles/{id}", { params: { path: { id } } })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ predicate: isGradingGuidance });
    },
  });
};

export const useStudentOverview = (courseId: string, userId: string) =>
  useQuery({
    queryKey: ["courses", courseId, "students", userId, "overview", "gradings"],
    queryFn: () => unwrap(api.GET("/courses/{courseId}/students/{userId}/overview", { params: { path: { courseId, userId } } })),
  });

export const useStudentAnalysis = (courseId: string, userId: string, kind: StudentAnalysisKind) =>
  useQuery({
    queryKey: ["courses", courseId, "students", userId, "analysis", kind, "gradings"],
    queryFn: async () =>
      (await unwrap(
        api.GET("/courses/{courseId}/students/{userId}/analysis", { params: { path: { courseId, userId }, query: { kind } } }),
      )) ?? null,
  });

export const useAnalyzeStudent = (courseId: string, userId: string, kind: StudentAnalysisKind) =>
  useMutation({
    mutationFn: () =>
      unwrap(api.POST("/courses/{courseId}/students/{userId}/analysis", { params: { path: { courseId, userId }, query: { kind } } })),
  });

export const useSyncCourse = (courseId: string) => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => unwrap(api.POST("/courses/{courseId}/sync", { params: { path: { courseId } } })),
    onSuccess: () => {
      forgetMissingPhotos();
      void queryClient.invalidateQueries({ queryKey: ["courses", courseId, "students"] });
      void queryClient.invalidateQueries({ queryKey: ["courses", courseId, "gradebook"] });
    },
  });
};

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
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["courses", courseId, "assignments", assignmentId, "submissions"] });
      void queryClient.invalidateQueries({ queryKey: ["courses", courseId, "assignments", assignmentId, "instructions"] });
    },
  });
};

export const useInstructions = (courseId: string, assignmentId: string) =>
  useQuery({
    queryKey: ["courses", courseId, "assignments", assignmentId, "instructions"],
    queryFn: async () =>
      (await unwrap(
        api.GET("/courses/{courseId}/assignments/{assignmentId}/instructions", {
          params: { path: { courseId, assignmentId } },
        }),
      )) ?? null,
  });

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
    staleTime: Infinity,
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

export const useCodeFileText = (versionId: string, path: string | null) =>
  useQuery({
    queryKey: ["versions", versionId, "code", path],
    queryFn: async () =>
      (await unwrap(
        api.GET("/versions/{versionId}/code/file", {
          params: { path: { versionId }, query: { path: path! } },
          parseAs: "text",
        }),
      )) ?? "",
    enabled: path !== null,
    staleTime: Infinity,
  });

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

export const useQueueGrading = (courseId: string, assignmentId: string) => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (versionId: string) =>
      unwrap(api.POST("/versions/{versionId}/grade/queue", { params: { path: { versionId } } })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["courses", courseId, "assignments", assignmentId, "jobs"] });
    },
  });
};

const isActive = (status: string) => status === "Queued" || status === "Running";

export const useAssignmentJobs = (courseId: string, assignmentId: string) =>
  useQuery({
    queryKey: ["courses", courseId, "assignments", assignmentId, "jobs"],
    queryFn: () =>
      unwrap(
        api.GET("/courses/{courseId}/assignments/{assignmentId}/jobs", { params: { path: { courseId, assignmentId } } }),
      ),
    enabled: assignmentId !== "",
    refetchInterval: (query) => (query.state.data?.some((job) => isActive(job.status)) ? 2000 : false),
  });

export const useJob = (jobId: string) =>
  useQuery({
    queryKey: ["jobs", jobId],
    queryFn: () => unwrap(api.GET("/jobs/{jobId}", { params: { path: { jobId } } })),
    staleTime: 0,
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status === undefined || isActive(status) ? 2000 : false;
    },
  });

export const useJobOverview = (status: JobStatus | undefined, type: JobType | undefined) =>
  useQuery({
    queryKey: ["jobs", "overview", status ?? "all", type ?? "all"],
    queryFn: () => unwrap(api.GET("/jobs", { params: { query: { status, type } } })),
    staleTime: 0,
    refetchInterval: (query) => {
      const counts = query.state.data?.counts;
      return counts && counts.queued + counts.running > 0 ? 2000 : 15000;
    },
  });

export const useModelUsage = (days: number, purpose: ModelCallPurpose | undefined) =>
  useQuery({
    queryKey: ["model-usage", days, purpose ?? "all"],
    queryFn: () => unwrap(api.GET("/model-usage", { params: { query: { days, purpose } } })),
    staleTime: 0,
    refetchInterval: 30000,
  });

export const useGradeAll = (courseId: string, assignmentId: string) => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (regrade: boolean) =>
      unwrap(
        api.POST("/courses/{courseId}/assignments/{assignmentId}/grade-all", {
          params: { path: { courseId, assignmentId }, query: { regrade } },
        }),
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["courses", courseId, "assignments", assignmentId, "jobs"] });
      void queryClient.invalidateQueries({ queryKey: ["jobs"] });
    },
  });
};

export const useCommonMistakes = (courseId: string, assignmentId: string) =>
  useQuery({
    queryKey: ["courses", courseId, "assignments", assignmentId, "common-mistakes", "gradings"],
    queryFn: async () =>
      (await unwrap(
        api.GET("/courses/{courseId}/assignments/{assignmentId}/common-mistakes", { params: { path: { courseId, assignmentId } } }),
      )) ?? null,
    enabled: assignmentId !== "",
  });

export const useAnalyzeMistakes = (courseId: string, assignmentId: string) => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () =>
      unwrap(
        api.POST("/courses/{courseId}/assignments/{assignmentId}/common-mistakes/analyze", {
          params: { path: { courseId, assignmentId } },
        }),
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["courses", courseId, "assignments", assignmentId, "jobs"] });
    },
  });
};

export const useApproveExplanation = (courseId: string, assignmentId: string) => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ clusterId, explanation }: { clusterId: string; explanation: string }) =>
      unwrap(api.POST("/mistake-clusters/{clusterId}/approve", { params: { path: { clusterId } }, body: { explanation } })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["courses", courseId, "assignments", assignmentId, "common-mistakes"] });
    },
  });
};

export const useGradeStudent = (courseId: string, userId: string) => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (regrade: boolean) =>
      unwrap(api.POST("/courses/{courseId}/students/{userId}/grade-all", { params: { path: { courseId, userId }, query: { regrade } } })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["jobs"] });
      void queryClient.invalidateQueries({ predicate: (query) => query.queryKey.includes("jobs") });
    },
  });
};

export const useJobChildren = (jobId: string, enabled: boolean) =>
  useQuery({
    queryKey: ["jobs", "children", jobId],
    queryFn: () => unwrap(api.GET("/jobs/{jobId}/children", { params: { path: { jobId } } })),
    enabled,
    staleTime: 0,
    refetchInterval: (query) =>
      query.state.data?.some((item) => item.job.status === "Queued" || item.job.status === "Running") ? 2000 : false,
  });

export const useJobAction = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ jobId, action }: { jobId: string; action: "cancel" | "retry" }) =>
      action === "cancel"
        ? unwrap(api.POST("/jobs/{jobId}/cancel", { params: { path: { jobId } } }))
        : unwrap(api.POST("/jobs/{jobId}/retry", { params: { path: { jobId } } })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["jobs"] });
      void queryClient.invalidateQueries({ predicate: (query) => query.queryKey.includes("jobs") });
    },
  });
};

export const useCancelJob = (courseId: string, assignmentId: string) => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (jobId: string) => unwrap(api.POST("/jobs/{jobId}/cancel", { params: { path: { jobId } } })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["courses", courseId, "assignments", assignmentId, "jobs"] });
    },
  });
};

export const useReview = (versionId: string | undefined) =>
  useQuery({
    queryKey: ["reviews", versionId],
    queryFn: async () =>
      (await unwrap(api.GET("/versions/{versionId}/review", { params: { path: { versionId: versionId! } } }))) ?? null,
    enabled: versionId !== undefined,
  });

export const useGradingReport = (gradingId: string | undefined) =>
  useQuery({
    queryKey: ["reports", gradingId],
    queryFn: async () =>
      (await unwrap(api.GET("/gradings/{gradingId}/report", { params: { path: { gradingId: gradingId! } } }))) ?? null,
    enabled: gradingId !== undefined,
  });

export const useSaveReport = (gradingId: string) => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ approve, body }: { approve: boolean; body: SaveReportRequest }) =>
      approve
        ? unwrap(api.POST("/gradings/{gradingId}/report/approve", { params: { path: { gradingId } }, body }))
        : unwrap(api.PUT("/gradings/{gradingId}/report", { params: { path: { gradingId } }, body })),
    onSuccess: (report) => {
      queryClient.setQueryData(["reports", gradingId], report);
      void queryClient.invalidateQueries({ predicate: (query) => query.queryKey.includes("gradings") });
    },
  });
};

export const useVersionGradings = (versionId: string | undefined) =>
  useQuery({
    queryKey: ["versions", versionId, "gradings"],
    queryFn: () => unwrap(api.GET("/versions/{versionId}/gradings", { params: { path: { versionId: versionId! } } })),
    enabled: versionId !== undefined,
  });

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

export const useCourseGradings = (
  courseId: string,
  filter: { assignmentId?: string; studentId?: string; all?: boolean } = {},
) =>
  useQuery({
    queryKey: ["courses", courseId, "gradings", filter],
    queryFn: () =>
      unwrap(
        api.GET("/courses/{courseId}/gradings", {
          params: {
            path: { courseId },
            query: { assignmentId: filter.assignmentId, studentId: filter.studentId, all: filter.all },
          },
        }),
      ),
  });

export const useReviewQueue = () =>
  useQuery({
    queryKey: ["gradings", "review-queue"],
    queryFn: () => unwrap(api.GET("/review-queue")),
    staleTime: 0,
  });

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

export const useStoredFileBlob = (fileId: string) =>
  useQuery({
    queryKey: ["stored-files", fileId],
    queryFn: () =>
      unwrap(api.GET("/stored-files/{contentId}", { params: { path: { contentId: fileId } }, parseAs: "blob" })),
    staleTime: Infinity,
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

export const useModelHealth = () =>
  useMutation({ mutationFn: () => unwrap(api.GET("/health/model")) });
