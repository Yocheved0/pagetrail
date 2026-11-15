export type JobStatus =
  "Pending" | "Running" | "Completed" | "Failed" | "Canceled";

export interface JobProgress {
  queued: number;
  done: number;
  skipped: number;
  failed: number;
}

export interface Job {
  id: string;
  url: string;
  maxDepth: number;
  status: JobStatus;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
  failureReason: string | null;
  progress: JobProgress;
}

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {}

export const finishedStatuses: readonly JobStatus[] = [
  "Completed",
  "Failed",
  "Canceled",
];

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, init);
  if (!response.ok) {
    throw new ApiError(await describeFailure(response));
  }
  return (await response.json()) as T;
}

async function describeFailure(response: Response): Promise<string> {
  const problem = (await response.json().catch(() => ({}))) as ProblemDetails;
  const fieldErrors = Object.values(problem.errors ?? {}).flat();
  if (fieldErrors.length > 0) {
    return fieldErrors.join(" ");
  }
  return (
    problem.detail ??
    problem.title ??
    `Request failed with status ${response.status}`
  );
}

export function createJob(url: string, maxDepth: number) {
  return request<{ jobId: string }>("/api/jobs", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ url, maxDepth }),
  });
}

export function getJob(jobId: string) {
  return request<Job>(`/api/jobs/${encodeURIComponent(jobId)}`);
}

export type PageStatus = "Queued" | "Done" | "Skipped" | "Failed";

export interface PageNode {
  id: number;
  url: string;
  depth: number;
  status: PageStatus;
  domainLinkRatio: number | null;
  links: string[];
  children: PageNode[];
}

export function getTree(jobId: string) {
  return request<PageNode>(`/api/jobs/${encodeURIComponent(jobId)}/tree`);
}

export type JobSummary = Pick<
  Job,
  "id" | "url" | "status" | "createdAt" | "startedAt" | "completedAt"
>;

export interface JobHistory {
  items: JobSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export function listJobs(page: number, pageSize: number) {
  return request<JobHistory>(`/api/jobs?page=${page}&pageSize=${pageSize}`);
}

export function cancelJob(jobId: string) {
  return request<Job>(`/api/jobs/${encodeURIComponent(jobId)}/cancel`, {
    method: "POST",
  });
}
