#!/usr/bin/env bash
# Builds the Windows package and, when Docker is available, the Inno Setup installer.
# Usage: tools/release/build-windows.sh
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$root"

version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -1)"
publish_dir="$root/artifacts/windows-win-x64"
installer="$root/artifacts/NotepadMoreOS-${version}-windows-x64-setup.exe"
zip="$root/artifacts/NotepadMoreOS-${version}-windows-x64.zip"

echo "NotepadMoreOS $version — Windows x64"

dotnet publish src/NotepadMoreOS -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -o "$publish_dir"

rm -f "$publish_dir"/*.pdb

built_installer=0
if command -v docker >/dev/null 2>&1 && docker info >/dev/null 2>&1; then
  echo "Building installer with Inno Setup (Docker)..."
  rm -f "$installer"
  if docker run --rm -v "$root:/work" amake/innosetup \
      /work/tools/release/setup.iss "/DMyAppVersion=$version"; then
    if [ -f "$installer" ]; then
      chmod 644 "$installer" 2>/dev/null || true
      built_installer=1
    fi
  fi
fi

if [ "$built_installer" -eq 1 ]; then
  echo "Installer: $installer ($(du -h "$installer" | cut -f1))"
else
  echo "Docker/Inno Setup unavailable; packing a portable zip instead."
  rm -f "$zip"
  zip -q -j "$zip" "$publish_dir/NotepadMoreOS.exe"
  echo "Portable: $zip ($(du -h "$zip" | cut -f1))"
fi
