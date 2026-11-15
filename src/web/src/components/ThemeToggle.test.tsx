import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "next-themes";
import { afterEach, expect, test } from "vitest";
import ThemeToggle from "./ThemeToggle";

afterEach(() => {
  localStorage.clear();
  document.documentElement.className = "";
});

function renderToggle() {
  return render(
    <ThemeProvider attribute="class" defaultTheme="system" enableSystem>
      <ThemeToggle />
    </ThemeProvider>,
  );
}

test("the theme toggle switches between light and dark and remembers the choice", async () => {
  const first = renderToggle();
  const toggle = screen.getByRole("switch", { name: "Dark mode" });
  expect(toggle).not.toBeChecked();

  await userEvent.click(toggle);

  expect(toggle).toBeChecked();
  expect(document.documentElement).toHaveClass("dark");
  expect(localStorage.getItem("theme")).toBe("dark");

  first.unmount();
  renderToggle();
  const restored = screen.getByRole("switch", { name: "Dark mode" });
  expect(restored).toBeChecked();

  await userEvent.click(restored);

  expect(restored).not.toBeChecked();
  expect(document.documentElement).toHaveClass("light");
  expect(localStorage.getItem("theme")).toBe("light");
});
