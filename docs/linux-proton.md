# Experimental Linux deployment with Proton

AstralParty.Toys is a Windows application built with WPF and WebView2. This guide
runs the Windows release through Proton; it does **not** add native Linux support
or make Linux an officially supported platform.

The recipe below reproduces a community-tested installation. Other distributions,
Proton builds, release versions, and Steam packaging formats may behave differently.

## Tested scope and prerequisites

- x86_64 Arch Linux with the native Steam client, niri, and xwayland-satellite.
- AstralParty.Toys **v2.3.3**, `win-x64-self-contained-portable.zip`.
- CesiumLoader **3.8.3** and the Chinese Steam build of Astral Party, app ID
  **2622000** (`AstralParty_CN.exe`).
- Steam's **Proton - Experimental** and `SteamLinuxRuntime_4`.
- Microsoft Edge WebView2 Evergreen **157.0.4341.60** in the toolbox prefix.
- Bash, `curl`, and `unzip`; `desktop-file-validate` is useful for the optional
  application-menu entry.

The observed result was a toolbox-button launch through Steam, both applications
running together, and a new loader report with 23 successful mods and zero failed
mods. This is a startup test, not validation of every replay, editor, multiplayer,
or mod feature. Newer releases, including v2.4.0, have not been validated by this
recipe. Flatpak/Snap Steam, ARM systems, and TapTap are outside its tested scope.

Install the game and the Steam tools above first. Select Proton - Experimental in
the game's Steam compatibility settings, launch the unmodified game once through
Steam, then exit it normally. Keep Steam running during setup.

**Exit the game before installing, updating, removing, or reconfiguring mods.**
Keep a separate backup of game data before using tools that move replays or manage
account profiles. Third-party mods remain subject to the game's rules; this guide
does not establish anti-cheat compatibility.

## 1. Choose paths and unpack the toolbox

These examples use a native Steam installation under `~/.local/share/Steam` and
its default library. Adjust the variables for your installation. The executable's
subdirectory can change: find it with Steam's **Manage > Browse local files**, and
set `game_dir` to the directory containing `AstralParty_CN.exe`, not its parent.

