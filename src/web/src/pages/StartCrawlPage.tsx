import { useMutation } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router";
import { createJob } from "../api";

const defaultDepth = 2;

export default function StartCrawlPage() {
  const navigate = useNavigate();
  const [url, setUrl] = useState("");
  const [maxDepth, setMaxDepth] = useState(defaultDepth);
  const start = useMutation({
    mutationFn: () => createJob(url.trim(), maxDepth),
    onSuccess: ({ jobId }) => navigate(`/jobs/${jobId}`),
  });

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    start.mutate();
  }

  return (
    <section>
      <h2>Start a crawl</h2>
      <form onSubmit={handleSubmit}>
        <label>
          Website URL
          <input
            type="text"
            required
            value={url}
            onChange={(e) => setUrl(e.target.value)}
            placeholder="https://example.com"
          />
        </label>
        <label>
          Max depth
          <input
            type="number"
            min={0}
            max={5}
            value={maxDepth}
            onChange={(e) => setMaxDepth(Number(e.target.value))}
          />
        </label>
        <button type="submit" disabled={start.isPending}>
          {start.isPending ? "Starting…" : "Start Crawl"}
        </button>
      </form>
      {start.isError && <p role="alert">{start.error.message}</p>}
    </section>
  );
}
