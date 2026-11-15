import { render, screen } from "@testing-library/react";
import { afterEach, expect, test, vi } from "vitest";
import App from "./App";

afterEach(() => vi.unstubAllGlobals());

test("shows the API as healthy when the health check succeeds", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 200 })));

  render(<App />);

  expect(await screen.findByText("API: healthy")).toBeInTheDocument();
});

test("shows the API as unavailable when the health check fails", async () => {
  vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("network")));

  render(<App />);

  expect(await screen.findByText("API: unavailable")).toBeInTheDocument();
});
