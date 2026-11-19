# Native Linux toolbox (experimental)

Use this version to inspect your Astral Party installation, manage CesiumLoader, or launch the game from Steam. The toolbox is a native GTK/WebKitGTK window (Tauri 2) using the same HTML, CSS and JavaScript as the Windows application. **The game still uses Proton.** The toolbox does not need its own Wine prefix, WebView2, a Windows .NET runtime, or a game process to attach to.

The native overview is not the complete Windows toolbox. For replay editing, skin editors, the marketplace, mod configuration and other Windows-only pages, use the [Windows toolbox under Proton](linux-proton.md). Both toolboxes can be kept on the same computer. Do not use both to modify the game at the same time.

## Build and run

This build targets x86-64 Linux with a desktop session, GTK 3, WebKitGTK 4.1, `xdg-open`, and native Steam. Flatpak and Snap Steam, ARM, TapTap, and non-Steam installations are not supported. Use native `protontricks` for loader management. Overview and normal Steam launch do not require it.

Install Rust/Cargo (1.90 or later), the .NET 8 SDK, a C toolchain, `pkg-config`, GTK 3 and WebKitGTK 4.1 development files, `curl`, `jq`, `unzip`, GNU coreutils and `tar`. On Arch Linux the relevant package names include `base-devel`, `rust`, `dotnet-sdk-8.0`, `gtk3`, `webkit2gtk-4.1`, `jq`, `unzip`, `xdg-utils`, and `protontricks`. An existing rustup toolchain works in place of the `rust` package. On Debian-family distributions the corresponding WebKit development package is `libwebkit2gtk-4.1-dev`.

From the repository root:

```sh
bash linux/build-linux.sh
mkdir -p "$HOME/.local/opt"
tar -xzf linux/target/AstralParty.Toys-linux-x64.tar.gz -C "$HOME/.local/opt"
"$HOME/.local/opt/AstralParty.Toys-linux-x64/astralparty-toys-linux"
```

Use an empty destination for a new build. Keep `managed/` beside the executable: it contains the self-contained Linux .NET host and is not a separate application. The archive is built locally; it is not one of the Windows release assets. The GUI needs the system GTK/WebKit libraries even though the .NET host is self-contained. For another distribution, build on that distribution or one with a compatible glibc.

The build downloads the pinned [CesiumLoader release](https://github.com/higashitaniyume/CesiumLoader/releases/tag/modloader-3.8.3) and verifies its SHA-256 against [`linux/loader-release.json`](../linux/loader-release.json) before embedding it. To reuse an already downloaded ZIP, export `CESIUM_LOADER_ARCHIVE` with its absolute path. The installed toolbox does not download a newer loader behind your back.

Optional application menu entry, assuming `~/.local/bin` is on your desktop session's `PATH`:

```sh
bundle="$HOME/.local/opt/AstralParty.Toys-linux-x64"
id=com.github.higashitaniyume.astralparty-toys-linux
mkdir -p "$HOME/.local/bin"
ln -s "$bundle/astralparty-toys-linux" "$HOME/.local/bin/astralparty-toys-linux"
install -Dm644 "$bundle/share/applications/$id.desktop" "$HOME/.local/share/applications/$id.desktop"
install -Dm644 "$bundle/share/icons/hicolor/256x256/apps/$id.png" "$HOME/.local/share/icons/hicolor/256x256/apps/$id.png"
```

## Use the overview

1. Install the game in native Steam (app ID `2622000`), launch it normally once, then exit. The toolbox reads Steam's registered libraries and app manifest; the library and game directory names need not match the default. The game must be installed locally.
2. Select the detected game edition. The selector chooses **which directory is inspected or modified**. The Steam button opens `steam://rungameid/2622000` and honors Steam's launch choice; it does not force the edition in the selector or bypass Steam.
3. Check the installed loader version and the number of enabled mods. These are disk settings, not proof of successful loading. Refresh after the game opens and check the newest log's timestamp and `ok` / `failed` summary. Without a log, the runtime result is unknown.
4. To install, repair, update or remove the loader, **exit the game, refresh, check the selected directory, then confirm**. The supported package is **3.8.3**, not necessarily the newest upstream release. A newer installation is not downgraded. Unknown `version.dll` files are not replaced. If detection cannot establish a stopped game or Steam is updating it, wait rather than forcing the change.

The toolbox installs the loader beside the selected Windows game executable. It uses native `protontricks` only for the executable-specific `version` override in that game's Proton prefix, never a global DLL override or Steam launch option. A missing prefix or disagreement about the selected Proton runtime is a reason to stop: select a compatibility tool in the game's Steam properties, launch and exit once, and refresh. Do not point the toolbox at a different prefix or edit `user.reg` while Wine is running.

The native manager recognizes the unmodified published inventories for 3.8.1, 3.8.2 and the bundled 3.8.3. An unknown version or edited inventory is inspection-only: do not replace the manifest manually to force an upgrade.

Updates retain mod enablement and player settings. Uninstall removes loader files matching the installed inventory, not arbitrary files from the game directory. Modified settings, third-party mods and logs can remain after removal. A pre-existing or changed DLL override is not removed. Uninstalling the desktop toolbox is separate: first use its **Uninstall loader** action if you want to undo the game integration, then remove the native app directory and any optional menu entry, icon and launcher symlink. Do not delete the game's Steam `compatdata/2622000` or save files.

### Tested on Arch Linux

Using native Steam, Proton Experimental and the Chinese edition, clicking **从 Steam 启动** in the installed native window created the game process and a new CesiumLoader 3.8.3 log: `23 ok · 0 failed · 0 not loaded`. If the game stops at an official resource update, complete it in the game and relaunch normally; a log that ends in `AstralParty.Runtime timed out` is **not** a successful mod load. Installation, upgrade and removal were tested only on disposable game directories and a separate Proton prefix.
