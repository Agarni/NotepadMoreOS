#!/usr/bin/env bash
# Builds distributable packages: a signed .app for macOS and a single .exe for Windows.
# Usage: tools/release/publish.sh [--skip-macos] [--skip-windows]
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$root"

version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -1)"
out="$root/artifacts"
macos_rid="osx-arm64"
windows_rid="win-x64"

build_macos() {
  local publish_dir="$out/macos-$macos_rid"
  local app="$out/NotepadMoreOS.app"
  local iconset
  iconset="$(mktemp -d)/AppIcon.iconset"

  dotnet publish src/NotepadMoreOS -c Release -r "$macos_rid" --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$publish_dir"

  rm -rf "$app"
  mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
  cp "$publish_dir/NotepadMoreOS" "$app/Contents/MacOS/NotepadMoreOS"
  chmod +x "$app/Contents/MacOS/NotepadMoreOS"

  mkdir -p "$iconset"
  for size in 16 32 128 256 512; do
    sips -z "$size" "$size" src/NotepadMoreOS/Assets/appicon.png --out "$iconset/icon_${size}x${size}.png" >/dev/null
    sips -z "$((size * 2))" "$((size * 2))" src/NotepadMoreOS/Assets/appicon.png \
      --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
  done
  iconutil -c icns "$iconset" -o "$app/Contents/Resources/AppIcon.icns"

  cat > "$app/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>CFBundleName</key><string>NotepadMoreOS</string>
	<key>CFBundleDisplayName</key><string>NotepadMoreOS</string>
	<key>CFBundleIdentifier</key><string>io.github.notepadmoreos.app</string>
	<key>CFBundleVersion</key><string>$version</string>
	<key>CFBundleShortVersionString</key><string>$version</string>
	<key>CFBundlePackageType</key><string>APPL</string>
	<key>CFBundleExecutable</key><string>NotepadMoreOS</string>
	<key>CFBundleIconFile</key><string>AppIcon</string>
	<key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
	<key>LSMinimumSystemVersion</key><string>11.0</string>
	<key>LSApplicationCategoryType</key><string>public.app-category.developer-tools</string>
	<key>NSHighResolutionCapable</key><true/>
	<key>NSSupportsAutomaticGraphicsSwitching</key><true/>
	<key>NSPrincipalClass</key><string>NSApplication</string>
</dict>
</plist>
PLIST

  # Ad-hoc signature keeps Apple Silicon happy without a Developer ID certificate.
  codesign --force --deep --sign - "$app"
  codesign --verify --deep --strict "$app"

  rm -f "$out/NotepadMoreOS-$version-macOS-arm64.zip"
  ditto -c -k --sequesterRsrc --keepParent "$app" "$out/NotepadMoreOS-$version-macOS-arm64.zip"
  echo "macOS package: $out/NotepadMoreOS-$version-macOS-arm64.zip"
}

build_windows() {
  local publish_dir="$out/windows-$windows_rid"
  dotnet publish src/NotepadMoreOS -c Release -r "$windows_rid" --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$publish_dir"

  rm -f "$publish_dir"/*.pdb
  rm -f "$out/NotepadMoreOS-$version-windows-x64.zip"
  zip -q -j "$out/NotepadMoreOS-$version-windows-x64.zip" "$publish_dir/NotepadMoreOS.exe"
  echo "Windows package: $out/NotepadMoreOS-$version-windows-x64.zip"
}

for argument in "$@"; do
  case "$argument" in
    --skip-macos) skip_macos=1 ;;
    --skip-windows) skip_windows=1 ;;
    *) echo "Unknown option: $argument" >&2; exit 1 ;;
  esac
done

[[ -z "${skip_macos:-}" ]] && build_macos
[[ -z "${skip_windows:-}" ]] && build_windows

echo "Done (version $version)."
