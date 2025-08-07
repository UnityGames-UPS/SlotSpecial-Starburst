#!/bin/bash

set -euo pipefail
shopt -s nullglob

DRY_RUN=false

# Reserved names in Windows (case-insensitive)
RESERVED_NAMES=(
  "CON" "PRN" "AUX" "NUL"
  "COM1" "COM2" "COM3" "COM4" "COM5" "COM6" "COM7" "COM8" "COM9"
  "LPT1" "LPT2" "LPT3" "LPT4" "LPT5" "LPT6" "LPT7" "LPT8" "LPT9"
)

function is_reserved_name() {
  local name_upper
  name_upper=$(echo "$1" | awk '{print toupper($0)}')
  for r in "${RESERVED_NAMES[@]}"; do
    if [[ "$name_upper" == "$r" ]]; then
      return 0
    fi
  done
  return 1
}

function sanitize_name() {
  local name="$1"

  # Replace spaces with underscores
  name="${name// /_}"

  # Remove trailing dots and spaces
  name=$(echo "$name" | sed 's/[ .]*$//')

  # Remove forbidden Windows characters
  name=$(echo "$name" | sed 's|[<>:"/\\|?*]||g')

  # Remove control characters
  name=$(echo "$name" | tr -cd '\11\12\15\40-\176')

  # Reserved name check
  local base="${name%%.*}"
  if is_reserved_name "$base"; then
    name="_$name"
  fi

  echo "$name"
}

function usage() {
  echo "Usage: $0 [--dry-run]"
  exit 1
}

if [[ "${1:-}" == "--dry-run" ]]; then
  DRY_RUN=true
fi

echo "🔍 Scanning for Windows-incompatible filenames..."

# Traverse all files and directories (depth-first)
find . -depth ! -path './.git/*' -print0 | while IFS= read -r -d '' path; do
  dir=$(dirname "$path")
  base=$(basename "$path")
  ext=""
  sanitized=""

  # Split filename and extension if it's a file
  if [[ -f "$path" && "$base" == *.* ]]; then
    ext=".${base##*.}"
    base="${base%.*}"
  fi

  sanitized_base=$(sanitize_name "$base")
  sanitized_name="$sanitized_base$ext"

  # Skip unchanged
  if [[ "$sanitized_name" == "$(basename "$path")" ]]; then
    continue
  fi

  new_path="$dir/$sanitized_name"
  suffix=1

  # Prevent overwriting existing files
  while [[ -e "$new_path" && "$new_path" != "$path" ]]; do
    new_path="$dir/${sanitized_base}_$suffix$ext"
    ((suffix++))
  done

  # If sanitized name is empty, skip
  if [[ -z "$sanitized_name" ]]; then
    echo "⚠️  Skipping '$path' (sanitized name is empty)"
    continue
  fi

  # Rename with git mv if tracked, fallback to mv
  if [[ "$DRY_RUN" == true ]]; then
    echo "💡 Dry-run: '$path' -> '$new_path'"
  else
    echo "🔁 Renaming: '$path' -> '$new_path'"
    if git ls-files --error-unmatch "$path" >/dev/null 2>&1; then
      git mv -f "$path" "$new_path"
    else
      mv -f "$path" "$new_path"
    fi
  fi
done

echo "✅ Done. ${DRY_RUN:+(Dry run)} Review changes with 'git status'."

