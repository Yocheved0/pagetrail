import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useParams } from "react-router";
import { cancelJob, finishedStatuses, getJob, getTree, type Job } from "../api";
import PageTree from "../components/PageTree";
import { formatTime } from "../format";

const pollIntervalMs = 1500;

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

function CompletedTree({ jobId }: { jobId: string }) {
  const tree = useQuery({
    queryKey: ["tree", jobId],
    queryFn: () => getTree(jobId),
  });
  if (tree.isPending) {
    return <p>Loading pages…</p>;
  }
  if (tree.isError) {
    return <p role="alert">Could not load pages: {tree.error.message}</p>;
  }
  return <PageTree root={tree.data} />;
}

function CancelButton({ jobId }: { jobId: string }) {
  const queryClient = useQueryClient();
  const cancel = useMutation({
    mutationFn: () => cancelJob(jobId),
    onSuccess: (job) => queryClient.setQueryData(["job", jobId], job),
  });
  return (
    <div>
      <button
        type="button"
        disabled={cancel.isPending}
        onClick={() => cancel.mutate()}
      >
        {cancel.isPending ? "Canceling…" : "Cancel crawl"}
      </button>
      {cancel.isError && (
        <p role="alert">Could not cancel: {cancel.error.message}</p>
      )}
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
      {!finishedStatuses.includes(data.status) && (
        <CancelButton jobId={data.id} />
      )}
      {data.status === "Completed" && <CompletedTree jobId={data.id} />}
    </section>
  );
}
