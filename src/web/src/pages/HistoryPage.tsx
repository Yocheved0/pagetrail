import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { ChevronLeftIcon, ChevronRightIcon } from "lucide-react";
import { useState } from "react";
import { Link } from "react-router";
import { listJobs } from "../api";
import ErrorAlert from "../components/ErrorAlert";
import StatusBadge from "../components/StatusBadge";
import TruncatedTooltip from "../components/TruncatedTooltip";
import { Button } from "../components/ui/button";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "../components/ui/card";
import { Skeleton } from "../components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "../components/ui/table";
import { formatTime } from "../format";

const pageSize = 20;

export default function HistoryPage() {
  const [page, setPage] = useState(1);
  const history = useQuery({
    queryKey: ["history", page],
    queryFn: () => listJobs(page, pageSize),
    placeholderData: keepPreviousData,
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-xl">
          <h2>History</h2>
        </CardTitle>
      </CardHeader>
      <CardContent>
        {history.isPending && (
          <Skeleton
            role="status"
            aria-label="Loading history"
            className="h-64 w-full"
          />
        )}
        {history.isError && (
          <ErrorAlert
            title="Could not load history"
            message={history.error.message}
          />
        )}
        {history.isSuccess && history.data.totalCount === 0 && (
          <p className="text-muted-foreground">No crawl jobs yet.</p>
        )}
        {history.isSuccess && history.data.totalCount > 0 && (
          <div className="grid gap-4">
            <Table
              className={history.isPlaceholderData ? "opacity-60" : undefined}
            >
              <TableHeader>
                <TableRow>
                  <TableHead scope="col">URL</TableHead>
                  <TableHead scope="col">Status</TableHead>
                  <TableHead scope="col">Created</TableHead>
                  <TableHead scope="col">Started</TableHead>
                  <TableHead scope="col">Completed</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {history.data.items.map((job) => (
                  <TableRow key={job.id}>
                    <TableCell className="max-w-sm">
                      <TruncatedTooltip text={job.url}>
                        <Link
                          to={`/jobs/${job.id}`}
                          className="block truncate font-medium hover:underline"
                        >
                          {job.url}
                        </Link>
                      </TruncatedTooltip>
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={job.status} />
                    </TableCell>
                    <TableCell className="tabular-nums">
                      {formatTime(job.createdAt)}
                    </TableCell>
                    <TableCell className="tabular-nums">
                      {formatTime(job.startedAt)}
                    </TableCell>
                    <TableCell className="tabular-nums">
                      {formatTime(job.completedAt)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
            <HistoryPager
              page={page}
              pageCount={Math.max(
                1,
                Math.ceil(history.data.totalCount / pageSize),
              )}
              onPageChange={setPage}
            />
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function HistoryPager({
  page,
  pageCount,
  onPageChange,
}: {
  page: number;
  pageCount: number;
  onPageChange: (page: number) => void;
}) {
  return (
    <nav
      aria-label="History pages"
      className="flex items-center justify-center gap-3"
    >
      <Button
        type="button"
        variant="ghost"
        disabled={page <= 1}
        onClick={() => onPageChange(page - 1)}
      >
        <ChevronLeftIcon aria-hidden data-icon="inline-start" />
        Previous
      </Button>
      <span className="text-sm text-muted-foreground tabular-nums">
        Page {page} of {pageCount}
      </span>
      <Button
        type="button"
        variant="ghost"
        disabled={page >= pageCount}
        onClick={() => onPageChange(page + 1)}
      >
        Next
        <ChevronRightIcon aria-hidden data-icon="inline-end" />
      </Button>
    </nav>
  );
}
