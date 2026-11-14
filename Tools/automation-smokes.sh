#!/usr/bin/env bash
# Automation smokes S1-S8 of ai-agent/tasks/editor-remote-control-2-tasks.md (T0.2 baseline, T3.1 comparison).
# Usage: Tools/automation-smokes.sh <output-folder>
# Each smoke runs the built editor on a fresh copy of Projects/SampleProject and records, in <output-folder>:
#   sN-automation.txt  [Automation] / [PlaySmoke] diagnostics lines, timestamps and copy paths removed
#   sN-hashes.txt      SHA-256 of every file of the copy after the editor exited, sorted by path
#   sN-exit.txt        editor exit code
set -u
REPO="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$1"
EDITOR="$REPO/CasaEngine.Editor/bin/Debug/net9.0-windows/CasaEngine.Editor.exe"
WORK="$(mktemp -d)"
mkdir -p "$OUT"

run_smoke() {
  local name="$1"
  shift
  local copy="$WORK/$name"
  rm -rf "$copy"
  mkdir -p "$copy"
  cp -r "$REPO/Projects/SampleProject" "$copy/"
  local project="$copy/SampleProject"
  local diagnostics="$copy/diagnostics.txt"
  (cd "$(dirname "$EDITOR")" && timeout 300 "./$(basename "$EDITOR")" --project "$project/SampleProject.json" "$@" --diagnostics-out "$diagnostics" > "$copy/stdout.txt" 2>&1)
  echo $? > "$OUT/$name-exit.txt"
  local winproject
  winproject="$(cygpath -w "$project" 2>/dev/null || echo "$project")"
  grep -h "\[Automation\]\|\[PlaySmoke\]" "$diagnostics" "$copy/stdout.txt" 2>/dev/null \
    | sed -E 's/^[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9:.]+ //' \
    | sed "s#$(printf '%s' "$winproject" | sed 's/[\\/&.]/\\&/g')#<project>#g; s#$(printf '%s' "$project" | sed 's/[\\/&.]/\\&/g')#<project>#g" \
    > "$OUT/$name-automation.txt"
  (cd "$project" && find . -type f ! -path "./.casaeditor/*" -print0 | sort -z | xargs -0 sha256sum) > "$OUT/$name-hashes.txt"
}

run_smoke s1 --entity ground --activate-panel inspector
run_smoke s2 --open-asset NewLitDiffuseMaterial.material --set-material-property alpha_cutoff=0.5
run_smoke s3 --open-asset Particles/FireLoop_Minimal.particle --set-particle-property maxparticles=500 --particle-undo-redo-smoke
run_smoke s4 --create-particle-asset Particles
run_smoke s5 --entity ground --drop-particle-asset Particles/SparkBurst_Additive.particle
run_smoke s6 --open-asset Screens/main-menu.uiscreen --set-screen-property btnPlay:Content=Start
run_smoke s7 --entity ground --save-project
run_smoke s8 --entity ground --play-smoke

rm -rf "$WORK"
echo "done: $OUT"
