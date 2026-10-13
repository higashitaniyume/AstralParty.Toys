# Inno Setup x64 installers

Releases provide two English Inno Setup installers (`...-win-x64-self-contained-installer.exe` and `...-win-x64-framework-dependent-installer.exe`) plus two portable ZIPs and two single-file EXEs. The self-contained installer includes .NET 8; the framework-dependent installer requires the x64 .NET 8 Desktop Runtime. Both require WebView2.

## Installation and upgrades

Setup installs for the current user without requesting administrator privileges by default (`PrivilegesRequired=lowest`). The default folder is `%LocalAppData%\Programs\AstralParty.Toys`; users can select another writable folder. The interactive directory page probes file creation, writing, and deletion. Protected folders require choosing another folder or explicitly launching Setup with suitable permissions.

The Start menu shortcut is selected by default; the desktop shortcut is not. Both are independently selectable. Inno upgrades remember the folder and tasks. Disabling a previously selected task removes only the corresponding Toys shortcut. Downgrades are blocked. The wizard language is English regardless of the Windows display language.

The fixed AppId is `{8E9C6C61-5A60-4EF0-AD84-8F59E877A663}`. No legacy installer detection, migration, or removal is performed.

## Uninstall

The generated uninstaller displays an English warning before interactive removal, including when launched using its registered uninstall command from Windows Settings or Control Panel:

> Uninstalling AstralParty.Toys will NOT remove CesiumLoader (the Mod loader) or mods from your game folder. To remove the Mod loader, cancel now and remove it from the Mods page in Toys first. Your settings, replays, and skins will also be kept.

The default response is cancellation. Silent uninstall skips interaction. The uninstaller only removes installed files and its shortcuts; it does not search or modify game folders or delete `Documents\AstralPartyReplays`. Avoid wildcard recursive uninstall deletion rules.

## Local build (PowerShell 7)

Install Inno Setup 6 (CI uses the Windows runner's compiler, or installs 6.6.1 if absent), then run from the repository root:

```powershell
dotnet publish src/AstralParty.Toys/AstralParty.Toys.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o .artifacts/inno-publish
./installer/Build-Installer.ps1 -PublishDirectory .artifacts/inno-publish -OutputPath .artifacts/AstralParty.Toys-installer.exe -ProductVersion 1.1.0
```

Without `-ProductVersion`, the build script reads the application project's `Version`, so future releases do not depend on a hardcoded installer version. Pass `-CompilerPath <path-to-ISCC.exe>` if the compiler is not in PATH or a standard Inno Setup 6 directory. The script packages the published tree and repository license, and checks the compiler exit status and expected output. The license is displayed and installed as `LICENSE.txt`. Do not publish the compiler or temporary build inputs.

```powershell
# Fresh installation / upgrade, without UAC for writable folders:
.\AstralParty.Toys-installer.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR="D:\Apps\AstralParty.Toys" /TASKS="startmenuicon,desktopicon" /LOG="install.log"
# No shortcuts: /TASKS=""
# Silent uninstall:
& 'D:\Apps\AstralParty.Toys\unins000.exe' /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG="uninstall.log"
```

## WinGet

Existing manifests under `winget/manifests/` remain historical records. The next version needs a new manifest using `InstallerType: inno`, `Scope: user`, the EXE URL/hash, and uninstall identity `{8E9C6C61-5A60-4EF0-AD84-8F59E877A663}_is1`. Validate WinGet detection before submitting a new manifest. Building this repository does not publish assets or submit a WinGet PR.
