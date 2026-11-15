import { useMutation } from "@tanstack/react-query";
import { Loader2Icon } from "lucide-react";
import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router";
import { createJob } from "../api";
import DepthStepper from "../components/DepthStepper";
import ErrorAlert from "../components/ErrorAlert";
import { Button } from "../components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "../components/ui/card";
import { Input } from "../components/ui/input";
import { Label } from "../components/ui/label";

const defaultDepth = 2;

export default function StartCrawlPage() {
  const navigate = useNavigate();
  const [url, setUrl] = useState("");
  const [maxDepth, setMaxDepth] = useState(String(defaultDepth));
  const start = useMutation({
    mutationFn: () =>
      createJob(url.trim(), maxDepth === "" ? defaultDepth : Number(maxDepth)),
    onSuccess: ({ jobId }) => navigate(`/jobs/${jobId}`),
  });

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    start.mutate();
  }

  return (
    <div className="grid flex-1 place-items-center">
      <Card className="w-full max-w-lg">
        <CardHeader>
          <CardTitle className="text-xl">
            <h2>Start a crawl</h2>
          </CardTitle>
          <CardDescription>
            Crawl a site and map its pages and links.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="grid gap-5">
            <div className="flex items-start gap-3">
              <div className="grid flex-1 gap-2">
                <Label htmlFor="url">Website URL</Label>
                <Input
                  id="url"
                  type="text"
                  required
                  value={url}
                  onChange={(e) => setUrl(e.target.value)}
                  placeholder="https://example.com"
                />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="maxDepth">Max depth</Label>
                <DepthStepper
                  id="maxDepth"
                  value={maxDepth}
                  fallback={defaultDepth}
                  onChange={setMaxDepth}
                />
              </div>
            </div>
            <Button type="submit" className="w-full" disabled={start.isPending}>
              {start.isPending && (
                <Loader2Icon aria-hidden className="animate-spin" />
              )}
              {start.isPending ? "Starting…" : "Start Crawl"}
            </Button>
            {start.isError && (
              <ErrorAlert
                title="Could not start the crawl"
                message={start.error.message}
              />
            )}
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
