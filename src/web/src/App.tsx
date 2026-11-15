import { Link, Route, Routes } from "react-router";
import JobDetailsPage from "./pages/JobDetailsPage";
import StartCrawlPage from "./pages/StartCrawlPage";

export default function App() {
  return (
    <>
      <header>
        <h1>PageTrail</h1>
        <nav>
          <Link to="/">Start Crawl</Link>
        </nav>
      </header>
      <main>
        <Routes>
          <Route path="/" element={<StartCrawlPage />} />
          <Route path="/jobs/:jobId" element={<JobDetailsPage />} />
        </Routes>
      </main>
    </>
  );
}
