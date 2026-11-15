import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

const apiTarget = "http://localhost:5080";

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      "/api": apiTarget,
      "/health": apiTarget,
    },
  },
  test: {
    environment: "jsdom",
    setupFiles: ["./src/setupTests.ts"],
  },
});
