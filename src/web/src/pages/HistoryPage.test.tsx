import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router";
import { afterEach, expect, test, vi } from "vitest";
import HistoryPage from "./HistoryPage";

afterEach(() => vi.unstubAllGlobals());

function renderHistory() {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <MemoryRouter initialEntries={["/history"]}>
        <Routes>
          <Route path="/history" element={<HistoryPage />} />
          <Route path="/jobs/:jobId" element={<p>details page</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

const job = (id: string, url: string, status: string) => ({
  id,
  url,
  status,
  createdAt: "2026-11-15T10:00:00Z",
  startedAt: null,
  completedAt: null,
});

test("lists previous jobs and opens one when clicked", async () => {
  const fetchMock = vi.fn().mockResolvedValue(
    Response.json({
      items: [
        job("job-2", "https://newer.example/", "Running"),
        job("job-1", "https://older.example/", "Completed"),
      ],
      page: 1,
      pageSize: 20,
      totalCount: 2,
    }),
  );
  vi.stubGlobal("fetch", fetchMock);
  renderHistory();

  const rows = await screen.findAllByRole("row");
  expect(rows[1]).toHaveTextContent("https://newer.example/");
  expect(rows[1]).toHaveTextContent("Running");
  expect(rows[2]).toHaveTextContent("https://older.example/");
  expect(fetchMock).toHaveBeenCalledWith(
    "/api/jobs?page=1&pageSize=20",
    undefined,
  );

  await userEvent.click(
    screen.getByRole("link", { name: "https://older.example/" }),
  );

  expect(await screen.findByText("details page")).toBeInTheDocument();
});

test("shows an empty state when there are no jobs", async () => {
  vi.stubGlobal(
    "fetch",
    vi
      .fn()
      .mockResolvedValue(
        Response.json({ items: [], page: 1, pageSize: 20, totalCount: 0 }),
      ),
  );
  renderHistory();

  expect(await screen.findByText("No crawl jobs yet.")).toBeInTheDocument();
});
