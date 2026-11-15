import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router";
import { afterEach, expect, test, vi } from "vitest";
import JobDetailsPage from "./JobDetailsPage";

afterEach(() => vi.unstubAllGlobals());

const completedJob = {
  id: "job-1",
  url: "https://example.com/",
  maxDepth: 2,
  status: "Completed",
  createdAt: "2026-11-15T10:00:00Z",
  startedAt: "2026-11-15T10:00:01Z",
  completedAt: "2026-11-15T10:00:05Z",
  failureReason: null,
  progress: { queued: 0, done: 2, skipped: 1, failed: 0 },
};

const tree = {
  id: 1,
  url: "https://example.com/",
  depth: 0,
  status: "Done",
  domainLinkRatio: 0.5,
  links: ["https://example.com/about", "https://other.org/"],
  children: [
    {
      id: 2,
      url: "https://example.com/about",
      depth: 1,
      status: "Done",
      domainLinkRatio: 1,
      links: ["https://example.com/"],
      children: [],
    },
    {
      id: 3,
      url: "https://example.com/logo.png",
      depth: 1,
      status: "Skipped",
      domainLinkRatio: null,
      links: [],
      children: [],
    },
  ],
};

test("a completed job shows its page tree with each page's Domain Link Ratio", async () => {
  vi.stubGlobal(
    "fetch",
    vi.fn((url: string) =>
      Promise.resolve(
        Response.json(url.endsWith("/tree") ? tree : completedJob),
      ),
    ),
  );

  render(
    <QueryClientProvider client={new QueryClient()}>
      <MemoryRouter initialEntries={["/jobs/job-1"]}>
        <Routes>
          <Route path="/jobs/:jobId" element={<JobDetailsPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );

  const treeView = await screen.findByRole("tree");
  const root = within(treeView).getAllByRole("treeitem")[0];
  expect(root).toHaveTextContent("https://example.com/");
  expect(root).toHaveTextContent("Domain Link Ratio 50%");
  expect(root).toHaveTextContent("2 outgoing links");
  expect(
    within(treeView)
      .getByRole("link", { name: "https://example.com/about" })
      .closest("li"),
  ).toHaveTextContent("Domain Link Ratio 100%");
  expect(
    within(treeView)
      .getByRole("link", { name: "https://example.com/logo.png" })
      .closest("li"),
  ).toHaveTextContent("Skipped");
});

test("an active job can be canceled", async () => {
  const runningJob = {
    ...completedJob,
    status: "Running",
    completedAt: null,
    progress: { queued: 3, done: 1, skipped: 0, failed: 0 },
  };
  const canceledJob = {
    ...runningJob,
    status: "Canceled",
    completedAt: "2026-11-15T10:00:06Z",
  };
  const fetchMock = vi.fn((_url: string, init?: RequestInit) =>
    Promise.resolve(
      Response.json(init?.method === "POST" ? canceledJob : runningJob),
    ),
  );
  vi.stubGlobal("fetch", fetchMock);

  render(
    <QueryClientProvider client={new QueryClient()}>
      <MemoryRouter initialEntries={["/jobs/job-1"]}>
        <Routes>
          <Route path="/jobs/:jobId" element={<JobDetailsPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );

  await userEvent.click(
    await screen.findByRole("button", { name: "Cancel crawl" }),
  );

  expect(await screen.findByText("Canceled")).toBeInTheDocument();
  expect(
    screen.queryByRole("button", { name: "Cancel crawl" }),
  ).not.toBeInTheDocument();
  expect(fetchMock).toHaveBeenCalledWith("/api/jobs/job-1/cancel", {
    method: "POST",
  });
});
