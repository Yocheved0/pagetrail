import { readFileSync, writeFileSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { pathToFileURL } from "node:url";

export const AI_MARKER = /co-authored-by:[^\n]*(claude|anthropic|copilot|cursor|openai|chatgpt)|generated (with|by)[^\n]*(claude|ai\b|copilot|chatgpt)|claude code|claude\.(ai|com)|claude-session|noreply@anthropic\.com|🤖/i;
export const CONVENTIONAL = /^(feat|fix|refactor|test|docs|chore|ci|build|perf|style|revert)(\([\w\-./ ]+\))?!?: \S.{0,70}$/;

export function stripAiLines(message) {
  return message
    .split("\n")
    .filter((line) => !AI_MARKER.test(line))
    .join("\n")
    .replace(/\n{3,}/g, "\n\n")
    .trimEnd() + "\n";
}

export function checkMessage(message) {
  const problems = [];
  const subject = message.split("\n").find((l) => l.trim() && !l.startsWith("#")) ?? "";
  if (/^(Merge|Revert) /.test(subject) || /^(fixup|squash|amend)! /.test(subject)) return problems;
  if (!CONVENTIONAL.test(subject)) problems.push(`Subject "${subject}" is not a conventional commit: type(scope): summary, max 72 chars. Types: feat fix refactor test docs chore ci build perf style revert.`);
  if (AI_MARKER.test(message)) problems.push("Message mentions an AI tool or co-author. Remove it.");
  return problems;
}

function hookMode(file) {
  const cleaned = stripAiLines(readFileSync(file, "utf8"));
  writeFileSync(file, cleaned);
  const problems = checkMessage(cleaned);
  if (problems.length) {
    console.error(problems.join("\n"));
    process.exit(1);
  }
}

function rangeMode(range) {
  const log = execFileSync("git", ["log", "--format=%H%x1f%B%x1e", range], { encoding: "utf8" });
  const problems = [];
  for (const entry of log.split("\x1e").map((e) => e.trim()).filter(Boolean)) {
    const [sha, body] = entry.split("\x1f");
    for (const p of checkMessage(body)) problems.push(`${sha.slice(0, 8)}: ${p}`);
  }
  const prBody = process.env.PR_BODY ?? "";
  const prTitle = process.env.PR_TITLE ?? "";
  if (AI_MARKER.test(prBody) || AI_MARKER.test(prTitle)) problems.push("Pull request title or description mentions an AI tool. Edit it.");
  if (problems.length) {
    console.error(problems.join("\n"));
    process.exit(1);
  }
  console.log("commit-policy: ok");
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? "").href) {
  const [mode, arg] = process.argv.slice(2);
  if (mode === "hook") hookMode(arg);
  else if (mode === "range") rangeMode(arg);
  else {
    console.error("usage: node tools/commit-policy.mjs hook <msg-file> | range <a..b>");
    process.exit(2);
  }
}
