#!/usr/bin/env bash
# Builds a macOS .app bundle and a drag-to-Applications .dmg.
# Usage: tools/release/build-macos.sh [arm64|x64]   (default: arm64)
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$root"

arch="${1:-arm64}"
rid="osx-$arch"
version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -1)"
app_name="NotepadMoreOS"
app="$root/artifacts/$app_name.app"
publish_dir="$root/artifacts/publish-$rid"
dmg="$root/artifacts/${app_name}-${version}-macOS-${arch}.dmg"
iconset="$(mktemp -d)/AppIcon.iconset"

echo "NotepadMoreOS $version — macOS $arch"

dotnet publish src/NotepadMoreOS -c Release -r "$rid" --self-contained true \
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
	<key>CFBundleName</key><string>$app_name</string>
	<key>CFBundleDisplayName</key><string>$app_name</string>
	<key>CFBundleIdentifier</key><string>io.github.notepadmoreos.app</string>
	<key>CFBundleVersion</key><string>$version</string>
	<key>CFBundleShortVersionString</key><string>$version</string>
	<key>CFBundlePackageType</key><string>APPL</string>
	<key>CFBundleExecutable</key><string>$app_name</string>
	<key>CFBundleIconFile</key><string>AppIcon</string>
	<key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
	<key>LSMinimumSystemVersion</key><string>11.0</string>
	<key>LSApplicationCategoryType</key><string>public.app-category.developer-tools</string>
	<key>NSHighResolutionCapable</key><true/>
	<key>NSSupportsAutomaticGraphicsSwitching</key><true/>
	<key>NSPrincipalClass</key><string>NSApplication</string>
	<key>NSHumanReadableCopyright</key><string>MIT License</string>
</dict>
</plist>
PLIST

# Ad-hoc signature: enough for Apple Silicon without a Developer ID certificate.
codesign --force --deep --sign - "$app"
codesign --verify --deep --strict "$app"

rm -f "$dmg"
create_dmg_fallback() {
  local staging
  staging="$(mktemp -d)"
  cp -R "$app" "$staging/"
  ln -s /Applications "$staging/Applications"
  hdiutil create -volname "$app_name" -srcfolder "$staging" -ov -format UDZO "$dmg"
  rm -rf "$staging"
}

if command -v create-dmg >/dev/null 2>&1; then
  # create-dmg drives Finder through AppleScript; it can time out in automation,
  # so a plain hdiutil image is used whenever it fails.
  if ! create-dmg \
    --volname "$app_name" \
    --volicon "$app/Contents/Resources/AppIcon.icns" \
    --window-pos 200 120 \
    --window-size 620 400 \
    --icon-size 110 \
    --icon "$app_name.app" 160 190 \
    --hide-extension "$app_name.app" \
    --app-drop-link 460 190 \
    --no-internet-enable \
    "$dmg" "$app"; then
    echo "create-dmg failed; falling back to hdiutil."
    rm -f "$root"/artifacts/rw.*.dmg
    create_dmg_fallback
  fi
else
  create_dmg_fallback
fi

codesign --force --sign - "$dmg" 2>/dev/null || true

echo "DMG: $dmg ($(du -h "$dmg" | cut -f1))"
