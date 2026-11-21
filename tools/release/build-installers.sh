#!/usr/bin/env bash
# Builds both installers (DMG for macOS, setup.exe for Windows) in artifacts/.
# Usage: tools/release/build-installers.sh [--macos-only|--windows-only]
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$root"

case "${1:-}" in
  --macos-only) tools/release/build-macos.sh ;;
  --windows-only) tools/release/build-windows.sh ;;
  --linux-only) tools/release/build-linux.sh ;;
  "")
    tools/release/build-macos.sh
    tools/release/build-windows.sh
    tools/release/build-linux.sh
    ;;
  *)
    echo "Unknown option: $1" >&2
    exit 1
    ;;
esac

echo "Artifacts:"
ls -lh artifacts | sed -n '2,20p'
