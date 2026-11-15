import { useEffect, useState } from "react";

type ApiState = "checking" | "healthy" | "unavailable";

export default function App() {
  const [apiState, setApiState] = useState<ApiState>("checking");

  useEffect(() => {
    fetch("/health")
      .then((response) => setApiState(response.ok ? "healthy" : "unavailable"))
      .catch(() => setApiState("unavailable"));
  }, []);

  return (
    <main>
      <h1>PageTrail</h1>
      <p>API: {apiState}</p>
    </main>
  );
}
