#!/usr/bin/env bash
set -euo pipefail

# Run as an unprivileged user, against a built LinuxHost. This script never passes
# a real game directory or touches Steam/Proton. An optional local old archive is
# accepted only if it matches the public release digest below.
if (( $# == 0 || EUID == 0 )); then
  printf 'Usage (non-root): bash %s [dotnet] /path/to/built/LinuxHost[.dll]\n' "$0" >&2
  exit 2
fi
sidecar=("$@")
repo=$(cd -- "$(dirname -- "$0")/.." && pwd -P)
version=$(jq -er .version "$repo/linux/loader-release.json")
work=$(realpath -- "$(mktemp -d)")
trap 'chmod -R u+rwX -- "$work"; rm -rf -- "$work"' EXIT

loader=AstralParty_ModLoader
manifest=$loader/cesium-loader.json
config=$loader/doorstop_config.json
mkdir "$work/data"

make_game() {
  mkdir -p "$1"
  touch "$1/${2:-AstralParty_CN.exe}"
}

apply() {
  local op=$1 game=$2 edition=cn
  if [[ -f "$game/AstralParty_INT.exe" ]]; then edition=global; fi
  jq -cn --arg type "$op" --arg game "$game" --arg data "$work/data" --arg edition "$edition" \
    '{type:$type,gameDirectory:$game,gameDirectories:[{id:$game,directory:$game,edition:$edition}],gameRunning:false,dataDirectory:$data,edition:$edition}' |
    "${sidecar[@]}"
}

success() {
  apply "$@" > "$work/response"
  jq -e 'type == "array" and length > 0' "$work/response" > /dev/null
}

snapshot() (
  cd -- "$1"
  find . -printf '%y %P %l\n' | LC_ALL=C sort
  find . -type f -print0 | LC_ALL=C sort -z | xargs -0 sha256sum
)

unchanged_on_failure() {
  local op=$1 game=$2
  snapshot "$game" > "$work/before"
  if apply "$op" "$game" > "$work/response" 2> "$work/error"; then
    printf '%s unexpectedly succeeded for %s\n' "$op" "$game" >&2
    cat "$work/response" >&2
    exit 1
  fi
  test -s "$work/error"
  snapshot "$game" > "$work/after"
  diff -u "$work/before" "$work/after"
}

fresh=$work/fresh
make_game "$fresh" AstralParty_INT.exe
success linuxInstallLoader "$fresh"
jq -e --arg version "$version" '.version == $version and .repo == "higashitaniyume/CesiumLoader" and .layout == "game-dir"' \
  "$fresh/$manifest" > /dev/null
(
  cd -- "$fresh"
  jq -er '.files | to_entries[] | "\(.value)  \(.key)"' "$manifest" | sha256sum --status -c
)
success linuxUninstallLoader "$fresh"
test ! -e "$fresh/version.dll"
test ! -e "$fresh/$loader"

old=3.8.1
old_zip=${CESIUM_LOADER_OLD_ARCHIVE:-$work/cesium-loader-$old.zip}
if [[ -z ${CESIUM_LOADER_OLD_ARCHIVE:-} ]]; then
  curl --fail --location --silent --show-error --max-time 60 \
    "https://github.com/higashitaniyume/CesiumLoader/releases/download/modloader-$old/cesium-loader-$old.zip" --output "$old_zip"
fi
# https://github.com/higashitaniyume/CesiumLoader/releases/expanded_assets/modloader-3.8.1
printf '%s  %s\n' 13ee63b03b3784abc8a01e8702a27cd4fcd70348f7f137a9fd8ff63ad48edb27 "$old_zip" | sha256sum --status -c
upgrade=$work/upgrade
make_game "$upgrade"
unzip -q "$old_zip" 'AstralParty_ModLoader/*' 'version.dll' 'version.pdb' 'CesiumLoader-*.txt' 'cesium-loader.json' -d "$upgrade"
mv -- "$upgrade/cesium-loader.json" "$upgrade/$manifest"

# Link an edited config to a second pathname to prove atomic replacements do not
# modify the bytes of an external hardlink. Only the top-level SDK value changes.
write_config() {
  printf '\xef\xbb\xbf{\r\n  // preserve this comment and CRLF\r\n  "profile": "custom",\r\n  "sdkVersion": "%s",\r\n  "user": {"sdkVersion":"3.8.1"},\r\n  "speedhackBaseSpeed": 1.25,\r\n}\r\n' "$1"
}
write_config "$old" > "$work/custom-config"
rm -- "$upgrade/$config"
ln -- "$work/custom-config" "$upgrade/$config"
write_config "$old" > "$work/old-config"
write_config "$version" > "$work/updated-config"
mkdir -p "$upgrade/$loader/disabled_mods" "$upgrade/$loader/mods/ThirdParty"
mv -- "$upgrade/$loader/mods/LanguageUnlockMod" "$upgrade/$loader/disabled_mods/"
custom_mod=$loader/disabled_mods/LanguageUnlockMod
printf '{"locale":"custom"}\n' > "$upgrade/$custom_mod/config.json"
printf 'third-party DLL\n' > "$upgrade/$loader/mods/ThirdParty/ThirdParty.dll"
printf 'saved log\n' > "$upgrade/$loader/logs/saved.log"
mod_sidecar=$loader/mods/PvePanelMod/PvePanelMod.json
jq '.enabled = false | .userSetting = 42' "$upgrade/$mod_sidecar" > "$work/user-sidecar"
mv -- "$work/user-sidecar" "$upgrade/$mod_sidecar"

success linuxUpdateLoader "$upgrade"
new_manifest=$upgrade/$manifest
jq -e --arg version "$version" '.version == $version' "$new_manifest" > /dev/null
check_owned_file() {
  local manifest_path=$1 installed_path=${2:-$1}
  printf '%s  %s\n' "$(jq -er --arg path "$manifest_path" '.files[$path]' "$new_manifest")" "$upgrade/$installed_path" |
    sha256sum --status -c
}
check_owned_file version.dll
check_owned_file "$loader/sdk/CesiumLoader.SDK.dll"
check_owned_file "$loader/bootstrap/CesiumLoader.Bootstrap.dll"
check_owned_file "$loader/mods/LanguageUnlockMod/LanguageUnlockMod.dll" "$custom_mod/LanguageUnlockMod.dll"
test ! -e "$upgrade/$loader/mods/LanguageUnlockMod"
cmp -- "$upgrade/$config" "$work/updated-config"
cmp -- "$work/custom-config" "$work/old-config"
jq -e '.enabled == false and .userSetting == 42' "$upgrade/$mod_sidecar" > /dev/null

# A newer installation must not downgrade and a retained-disabled proxy must
# not be silently re-enabled. Alter only metadata of the disposable fixture.
cp -- "$new_manifest" "$work/installed-manifest"
jq '.version = "999.0.0" | .tag = "modloader-999.0.0"' "$work/installed-manifest" > "$new_manifest"
unchanged_on_failure linuxUpdateLoader "$upgrade"
cp -- "$work/installed-manifest" "$new_manifest"

# A matching DLL hash in an edited inventory does not make a third-party mod
# publishable or owned by CesiumLoader.
foreign=$loader/mods/ThirdParty/ThirdParty.dll
foreign_digest=$(sha256sum "$upgrade/$foreign")
jq --arg path "$foreign" --arg digest "${foreign_digest%% *}" '.files[$path] = $digest' "$work/installed-manifest" > "$new_manifest"
unchanged_on_failure linuxUpdateLoader "$upgrade"
unchanged_on_failure linuxUninstallLoader "$upgrade"
grep -q '无法验证已安装的 CesiumLoader 发布清单' "$work/error"
cp -- "$work/installed-manifest" "$new_manifest"

mv -- "$upgrade/version.dll" "$work/owned-version.dll"
success linuxUpdateLoader "$upgrade"
test ! -e "$upgrade/version.dll"
printf 'foreign proxy\n' > "$upgrade/version.dll"
for op in linuxInstallLoader linuxUpdateLoader linuxUninstallLoader; do
  unchanged_on_failure "$op" "$upgrade"
done
mv -- "$work/owned-version.dll" "$upgrade/version.dll"

success linuxUninstallLoader "$upgrade"
test ! -e "$upgrade/version.dll"
test ! -e "$upgrade/$manifest"
test ! -e "$upgrade/$custom_mod/LanguageUnlockMod.dll"
[[ $(cat "$upgrade/$loader/mods/ThirdParty/ThirdParty.dll") == 'third-party DLL' ]]
[[ $(cat "$upgrade/$loader/logs/saved.log") == 'saved log' ]]
jq -e '.locale == "custom"' "$upgrade/$custom_mod/config.json" > /dev/null
jq -e '.enabled == false and .userSetting == 42' "$upgrade/$mod_sidecar" > /dev/null
cmp -- "$upgrade/$config" "$work/updated-config"

conflict=$work/conflict
make_game "$conflict"
mkdir -p "$conflict/$loader/sdk"
printf 'foreign SDK\n' > "$conflict/$loader/sdk/CesiumLoader.SDK.dll"
unchanged_on_failure linuxInstallLoader "$conflict"

linked=$work/linked
make_game "$linked"
mkdir -p "$linked/$loader" "$work/external"
printf 'untouchable\n' > "$work/external/CesiumLoader.SDK.dll"
ln -s -- "$work/external" "$linked/$loader/sdk"
snapshot "$work/external" > "$work/external-before"
unchanged_on_failure linuxInstallLoader "$linked"
snapshot "$work/external" > "$work/external-after"
diff -u "$work/external-before" "$work/external-after"

rollback=$work/rollback
make_game "$rollback"
blocked=$rollback/$loader/mods/ActivityLogMod
mkdir -p -- "$blocked"
chmod 0555 -- "$blocked"
unchanged_on_failure linuxInstallLoader "$rollback"
# This is a commit-time rename error after earlier files were installed, not a
# conflicting-file refusal that stops in preflight before there is anything to undo.
grep -q '无法原子移动加载器文件' "$work/error"

printf 'Isolated CesiumLoader lifecycle checks passed (3.8.1 → %s).\n' "$version"
