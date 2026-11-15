import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";

declare const jsdom: { window: Window };

afterEach(cleanup);

// WHY: Node 25+ defines its own localStorage global, which shadows jsdom's and is undefined without --localstorage-file
Object.defineProperty(globalThis, "localStorage", {
  value: jsdom.window.localStorage,
  configurable: true,
});

Object.defineProperty(window, "matchMedia", {
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
});
