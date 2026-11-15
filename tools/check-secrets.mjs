import { readFileSync, existsSync, statSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { pathToFileURL } from "node:url";

const PATTERNS = [
  ["private key", /-----BEGIN [A-Z ]*PRIVATE KEY-----/],
  ["AWS access key", /\bAKIA[0-9A-Z]{16}\b/],
  ["GitHub token", /\bgh[pousr]_[A-Za-z0-9]{36,}\b/],
  ["OpenAI/Anthropic style key", /\bsk-(ant-)?[A-Za-z0-9_-]{20,}\b/],
  ["Slack token", /\bxox[abprs]-[A-Za-z0-9-]{10,}\b/],
  ["Azure storage key", /AccountKey=[A-Za-z0-9+/=]{40,}/],
  ["JWT", /\beyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}/],
  ["US SSN", /\b(?!000|666|9\d\d)\d{3}-(?!00)\d{2}-(?!0000)\d{4}\b/],
];

const SKIP = /(^|[\\/])(bin|obj|node_modules|dist|\.git|TestResults)([\\/]|$)|\.(png|jpg|jpeg|gif|ico|pdf|zip|dll|exe|woff2?|lock)$|package-lock\.json$|check-secrets\.mjs$/i;

export function findSecrets(text) {
  const hits = [];
  text.split(/\r?\n/).forEach((line, i) => {
    for (const [name, re] of PATTERNS) if (re.test(line)) hits.push({ line: i + 1, kind: name });
  });
  return hits;
}

export function scanFiles(files) {
  const results = [];
  for (const file of files) {
    if (SKIP.test(file) || !existsSync(file) || statSync(file).size > 2_000_000) continue;
    for (const hit of findSecrets(readFileSync(file, "utf8"))) results.push({ file, ...hit });
  }
  const envFiles = files.filter((f) => /(^|[\\/])\.env(\.[\w-]+)?$/.test(f) && !/\.example$/.test(f));
  for (const file of envFiles) results.push({ file, line: 0, kind: ".env file must not be committed (use .env.example)" });
  return results;
}

function trackedAndNewFiles() {
  try {
    return execFileSync("git", ["ls-files", "--cached", "--others", "--exclude-standard"], { encoding: "utf8" }).split("\n").filter(Boolean);
  } catch {
    return [];
  }
}

export function scanRepo() {
  return scanFiles(trackedAndNewFiles());
}

export function formatSecrets(results) {
  return [
    "Possible secret or PHI found. Never commit credentials or real identifiers; use configuration/user-secrets and synthetic data.",
    ...results.map((r) => `  ${r.file}:${r.line}  ${r.kind}`),
  ].join("\n");
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? "").href) {
  const results = scanRepo();
  if (results.length) {
    console.error(formatSecrets(results));
    process.exit(1);
  }
  console.log("check-secrets: ok");
}
