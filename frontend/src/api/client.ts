import createClient from "openapi-fetch";
import type { components, paths } from "./schema";

export type Course = components["schemas"]["CourseResponse"];
export type Assignment = components["schemas"]["AssignmentResponse"];
export type Submission = components["schemas"]["SubmissionResponse"];
export type Attachment = components["schemas"]["SubmissionAttachment"];
export type FetchSummary = components["schemas"]["FetchSummary"];
export type FetchProblem = components["schemas"]["FetchProblem"];
export type CourseSyncSummary = components["schemas"]["CourseSyncSummary"];
export type CourseStudents = components["schemas"]["CourseStudentsResponse"];
export type CourseStudent = components["schemas"]["CourseStudentResponse"];
export type StudentAssignmentResult = components["schemas"]["StudentAssignmentResult"];
export type Gradebook = components["schemas"]["GradebookResponse"];
export type GradebookColumn = components["schemas"]["GradebookColumn"];
export type StudentAssignmentColumn = components["schemas"]["StudentAssignmentColumn"];
export type GoogleStatus = components["schemas"]["GoogleConnectionStatus"];
export type ModelHealth = components["schemas"]["ModelHealthResponse"];
export type StoredSubmission = components["schemas"]["StoredSubmissionResponse"];
export type StoredVersion = components["schemas"]["StoredVersionResponse"];
export type StoredFile = components["schemas"]["StoredFileResponse"];

export type CodeFile = components["schemas"]["CodeFileResponse"];
export type GradingResult = components["schemas"]["GradingResponse"];
export type AssignmentGrading = components["schemas"]["AssignmentGradingResponse"];
export type ReviewQueue = components["schemas"]["ReviewQueueResponse"];
export type ReviewDraft = components["schemas"]["ReviewDraftItem"];
export type UngradedSubmission = components["schemas"]["UngradedSubmissionItem"];
export type JobInfo = components["schemas"]["JobResponse"];
export type JobOverview = components["schemas"]["JobOverviewResponse"];
export type JobOverviewItem = components["schemas"]["JobOverviewItem"];
export type JobStatus = components["schemas"]["JobStatus"];
export type JobType = components["schemas"]["JobType"];
export type GradeAllResult = components["schemas"]["GradeAllResponse"];
export type CommonMistakes = components["schemas"]["CommonMistakesResponse"];
export type MistakeGroup = components["schemas"]["MistakeClusterResponse"];
export type ModelCallPurpose = components["schemas"]["ModelCallPurpose"];
export type StudentMistakeInfo = components["schemas"]["StudentMistakeResponse"];
export type MistakeCategoryInfo = components["schemas"]["MistakeCategoryResponse"];
export type MistakeLabel = components["schemas"]["MistakeLabel"];
export type StudentMistakeFilter = {
  assignmentId?: string;
  category?: MistakeCategory;
  severity?: MistakeSeverity;
  label?: MistakeLabel;
};
export type StudentStrengths = components["schemas"]["StudentStrengthsResponse"];
export type StudentAnalysis = components["schemas"]["StudentAnalysisResponse"];
export type StudentAnalysisKind = components["schemas"]["StudentAnalysisKind"];
export type StudentStrengthGroup = components["schemas"]["StrengthGroupResponse"];
export type StudentStrengthInfo = components["schemas"]["StudentStrengthResponse"];
export type StrengthCategoryInfo = components["schemas"]["StrengthCategoryResponse"];
export type StrengthLabel = components["schemas"]["StrengthLabel"];
export type StudentStrengthFilter = {
  assignmentId?: string;
  category?: MistakeCategory;
  label?: StrengthLabel;
};
export type ModelUsage = components["schemas"]["ModelUsageResponse"];
export type ModelUsageTotal = components["schemas"]["ModelUsageTotal"];
export type ModelCallItem = components["schemas"]["ModelCallResponse"];
export type CodeMistake = components["schemas"]["CodeMistake"];
export type GradingReport = components["schemas"]["GradingReportResponse"];
export type ReportMistake = components["schemas"]["ReportMistakeResponse"];
export type CodeStrength = components["schemas"]["CodeStrength"];
export type ReportStrength = components["schemas"]["ReportStrengthResponse"];
export type SaveReportRequest = components["schemas"]["SaveGradingReportRequest"];
export type MistakeSeverity = components["schemas"]["MistakeSeverity"];
export type MistakeCategory = components["schemas"]["MistakeCategory"];
export type ExtractionSummary = components["schemas"]["ExtractionSummary"];
export type StoredInstructions = components["schemas"]["StoredInstructionsResponse"];
export type InstructionsFile = components["schemas"]["InstructionsFileResponse"];

export const studentPhotoUrl = (userId: string) => `/api/students/${encodeURIComponent(userId)}/photo`;

export const codeFileUrl = (versionId: string, path: string) =>
  `/api/versions/${versionId}/code/file?path=${encodeURIComponent(path)}`;

export const storedFileUrl = (fileId: string, download = false) =>
  `/api/stored-files/${fileId}${download ? "?download=true" : ""}`;

export const instructionsFileUrl = (courseId: string, assignmentId: string, fileId: string, download = false) =>
  `/api/courses/${courseId}/assignments/${assignmentId}/instructions/files/${fileId}${download ? "?download=true" : ""}`;

export const api = createClient<paths>({ baseUrl: "/api" });

export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }

  get isNotConnected() {
    return this.status === 409;
  }
}

export async function unwrap<T>(call: Promise<{ data?: T; error?: unknown; response: Response }>): Promise<T> {
  const { data, error, response } = await call;
  if (!response.ok) {
    const body = (error ?? {}) as { detail?: string; message?: string; title?: string };
    throw new ApiError(response.status, body.detail ?? body.message ?? body.title ?? response.statusText);
  }
  return data as T;
}
