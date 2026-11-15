import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Link } from "react-router";
import { listJobs } from "../api";
import { formatTime } from "../format";

const pageSize = 20;

export default function HistoryPage() {
  const [page, setPage] = useState(1);
  const history = useQuery({
    queryKey: ["history", page],
    queryFn: () => listJobs(page, pageSize),
    placeholderData: keepPreviousData,
  });

  if (history.isPending) {
    return <p>Loading history…</p>;
  }
  if (history.isError) {
    return <p role="alert">Could not load history: {history.error.message}</p>;
  }

  const { items, totalCount } = history.data;
  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize));
  if (totalCount === 0) {
    return <p>No crawl jobs yet.</p>;
  }

  return (
    <section>
      <h2>History</h2>
      <table>
        <thead>
          <tr>
            <th scope="col">URL</th>
            <th scope="col">Status</th>
            <th scope="col">Created</th>
            <th scope="col">Started</th>
            <th scope="col">Completed</th>
          </tr>
        </thead>
        <tbody>
          {items.map((job) => (
            <tr key={job.id}>
              <td>
                <Link to={`/jobs/${job.id}`}>{job.url}</Link>
              </td>
              <td>{job.status}</td>
              <td>{formatTime(job.createdAt)}</td>
              <td>{formatTime(job.startedAt)}</td>
              <td>{formatTime(job.completedAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <nav aria-label="History pages">
        <button
          type="button"
          disabled={page <= 1}
          onClick={() => setPage(page - 1)}
        >
          Previous
        </button>
        <span>
          Page {page} of {pageCount}
        </span>
        <button
          type="button"
          disabled={page >= pageCount}
          onClick={() => setPage(page + 1)}
        >
          Next
        </button>
      </nav>
    </section>
  );
}
