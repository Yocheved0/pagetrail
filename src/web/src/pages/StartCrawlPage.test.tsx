import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router";
import { afterEach, expect, test, vi } from "vitest";
import StartCrawlPage from "./StartCrawlPage";

afterEach(() => vi.unstubAllGlobals());

function renderStartCrawl() {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <MemoryRouter initialEntries={["/"]}>
        <Routes>
          <Route path="/" element={<StartCrawlPage />} />
          <Route path="/jobs/:jobId" element={<p>details page</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

test("submitting a URL creates a job and opens its details", async () => {
  const fetchMock = vi
    .fn()
    .mockResolvedValue(Response.json({ jobId: "job-1" }, { status: 202 }));
  vi.stubGlobal("fetch", fetchMock);
  renderStartCrawl();

  await userEvent.type(
    screen.getByLabelText("Website URL"),
    "https://example.com",
  );
  await userEvent.click(screen.getByRole("button", { name: "Start Crawl" }));

  expect(await screen.findByText("details page")).toBeInTheDocument();
  const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
  expect(url).toBe("/api/jobs");
  expect(JSON.parse(init.body as string)).toEqual({
    url: "https://example.com",
    maxDepth: 2,
  });
});

test("shows the API's validation error when the job is rejected", async () => {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockResolvedValue(
      Response.json(
        {
          title: "One or more validation errors occurred.",
          errors: { Url: ["Url must be an absolute http or https URL."] },
        },
        { status: 400 },
      ),
    ),
  );
  renderStartCrawl();

  await userEvent.type(
    screen.getByLabelText("Website URL"),
    "ftp://example.com",
  );
  await userEvent.click(screen.getByRole("button", { name: "Start Crawl" }));

  expect(await screen.findByRole("alert")).toHaveTextContent(
    "Url must be an absolute http or https URL.",
  );
});