Download the [v2.3.3 self-contained portable release](https://github.com/higashitaniyume/AstralParty.Toys/releases/tag/v2.3.3)
and [CesiumLoader 3.8.3](https://github.com/higashitaniyume/CesiumLoader/releases/tag/modloader-3.8.3).
The commands use `~/Downloads`; substitute your actual download directory if needed.
Use a fresh toolbox directory, not an existing installation with different data.

Run the setup blocks in the **same Bash session**:

```bash
steam_dir="$HOME/.local/share/Steam"
game_library="$steam_dir"
app_dir="$HOME/.local/share/astralparty-toys"
game_dir="$game_library/steamapps/common/Astral Party/8vJXn6CN"
game_compat="$game_library/steamapps/compatdata/2622000"
proton="$steam_dir/steamapps/common/Proton - Experimental/proton"
runtime="$steam_dir/steamapps/common/SteamLinuxRuntime_4/_v2-entry-point"

mkdir -p "$app_dir" "$app_dir/compatdata"
unzip "$HOME/Downloads/AstralParty.Toys-v2.3.3-win-x64-self-contained-portable.zip" \
  -d "$app_dir"
```

`AstralParty.Toys.exe` should now be directly inside `app_dir`. The self-contained
package supplies .NET; it does not supply WebView2.

Use this temporary shell helper for setup commands. It runs Windows utilities
through the same Proton and Steam runtime used by the launcher below:

```bash
run_in_prefix() {
  local compat_dir="$1" install_dir="$2"
  shift 2
  STEAM_COMPAT_CLIENT_INSTALL_PATH="$steam_dir" \
  STEAM_COMPAT_DATA_PATH="$compat_dir" \
  STEAM_COMPAT_INSTALL_PATH="$install_dir" \
  STEAM_COMPAT_APP_ID=0 SteamAppId=0 SteamGameId=0 \
    "$runtime" --verb=run -- "$proton" run "$@"
}
```

## 2. Create an independent prefix and install WebView2

Do **not** run the toolbox in `steamapps/compatdata/2622000`. Steam can wait for
existing processes in that Wine prefix before launching the game, so keeping the
toolbox open there can prevent the game from starting. This recipe instead uses
`$app_dir/compatdata`, leaving Steam's game prefix in place.

Download Microsoft's [WebView2 Evergreen standalone x64 installer](https://developer.microsoft.com/en-us/microsoft-edge/webview2/#download-section)
and install it into the toolbox prefix:

```bash
webview_installer="$HOME/Downloads/MicrosoftEdgeWebView2RuntimeInstallerX64.exe"
curl --http1.1 --fail --location \
  'https://go.microsoft.com/fwlink/p/?LinkId=2124701' \
  --output "$webview_installer"

run_in_prefix "$app_dir/compatdata" "$app_dir" \
  "$webview_installer" /silent /install
```

The download is Evergreen, so its version can differ from the tested version.
HTTP/1.1 was needed for this download in the tested environment; do not disable
certificate verification to work around a failed download.

Retain Proton's default application compatibility settings. Forcing
`msedgewebview2.exe` to Windows 10 produced a blank interface in the tested setup;
restoring Proton's default override allowed WebView2 to render again. Do not apply
a global Windows-version override to the prefix.

## 3. Connect Steam and existing game data

### Steam URL handler

The toolbox uses `steam://rungameid/2622000` for the normal Steam launch mode. The
independent prefix's Windows Steam stub is not the native Linux Steam client.
Register the URI handler **only in the toolbox prefix** so Wine hands it to the
host's browser/URL integration:

```bash
run_in_prefix "$app_dir/compatdata" "$app_dir" reg.exe add \
  'HKCU\Software\Classes\steam' /ve /t REG_SZ /d 'URL:Steam Protocol' /f
run_in_prefix "$app_dir/compatdata" "$app_dir" reg.exe add \
  'HKCU\Software\Classes\steam' /v 'URL Protocol' /t REG_SZ /d '' /f
run_in_prefix "$app_dir/compatdata" "$app_dir" reg.exe add \
  'HKCU\Software\Classes\steam\shell\open\command' /ve /t REG_SZ \
  /d '"C:\windows\system32\winebrowser.exe" "%1"' /f
```

The host must already associate Steam URLs with native Steam. Check with:

```bash
xdg-mime query default x-scheme-handler/steam
```

The tested system returned `steam.desktop`. If no Steam handler is registered,
repair your distribution's Steam desktop integration before testing the button.
This does not require changing Steam's game launch options.

### Existing saves, accounts, and replay cache

A separate prefix otherwise has a separate Windows user-data directory. Expose
only the existing game's `LocalLow/feimo` directory at the location the toolbox
expects, rather than copying saves or sharing the entire prefix:

```bash
game_data="$game_compat/pfx/drive_c/users/steamuser/AppData/LocalLow/feimo"
tools_local_low="$app_dir/compatdata/pfx/drive_c/users/steamuser/AppData/LocalLow"

mkdir -p "$tools_local_low"
ln -s "$game_data" "$tools_local_low/feimo"
```

The source `game_data` must already exist. If the destination `feimo` already
exists, stop and inspect its contents; do not replace or delete existing profiles
to force this command through. This is a fresh-install recipe, not an automatic
migration from another toolbox prefix.

**This link grants the toolbox access to the real game data.** It is not a copy:
profile operations and replay management can affect the game's files. Keep the
game closed during setup and data-management operations. Ordinary toolbox data
remains separate under the toolbox prefix's
`drive_c/users/steamuser/Documents/AstralParty.Toys`.

## 4. Install the loader and its per-game DLL override

For an existing CesiumLoader installation, skip the fresh-install extraction and
use the toolbox's normal loader-management flow with the game fully closed. Do not
overwrite a `version.dll` belonging to another loader.

For the tested fresh installation, extract the loader components next to the game
executable. Keep the archive's directory structure. Move the release manifest into
the loader directory so the toolbox can recognize ownership and version:

```bash
unzip "$HOME/Downloads/cesium-loader-3.8.3.zip" \
  'version.dll' 'AstralParty_ModLoader/*' 'cesium-loader.json' \
  'CesiumLoader-LICENSE.txt' 'CesiumLoader-NOTICE.txt' \
  -d "$game_dir"
mv "$game_dir/cesium-loader.json" \
  "$game_dir/AstralParty_ModLoader/cesium-loader.json"
```

Optional skin assets are distributed separately; see the
[CesiumLoader documentation](https://github.com/higashitaniyume/CesiumLoader).
The base loader archive is sufficient for loader initialization.

Tell Proton to load the native `version.dll` for this executable. This command
belongs to the **game prefix**, unlike the WebView2 and Steam-handler commands:

```bash
run_in_prefix "$game_compat" "$game_dir" reg.exe add \
  'HKCU\Software\Wine\AppDefaults\AstralParty_CN.exe\DllOverrides' \
  /v version /t REG_SZ /d native,builtin /f
```

This is an executable-specific override. It avoids replacing existing Steam
launch options or applying the override to unrelated applications. Leave the
loader's Steam/TapTap bypass settings off, the default account profile selected,
and the base speed at 1.0 for the initial test.

## 5. Add a launcher

Create `~/.local/bin/astralparty-toys`. If you changed `app_dir`, `steam_dir`, or the
Steam tool locations above, adjust the corresponding paths in this script too.

```bash
mkdir -p "$HOME/.local/bin"
cat > "$HOME/.local/bin/astralparty-toys" <<'SH'
#!/usr/bin/env bash
set -euo pipefail

app_dir="$HOME/.local/share/astralparty-toys"
steam_dir="$HOME/.local/share/Steam"
cd "$app_dir"

export STEAM_COMPAT_CLIENT_INSTALL_PATH="$steam_dir"
export STEAM_COMPAT_DATA_PATH="$app_dir/compatdata"
export STEAM_COMPAT_INSTALL_PATH="$app_dir"
export STEAM_COMPAT_APP_ID=0 SteamAppId=0 SteamGameId=0
export WINEDEBUG=-all

# Use Wine's D3D9 for WPF; DXVK turned the window black after tiling.
export WINEDLLOVERRIDES="${WINEDLLOVERRIDES:+${WINEDLLOVERRIDES};}d3d9=b"
exec "$steam_dir/steamapps/common/SteamLinuxRuntime_4/_v2-entry-point" --verb=run -- \
    "$steam_dir/steamapps/common/Proton - Experimental/proton" run \
    "$app_dir/AstralParty.Toys.exe" "$@"
SH
chmod +x "$HOME/.local/bin/astralparty-toys"
```

The `d3d9=b` override is limited to the toolbox process tree. It fixed a black WPF
window after tiling/resizing in the tested environment; it does not change the
game's rendering configuration. Keep `proton run`, rather than replacing it with
`runinprefix` or a different Wine installation.

Optionally add an application-menu entry:

```bash
mkdir -p "$HOME/.local/share/applications"
cat > "$HOME/.local/share/applications/astralparty-toys.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=AstralParty.Toys
Comment=Astral Party replay and mod toolbox (Proton)
Exec="$HOME/.local/bin/astralparty-toys"
Icon=applications-games
Terminal=false
Categories=Game;
EOF
```

If installed, `desktop-file-validate` can check that entry. The shell launcher can
also be run directly as `~/.local/bin/astralparty-toys`.

## 6. Select the game and verify a fresh launch

1. Open the toolbox and add/select the directory containing `AstralParty_CN.exe`.
   Browse through Wine's **Z:** drive to the Linux path in `game_dir`. Do not copy
   an **S:** drive path from the game's prefix: drive mappings can differ between
   prefixes, and automatic Windows Steam-library detection may not find native
   Steam.
2. Confirm the toolbox recognizes CesiumLoader and its version.
3. In the overview, select the normal **Steam** launch mode and click the toolbox's
   **Launch game** button. Do not use either Steam/TapTap bypass mode for this test.
4. Keep the toolbox open. Verify that Steam creates a new game process and the
   actual game window reaches its start screen.
5. Check a **newly timestamped** log for this launch under
   `AstralParty_ModLoader/logs/cesium-loader-*.log`. It should report native
   `version.dll` loading, bypass switches off, and a successful summary for the
   enabled mods. A previous session's successful log does not verify this launch.

An invocation of the Steam URL alone is not proof that the game started. Steam's
`logs/console_log.txt` can distinguish URI receipt, `CreatingProcess`, and
`WaitingGameWindow`; confirm the actual game window as well.

## Known limitations and troubleshooting

- **The toolbox may report the game as stopped while it is running.** Windows
  process enumeration in one Wine prefix does not reliably see the game in the
  other prefix. Do not rely on this status as a safety check: fully exit the game
  before changing loader/mod files or managing game data.
- **X11 monitor/input errors can terminate the toolbox.** `BadRRCrtc` in RANDR
  `RRGetCrtcInfo`, and `XI_BadDevice`, were observed under niri/xwayland-satellite,
  including after long runs. Restart the toolbox after a crash. Include your
  compositor, monitor layout/scaling, and recent display/input changes in reports;
  the exact trigger was not isolated. This recipe is not a permanent fix for those
  failures, and disabling the compositor or GPU globally is not part of it.
- **A blank or black toolbox window is not evidence of loader failure.** Check the
  toolbox-only `d3d9=b` setting and retain Proton's default WebView2 compatibility
  settings. WebView2 must be installed in the toolbox prefix, not just the game
  prefix.
- **The button reaches Steam, but no game window appears.** If Steam stays at
  `CheckShaderDepotManifest` without creating the game process, exit other games
  normally, fully exit Steam, reopen it, and try the toolbox button again. This
  stall was observed in an existing Steam session; a subsequent launch completed
  after restarting Steam. Do not work around it by enabling Steam bypass or
  repeatedly reinstalling the loader.

## Updates and removal

Follow the [normal update instructions](../README.md#安装), preserving the
independent prefix and its toolbox data. A different release is a new compatibility
test; the result above does not certify future Proton, WebView2, or toolbox versions.

To remove the loader, first exit the game and use the toolbox's normal uninstall
flow. See [loader recovery](loader-recovery.md) for the distinction between disabling
and uninstalling it. Remove the executable-specific `version` registry value added
above, or restore its previous value if it existed. The application-menu entry and
shell launcher can be removed separately.

Before deleting the toolbox directory, preserve any wanted replay library and
configuration inside its prefix. Do **not** delete Steam's `compatdata/2622000` or
the original `LocalLow/feimo` directory as part of removing the toolbox. The data link
is not a backup of that directory.
