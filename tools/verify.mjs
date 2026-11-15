import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { spawnSync, execFileSync } from "node:child_process";
import { pathToFileURL } from "node:url";
import { scanRepo as scanComments, formatViolations } from "./check-comments.mjs";
import { scanRepo as scanSecrets, formatSecrets } from "./check-secrets.mjs";

const isWindows = process.platform === "win32";

function listFiles() {
  try {
    return execFileSync("git", ["ls-files", "--cached", "--others", "--exclude-standard"], { encoding: "utf8" })
      .split("\n")
      .filter(Boolean)
      .filter((f) => existsSync(f));
  } catch {
    return [];
  }
}

function run(label, cmd, args, cwd = ".") {
  const started = Date.now();
  const result = spawnSync(cmd, args, {
    cwd,
    encoding: "utf8",
    shell: isWindows,
    env: { ...process.env, CI: "true", DOTNET_CLI_TELEMETRY_OPTOUT: "1", DOTNET_NOLOGO: "1" },
    timeout: 10 * 60 * 1000,
    maxBuffer: 64 * 1024 * 1024,
  });
  const seconds = ((Date.now() - started) / 1000).toFixed(1);
  const output = `${result.stdout ?? ""}${result.stderr ?? ""}`.trim();
  const ok = result.status === 0;
  return { label, ok, seconds, output: result.error ? String(result.error) : output };
}

function tail(text, lines = 60) {
  return text.split("\n").slice(-lines).join("\n");
}

function dotnetSteps(files) {
  const solutions = files.filter((f) => /\.(sln|slnx)$/.test(f));
  const projects = files.filter((f) => f.endsWith(".csproj"));
  if (!solutions.length && !projects.length) return [];
  if (!solutions.length) {
    return [() => ({ label: "dotnet solution", ok: false, seconds: "0", output: "C# projects exist but no solution file. Create one at the repo root: dotnet new sln; dotnet sln add <each .csproj>" })];
  }
  const sln = solutions.sort((a, b) => a.split("/").length - b.split("/").length)[0];
  const testProjects = projects.filter((p) => /Microsoft\.NET\.Test\.Sdk|xunit|NUnit|MSTest|TUnit/i.test(readFileSync(p, "utf8")));
  const steps = [
    () => run("dotnet restore", "dotnet", ["restore", sln, "--verbosity", "quiet"]),
    () => run("dotnet build (warnings as errors)", "dotnet", ["build", sln, "--no-restore", "--nologo", "-warnaserror", "-v", "q"]),
    () => run("dotnet format --verify-no-changes", "dotnet", ["format", sln, "--verify-no-changes", "--no-restore", "-v", "q"]),
  ];
  if (testProjects.length) {
    steps.push(() => run("dotnet test", "dotnet", ["test", sln, "--no-build", "--nologo", "-v", "q"]));
  } else {
    steps.push(() => ({ label: "dotnet tests exist", ok: false, seconds: "0", output: "No test project found. Add an xUnit project (dotnet new xunit -o tests/<App>.Tests) and cover the behaviour you built." }));
  }
  return steps;
}

function nodeSteps(files) {
  const dirs = files
    .filter((f) => f.endsWith("package.json") && !f.includes("node_modules/"))
    .map((f) => dirname(f));
  return dirs.flatMap((dir) => {
    const pkg = JSON.parse(readFileSync(join(dir, "package.json"), "utf8"));
    const scripts = pkg.scripts ?? {};
    const steps = [];
    const foreignLocks = ["package-lock.json", "yarn.lock", "bun.lockb", "bun.lock"].filter((f) => existsSync(join(dir, f)));
    if (foreignLocks.length) {
      return [() => ({ label: `pnpm only (${dir})`, ok: false, seconds: "0", output: `${dir} has ${foreignLocks.join(", ")}. This repo uses pnpm: delete those lockfiles and run pnpm install.` })];
    }
    if (!existsSync(join(dir, "node_modules"))) {
      const lock = existsSync(join(dir, "pnpm-lock.yaml"));
      steps.push(() => run(`pnpm install (${dir})`, "pnpm", lock ? ["install", "--frozen-lockfile"] : ["install"], dir));
    }
    for (const name of ["lint", "typecheck", "test", "build"]) {
      if (scripts[name]) steps.push(() => run(`pnpm run ${name} (${dir})`, "pnpm", ["run", name], dir));
    }
    if (!scripts.lint) steps.push(() => ({ label: `lint script (${dir})`, ok: false, seconds: "0", output: `${dir}/package.json has no "lint" script. Add ESLint and a lint script.` }));
    if (!String(pkg.packageManager ?? "").startsWith("pnpm@")) steps.push(() => ({ label: `packageManager field (${dir})`, ok: false, seconds: "0", output: `${dir}/package.json must declare "packageManager": "pnpm@<version>" (use the version printed by pnpm -v).` }));
    return steps;
  });
}

function pythonSteps(files) {
  if (!files.some((f) => /(^|\/)(pyproject\.toml|requirements\.txt)$/.test(f))) return [];
  const has = (tool) => spawnSync(tool, ["--version"], { shell: isWindows }).status === 0;
  const steps = [];
  if (has("ruff")) {
    steps.push(() => run("ruff check", "ruff", ["check", "."]));
    steps.push(() => run("ruff format --check", "ruff", ["format", "--check", "."]));
  } else {
    steps.push(() => ({ label: "ruff", ok: false, seconds: "0", output: "Python project found but ruff is not installed: pip install ruff" }));
  }
  if (has("pytest")) steps.push(() => run("pytest", "pytest", ["-q"]));
  return steps;
}

function hygieneSteps(changedOnly) {
  return [
    () => {
      const v = scanComments({ changedOnly });
      return { label: "no comments", ok: v.length === 0, seconds: "0", output: v.length ? formatViolations(v) : "" };
    },
    () => {
      const s = scanSecrets();
      return { label: "no secrets / PHI", ok: s.length === 0, seconds: "0", output: s.length ? formatSecrets(s) : "" };
    },
  ];
}

export function verify({ changedOnly = false, log = console.log } = {}) {
  const files = listFiles();
  const steps = [...hygieneSteps(changedOnly), ...dotnetSteps(files), ...nodeSteps(files), ...pythonSteps(files)];
  const results = [];
  for (const step of steps) {
    const result = step();
    results.push(result);
    log(`${result.ok ? "PASS" : "FAIL"}  ${result.label}  (${result.seconds}s)`);
    if (!result.ok && /restore|install|solution|pnpm only/.test(result.label)) break;
  }
  const failures = results.filter((r) => !r.ok);
  return { ok: failures.length === 0, results, failures };
}

export function describeFailures(failures) {
  return failures.map((f) => `### ${f.label}\n${tail(f.output)}`).join("\n\n");
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? "").href) {
  const { ok, failures } = verify({ changedOnly: process.argv.includes("--changed") });
  if (!ok) {
    console.error("\n" + describeFailures(failures));
    process.exit(1);
  }
  console.log("verify: all checks passed");
}
