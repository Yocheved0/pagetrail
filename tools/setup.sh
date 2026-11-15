#!/bin/sh
set -e
cd "$(git rev-parse --show-toplevel)"
for tool in git gh node pnpm dotnet docker; do
  if command -v "$tool" >/dev/null 2>&1; then echo "  OK   $tool"; else echo "  MISS $tool"; fi
done
dotnet --list-sdks 2>/dev/null | grep -q '^10\.' || echo "  MISS .NET 10 SDK"
git config core.hooksPath .githooks
chmod +x .githooks/* 2>/dev/null || true
echo "Git hooks enabled (.githooks)."
settings="$HOME/.claude/settings.json"
mkdir -p "$HOME/.claude"
[ -f "$settings" ] || echo '{}' > "$settings"
node -e 'const fs=require("fs");const p=process.argv[1];const s=JSON.parse(fs.readFileSync(p,"utf8"));s.attribution={commit:"",pr:""};fs.writeFileSync(p,JSON.stringify(s,null,2)+"\n")' "$settings"
echo "User-level Claude Code attribution disabled in $settings."
node tools/verify.mjs
