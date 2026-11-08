import createClient from "openapi-fetch";
import type { components, paths } from "./schema";

// Types come from the backend's Swagger spec (npm run gen:api); never write them by hand.
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
export type StudentAssignmentColumn = components["schemas"]["StudentAssignmentColumn"];
export type GoogleStatus = components["schemas"]["GoogleConnectionStatus"];
export type ModelHealth = components["schemas"]["ModelHealthResponse"];
export type StoredSubmission = components["schemas"]["StoredSubmissionResponse"];
export type StoredVersion = components["schemas"]["StoredVersionResponse"];
export type StoredFile = components["schemas"]["StoredFileResponse"];

export type CodeFile = components["schemas"]["CodeFileResponse"];
export type GradingResult = components["schemas"]["GradingResponse"];
export type AssignmentGrading = components["schemas"]["AssignmentGradingResponse"];
export type CodeMistake = components["schemas"]["CodeMistake"];
export type ExtractionSummary = components["schemas"]["ExtractionSummary"];
export type StoredInstructions = components["schemas"]["StoredInstructionsResponse"];
export type InstructionsFile = components["schemas"]["InstructionsFileResponse"];

/** URL of a student's stored profile photo, by their Google user ID (404 when there is none). */
export const studentPhotoUrl = (userId: string) => `/api/students/${encodeURIComponent(userId)}/photo`;

/** URL of one extracted file of a version; path comes from the version's code list. */
export const codeFileUrl = (versionId: string, path: string) =>
  `/api/versions/${versionId}/code/file?path=${encodeURIComponent(path)}`;

/** URL of a stored file: shown in the browser, or saved under the student's file name when download is true. */
export const storedFileUrl = (fileId: string, download = false) =>
  `/api/stored-files/${fileId}${download ? "?download=true" : ""}`;

/** URL of a stored instructions file of an assignment, shown in the browser or downloaded under its name. */
export const instructionsFileUrl = (courseId: string, assignmentId: string, fileId: string, download = false) =>
  `/api/courses/${courseId}/assignments/${assignmentId}/instructions/files/${fileId}${download ? "?download=true" : ""}`;

// "/api" is forwarded to the ASP.NET API by the Vite dev server (see vite.config.ts).
export const api = createClient<paths>({ baseUrl: "/api" });

/** An error response from the API, with the message from its ProblemDetails (or status) body. */
export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }

  /** 409 = Google account not connected or authorization expired. */
  get isNotConnected() {
    return this.status === 409;
  }
}

/** Awaits an openapi-fetch call and returns its data, or throws an ApiError with the server's explanation. */
export async function unwrap<T>(call: Promise<{ data?: T; error?: unknown; response: Response }>): Promise<T> {
  const { data, error, response } = await call;
  if (!response.ok) {
    const body = (error ?? {}) as { detail?: string; message?: string; title?: string };
    throw new ApiError(response.status, body.detail ?? body.message ?? body.title ?? response.statusText);
  }
  return data as T;
}
