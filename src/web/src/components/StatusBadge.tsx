import type { JobStatus, PageStatus } from "../api";
import { Badge } from "./ui/badge";

const neutral = "bg-secondary text-secondary-foreground";
const running = "bg-blue-100 text-blue-800 dark:bg-blue-950 dark:text-blue-300";
const success =
  "bg-green-100 text-green-800 dark:bg-green-950 dark:text-green-300";
const failure = "bg-red-100 text-red-800 dark:bg-red-950 dark:text-red-300";
const warning =
  "bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300";

const statusColors: Record<JobStatus | PageStatus, string> = {
  Pending: neutral,
  Queued: neutral,
  Running: running,
  Completed: success,
  Done: success,
  Failed: failure,
  Canceled: warning,
  Skipped: warning,
};

export default function StatusBadge({
  status,
}: {
  status: JobStatus | PageStatus;
}) {
  return <Badge className={statusColors[status]}>{status}</Badge>;
}
