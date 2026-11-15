import { useQuery } from "@tanstack/react-query";
import { useParams } from "react-router";
import { finishedStatuses, getJob, type Job } from "../api";

const pollIntervalMs = 1500;

function formatTime(value: string | null) {
  return value ? new Date(value).toLocaleString() : "—";
}

function Progress({ job }: { job: Job }) {
  const { queued, done, skipped, failed } = job.progress;
  const total = queued + done + skipped + failed;
  const processed = total - queued;
  return (
    <div>
      <progress value={processed} max={total} aria-label="Crawl progress" />
      <p>
        {processed} of {total} pages processed ({done} done, {skipped} skipped,{" "}
        {failed} failed)
      </p>
    </div>
  );
}

export default function JobDetailsPage() {
  const { jobId = "" } = useParams();
  const job = useQuery({
    queryKey: ["job", jobId],
    queryFn: () => getJob(jobId),
    refetchInterval: (query) =>
      query.state.data && finishedStatuses.includes(query.state.data.status)
        ? false
        : pollIntervalMs,
  });

  if (job.isPending) {
    return <p>Loading job…</p>;
  }
  if (job.isError) {
    return <p role="alert">Could not load job: {job.error.message}</p>;
  }

  const data = job.data;
  return (
    <section>
      <h2>Job {data.id}</h2>
      <dl>
        <dt>URL</dt>
        <dd>{data.url}</dd>
        <dt>Status</dt>
        <dd>{data.status}</dd>
        <dt>Max depth</dt>
        <dd>{data.maxDepth}</dd>
        <dt>Created</dt>
        <dd>{formatTime(data.createdAt)}</dd>
        <dt>Started</dt>
        <dd>{formatTime(data.startedAt)}</dd>
        <dt>Completed</dt>
        <dd>{formatTime(data.completedAt)}</dd>
        {data.failureReason && (
          <>
            <dt>Failure reason</dt>
            <dd>{data.failureReason}</dd>
          </>
        )}
      </dl>
      <Progress job={data} />
    </section>
  );
}
