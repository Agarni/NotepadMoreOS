#!/usr/bin/env bash
# Builds the Linux packages: portable .tar.gz and a Debian/Ubuntu .deb.
# Usage: tools/release/build-linux.sh [x64|arm64]   (default: x64)
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$root"

arch="${1:-x64}"
case "$arch" in
  x64) rid="linux-x64"; deb_arch="amd64" ;;
  arm64) rid="linux-arm64"; deb_arch="arm64" ;;
  *) echo "Usage: $0 [x64|arm64]" >&2; exit 1 ;;
esac

version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -1)"
publish_dir="$root/artifacts/publish-$rid"
portable="$root/artifacts/NotepadMoreOS-${version}-linux-${arch}.tar.gz"
deb="$root/artifacts/NotepadMoreOS-${version}-linux-${deb_arch}.deb"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

echo "NotepadMoreOS $version — Linux $arch"

dotnet publish src/NotepadMoreOS -c Release -r "$rid" --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:InvariantGlobalization=true -o "$publish_dir"
rm -f "$publish_dir"/*.pdb

# Portable tarball: run ./NotepadMoreOS/NotepadMoreOS
portable_dir="$work/portable/NotepadMoreOS"
mkdir -p "$portable_dir"
cp "$publish_dir/NotepadMoreOS" "$portable_dir/"
chmod +x "$portable_dir/NotepadMoreOS"
cp LICENSE "$portable_dir/LICENSE"
cp README.md "$portable_dir/README.md"
cp src/NotepadMoreOS/Assets/appicon.png "$portable_dir/appicon.png"
cp tools/release/notepadmoreos.desktop "$portable_dir/notepadmoreos.desktop"
rm -f "$portable"
tar -czf "$portable" -C "$work/portable" NotepadMoreOS

# Debian package
deb_root="$root/artifacts/.deb-staging"
rm -rf "$deb_root"
mkdir -p "$deb_root/DEBIAN" \
         "$deb_root/opt/notepadmoreos" \
         "$deb_root/usr/bin" \
         "$deb_root/usr/share/applications" \
         "$deb_root/usr/share/icons/hicolor/512x512/apps" \
         "$deb_root/usr/share/doc/notepadmoreos"

cp "$publish_dir/NotepadMoreOS" "$deb_root/opt/notepadmoreos/NotepadMoreOS"
chmod 755 "$deb_root/opt/notepadmoreos/NotepadMoreOS"
cp LICENSE "$deb_root/usr/share/doc/notepadmoreos/copyright"
cp README.md "$deb_root/usr/share/doc/notepadmoreos/README.md"
cp tools/release/notepadmoreos.desktop "$deb_root/usr/share/applications/notepadmoreos.desktop"
cp src/NotepadMoreOS/Assets/appicon.png "$deb_root/usr/share/icons/hicolor/512x512/apps/notepadmoreos.png"
ln -s /opt/notepadmoreos/NotepadMoreOS "$deb_root/usr/bin/notepadmoreos"

installed_size="$(du -sk "$deb_root" | cut -f1)"

cat > "$deb_root/DEBIAN/control" <<CONTROL
Package: notepadmoreos
Version: $version
Section: editors
Priority: optional
Architecture: $deb_arch
Maintainer: NotepadMoreOS contributors <notepadmoreos@users.noreply.github.com>
Installed-Size: $installed_size
Depends: libx11-6, libice6, libsm6, libfontconfig1, libfreetype6, libxrandr2, libxi6, libxcursor1, libgl1, libglib2.0-0
Homepage: https://github.com/Agarni/NotepadMoreOS
Description: Text and source code editor inspired by Notepad++
 NotepadMoreOS is a cross-platform editor with tabs, TextMate syntax
 highlighting, find and replace, folder search, encoding conversion and
 named sessions. It is inspired by Notepad++ and runs on Linux, macOS and
 Windows.
CONTROL

cat > "$deb_root/DEBIAN/postinst" <<'POSTINST'
#!/bin/sh
set -e
update-desktop-database -q >/dev/null 2>&1 || true
gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor >/dev/null 2>&1 || true
POSTINST
cat > "$deb_root/DEBIAN/prerm" <<'PRERM'
#!/bin/sh
set -e
update-desktop-database -q >/dev/null 2>&1 || true
PRERM
chmod 755 "$deb_root/DEBIAN/postinst" "$deb_root/DEBIAN/prerm"

# Build the package with dpkg-deb when available (Linux, or Docker); the manual
# ar archive is only a last resort because macOS ar writes an empty archive.
chmod -R go-w "$deb_root"
chmod 755 "$deb_root/opt/notepadmoreos/NotepadMoreOS" "$deb_root/DEBIAN/postinst" "$deb_root/DEBIAN/prerm"
rm -f "$deb"

if command -v dpkg-deb >/dev/null 2>&1; then
  dpkg-deb --build --root-owner-group "$deb_root" "$deb"
elif command -v docker >/dev/null 2>&1 && docker info >/dev/null 2>&1; then
  docker run --rm -v "$root:/work" debian:stable \
    dpkg-deb --build --root-owner-group "/work/artifacts/.deb-staging" "/work/artifacts/$(basename "$deb")"
else
  echo "dpkg-deb and Docker are unavailable; building the ar archive manually."
  (cd "$deb_root" && tar --owner=0 --group=0 -czf "$work/data.tar.gz" .)
  (cd "$deb_root/DEBIAN" && tar --owner=0 --group=0 -czf "$work/control.tar.gz" .)
  printf '2.0\n' > "$work/debian-binary"
  rm -rf "$work/deb-build" && mkdir -p "$work/deb-build"
  cp "$work/debian-binary" "$work/control.tar.gz" "$work/data.tar.gz" "$work/deb-build/"
  (cd "$work/deb-build" && ar -q -c "$deb" debian-binary control.tar.gz data.tar.gz >/dev/null 2>&1 || \
    (ar qc "$deb" debian-binary control.tar.gz data.tar.gz))
fi

rm -rf "$deb_root"

if [ ! -s "$deb" ]; then
  echo "Failed to build the Debian package." >&2
  exit 1
fi

echo "Portable: $portable ($(du -h "$portable" | cut -f1))"
echo "Debian:   $deb ($(du -h "$deb" | cut -f1))"
