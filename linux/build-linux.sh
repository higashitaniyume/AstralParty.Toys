#!/usr/bin/env bash
# Build a relocatable GUI plus its private .NET host. Never run the game or Wine here.
set -euo pipefail

cd "$(dirname "$0")"
if [[ "$(uname -sm)" != 'Linux x86_64' ]]; then
    printf 'This build targets native x86_64 Linux.\n' >&2
    exit 1
fi
mkdir -p target
work=$(mktemp -d "$PWD/target/package.XXXXXXXX")
trap 'rm -rf -- "$work"' EXIT
version=$(jq -er '.version' loader-release.json)
digest=$(jq -er '.sha256' loader-release.json)
zip=${CESIUM_LOADER_ARCHIVE:-"$work/cesium-loader.zip"}
if [[ -z ${CESIUM_LOADER_ARCHIVE:-} ]]; then
    curl --fail --location --show-error --silent \
        "https://github.com/higashitaniyume/CesiumLoader/releases/download/modloader-$version/cesium-loader-$version.zip" -o "$zip"
fi
zip=$(realpath -- "$zip")
printf '%s  %s\n' "$digest" "$zip" | sha256sum --check --status || { echo 'Unexpected CesiumLoader archive checksum.' >&2; exit 1; }

bundle="$work/AstralParty.Toys-linux-x64"
dotnet publish ../src/AstralParty.Toys.LinuxHost -c Release -r linux-x64 --self-contained true \
    -p:CesiumLoaderArchive="$zip" -p:DebugType=None -o "$bundle/managed" --nologo
cargo build --release --locked --target-dir target
install -m 755 target/release/astralparty-toys-linux "$bundle/"
install -m 644 ../LICENSE "$bundle/LICENSE"
desktop_id=com.github.higashitaniyume.astralparty-toys-linux
install -Dm644 "$desktop_id.desktop" "$bundle/share/applications/$desktop_id.desktop"
install -Dm644 ../src/AstralParty.Toys/WebUI/images/astralparty-game-icon.png \
    "$bundle/share/icons/hicolor/256x256/apps/$desktop_id.png"
unzip -p "$zip" CesiumLoader-LICENSE.txt > "$bundle/CesiumLoader-LICENSE.txt"
unzip -p "$zip" CesiumLoader-NOTICE.txt > "$bundle/CesiumLoader-NOTICE.txt"
tar -C "$work" -czf "$work/bundle.tar.gz" AstralParty.Toys-linux-x64
mv "$work/bundle.tar.gz" target/AstralParty.Toys-linux-x64.tar.gz
printf '%s\n' "$PWD/target/AstralParty.Toys-linux-x64.tar.gz"
