import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import { useParams } from "react-router";
import { toast } from "sonner";
import {
  cancelJob,
  finishedStatuses,
  getJob,
  getTree,
  type Job,
  type JobStatus,
} from "../api";
import ErrorAlert from "../components/ErrorAlert";
import PageTree from "../components/PageTree";
import StatusBadge from "../components/StatusBadge";
import TruncatedTooltip from "../components/TruncatedTooltip";
import { Button } from "../components/ui/button";
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "../components/ui/card";
import { Progress } from "../components/ui/progress";
import { Skeleton } from "../components/ui/skeleton";
import { formatTime } from "../format";

const pollIntervalMs = 1500;

const isFinished = (status: JobStatus) => finishedStatuses.includes(status);

function useFinishedToast(status: JobStatus | undefined) {
  const previousStatus = useRef(status);
  useEffect(() => {
    const previous = previousStatus.current;
    if (status && previous && !isFinished(previous) && isFinished(status)) {
      toast.info(`Crawl ${status.toLowerCase()}`);
    }
    previousStatus.current = status;
  }, [status]);
}

function CrawlProgress({ job }: { job: Job }) {
  const { queued, done, skipped, failed } = job.progress;
  const total = queued + done + skipped + failed;
  const processed = total - queued;
  return (
    <div className="grid gap-2">
      <Progress
        value={total === 0 ? 0 : (processed / total) * 100}
        aria-label="Crawl progress"
      />
      <p className="text-sm text-muted-foreground tabular-nums">
        {processed} of {total} pages processed
      </p>
    </div>
  );
}

function StatTile({ label, value }: { label: string; value: number }) {
  return (
    <Card size="sm">
      <CardContent>
        <p className="text-sm text-muted-foreground">{label}</p>
        <p className="text-2xl font-semibold tabular-nums">{value}</p>
      </CardContent>
    </Card>
  );
}

function PageStats({ job }: { job: Job }) {
  const { queued, done, skipped, failed } = job.progress;
  return (
    <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
      <StatTile label="Queued" value={queued} />
      <StatTile label="Done" value={done} />
      <StatTile label="Skipped" value={skipped} />
      <StatTile label="Failed" value={failed} />
    </div>
  );
}

function Detail({ label, value }: { label: string; value: string | number }) {
  return (
    <div>
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="tabular-nums">{value}</dd>
    </div>
  );
}

function CompletedTree({ jobId }: { jobId: string }) {
  const tree = useQuery({
    queryKey: ["tree", jobId],
    queryFn: () => getTree(jobId),
  });
  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <h3>Pages</h3>
        </CardTitle>
      </CardHeader>
      <CardContent>
        {tree.isPending && (
          <Skeleton
            role="status"
            aria-label="Loading pages"
            className="h-24 w-full"
          />
        )}
        {tree.isError && (
          <ErrorAlert
            title="Could not load pages"
            message={tree.error.message}
          />
        )}
        {tree.isSuccess && <PageTree root={tree.data} />}
      </CardContent>
    </Card>
  );
}

function CancelButton({ jobId }: { jobId: string }) {
  const queryClient = useQueryClient();
  const cancel = useMutation({
    mutationFn: () => cancelJob(jobId),
    onSuccess: (job) => queryClient.setQueryData(["job", jobId], job),
  });
  return (
    <div className="grid justify-items-end gap-2">
      <Button
        type="button"
        variant="destructive"
        size="sm"
        disabled={cancel.isPending}
        onClick={() => cancel.mutate()}
      >
        {cancel.isPending ? "Canceling…" : "Cancel crawl"}
      </Button>
      {cancel.isError && (
        <ErrorAlert title="Could not cancel" message={cancel.error.message} />
      )}
    </div>
  );
}

function JobSkeleton() {
  return (
    <div role="status" aria-label="Loading job" className="grid gap-6">
      <Skeleton className="h-48 w-full" />
      <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
        {[1, 2, 3, 4].map((tile) => (
          <Skeleton key={tile} className="h-24" />
        ))}
      </div>
    </div>
  );
}

export default function JobDetailsPage() {
  const { jobId = "" } = useParams();
  const job = useQuery({
    queryKey: ["job", jobId],
    queryFn: () => getJob(jobId),
    refetchInterval: (query) =>
      query.state.data && isFinished(query.state.data.status)
        ? false
        : pollIntervalMs,
  });
  useFinishedToast(job.data?.status);

  if (job.isPending) {
    return <JobSkeleton />;
  }
  if (job.isError) {
    return (
      <ErrorAlert title="Could not load job" message={job.error.message} />
    );
  }

  const data = job.data;
  return (
    <section className="flex flex-col gap-6">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-xl">
            <h2>Job {data.id}</h2>
            <StatusBadge status={data.status} />
          </CardTitle>
          <CardDescription className="min-w-0">
            <TruncatedTooltip text={data.url}>
              <a
                href={data.url}
                target="_blank"
                rel="noreferrer"
                className="block truncate hover:underline"
              >
                {data.url}
              </a>
            </TruncatedTooltip>
          </CardDescription>
          {!isFinished(data.status) && (
            <CardAction>
              <CancelButton jobId={data.id} />
            </CardAction>
          )}
        </CardHeader>
        <CardContent className="grid gap-5">
          {data.failureReason && (
            <ErrorAlert title="Crawl failed" message={data.failureReason} />
          )}
          <CrawlProgress job={data} />
          <dl className="grid gap-4 text-sm sm:grid-cols-4">
            <Detail label="Max depth" value={data.maxDepth} />
            <Detail label="Created" value={formatTime(data.createdAt)} />
            <Detail label="Started" value={formatTime(data.startedAt)} />
            <Detail label="Completed" value={formatTime(data.completedAt)} />
          </dl>
        </CardContent>
      </Card>
      <PageStats job={data} />
      {data.status === "Completed" && <CompletedTree jobId={data.id} />}
    </section>
  );
}
