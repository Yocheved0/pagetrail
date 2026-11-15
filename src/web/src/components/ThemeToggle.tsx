import { cn } from "cn";
import { MoonIcon, SunIcon } from "lucide-react";
import { useTheme } from "next-themes";
import { Switch } from "./ui/switch";

const themeSwitchingClass = "theme-switching";
const iconClass =
  "absolute size-3 text-foreground transition-[opacity,scale,rotate,filter] duration-250 ease-out-strong";
const hiddenIconClass = "scale-50 opacity-0 blur-[2px]";

export default function ThemeToggle() {
  const { resolvedTheme, setTheme } = useTheme();
  const isDark = resolvedTheme === "dark";

  function changeTheme(dark: boolean) {
    const root = document.documentElement;
    root.classList.add(themeSwitchingClass);
    setTheme(dark ? "dark" : "light");
    // WHY: two frames let the new colours paint with transitions off, so hundreds of elements don't animate at once
    requestAnimationFrame(() =>
      requestAnimationFrame(() => root.classList.remove(themeSwitchingClass)),
    );
  }

  return (
    <Switch
      size="lg"
      aria-label="Dark mode"
      checked={isDark}
      onCheckedChange={changeTheme}
    >
      <SunIcon
        aria-hidden
        className={cn(iconClass, isDark && cn(hiddenIconClass, "-rotate-90"))}
      />
      <MoonIcon
        aria-hidden
        className={cn(iconClass, !isDark && cn(hiddenIconClass, "rotate-90"))}
      />
    </Switch>
  );
}
