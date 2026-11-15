import { execFileSync, spawnSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { pathToFileURL } from "node:url";

const git = (...args) => execFileSync("git", args, { encoding: "utf8" });

export function loadTerms() {
  const file = git("rev-parse", "--git-path", "info/forbidden-terms").trim();
  if (!existsSync(file)) return [];
  return readFileSync(file, "utf8")
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line && !line.startsWith("#"))
    .map((pattern) => new RegExp(pattern, "i"));
}

export const findTerm = (text, terms) => terms.find((term) => term.test(text));

function stagedPaths() {
  return git("diff", "--cached", "--name-only", "--diff-filter=ACMR", "-z").split("\0").filter(Boolean);
}

function ignoredPaths(paths) {
  if (paths.length === 0) return [];
  const result = spawnSync("git", ["check-ignore", "--no-index", "-z", "--stdin"], { input: paths.join("\0"), encoding: "utf8" });
  return result.stdout.split("\0").filter(Boolean);
}

function addedLines() {
  const lines = [];
  let file = "";
  for (const line of git("diff", "--cached", "-U0", "--no-color", "--diff-filter=ACMR").split("\n")) {
    if (line.startsWith("+++ ")) file = line.replace(/^\+\+\+ (b\/)?/, "");
    else if (line.startsWith("+")) lines.push({ file, text: line.slice(1) });
  }
  return lines;
}

function checkStaged() {
  const problems = [];
  const terms = loadTerms();
  const paths = stagedPaths();
  for (const path of ignoredPaths(paths)) problems.push(`${path}: private path, keep it out of the repo (git restore --staged "${path}").`);
  for (const path of paths) {
    if (findTerm(path, terms)) problems.push(`${path}: file name contains a term listed in .git/info/forbidden-terms.`);
  }
  for (const { file, text } of addedLines()) {
    if (findTerm(text, terms)) problems.push(`${file}: "${text.trim().slice(0, 120)}" contains a term listed in .git/info/forbidden-terms.`);
  }
  return problems;
}

function checkMessage(messageFile) {
  const message = readFileSync(messageFile, "utf8")
    .split("\n")
    .filter((line) => !line.startsWith("#"))
    .join("\n");
  return findTerm(message, loadTerms()) ? ["Commit message contains a term listed in .git/info/forbidden-terms."] : [];
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? "").href) {
  const [mode, arg] = process.argv.slice(2);
  const problems = mode === "staged" ? checkStaged() : mode === "message" ? checkMessage(arg) : null;
  if (problems === null) {
    console.error("usage: node tools/check-commit-scope.mjs staged | message <msg-file>");
    process.exit(2);
  }
  if (problems.length) {
    console.error(problems.join("\n"));
    process.exit(1);
  }
}
