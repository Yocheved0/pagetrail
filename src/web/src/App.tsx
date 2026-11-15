import { cn } from "cn";
import {
  HistoryIcon,
  PlayIcon,
  WaypointsIcon,
  type LucideIcon,
} from "lucide-react";
import { Link, NavLink, Route, Routes } from "react-router";
import ThemeToggle from "./components/ThemeToggle";
import HistoryPage from "./pages/HistoryPage";
import JobDetailsPage from "./pages/JobDetailsPage";
import StartCrawlPage from "./pages/StartCrawlPage";

function NavItem({
  to,
  icon: Icon,
  label,
  end,
}: {
  to: string;
  icon: LucideIcon;
  label: string;
  end?: boolean;
}) {
  return (
    <NavLink
      to={to}
      end={end}
      className={({ isActive }) =>
        cn(
          "inline-flex h-8 items-center gap-1.5 rounded-full px-3 text-sm font-medium transition-[color,background-color,box-shadow] duration-150 ease-out-strong outline-none focus-visible:ring-3 focus-visible:ring-ring/50 sm:px-3.5",
          isActive
            ? "bg-background text-foreground shadow-xs dark:ring-1 dark:ring-foreground/10"
            : "text-muted-foreground hover:text-foreground",
        )
      }
    >
      <Icon aria-hidden className="size-4" />
      <span className="sr-only sm:not-sr-only">{label}</span>
    </NavLink>
  );
}

export default function App() {
  return (
    <div className="flex min-h-svh flex-col">
      <header className="sticky top-0 z-40 h-14 border-b bg-background">
        <div className="mx-auto flex h-full max-w-5xl items-center gap-4 px-4">
          <Link
            to="/"
            className="flex items-center gap-2.5 rounded-full outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
          >
            <span className="flex size-8 items-center justify-center rounded-xl bg-primary text-primary-foreground shadow-xs ring-1 ring-foreground/10 ring-inset">
              <WaypointsIcon aria-hidden className="size-4.5" />
            </span>
            <h1 className="sr-only text-[15px] font-semibold tracking-tight sm:not-sr-only">
              PageTrail
            </h1>
          </Link>
          <nav className="ml-2 flex items-center gap-1 rounded-full bg-muted p-1">
            <NavItem to="/" icon={PlayIcon} label="Start Crawl" end />
            <NavItem to="/history" icon={HistoryIcon} label="History" />
          </nav>
          <span className="ml-auto">
            <ThemeToggle />
          </span>
        </div>
      </header>
      <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col px-4 py-8">
        <Routes>
          <Route path="/" element={<StartCrawlPage />} />
          <Route path="/jobs/:jobId" element={<JobDetailsPage />} />
          <Route path="/history" element={<HistoryPage />} />
        </Routes>
      </main>
    </div>
  );
}
