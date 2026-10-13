# x64 MSI 安装包

GitHub Actions 每个版本提供 2 个 x64 中文 MSI 安装包（自包含 `AstralParty.Toys-<tag>-win-x64-self-contained-installer.msi` 与框架依赖 `AstralParty.Toys-<tag>-win-x64-framework-dependent-installer.msi`），以及 4 个免安装便携/单文件附件。带 `-` 后缀的 tag（例如 `v1.0.1-beta`）发布为 GitHub prerelease。

安装向导使用简体中文界面与专属主题图，支持选择安装目录，并分别勾选开始菜单和桌面快捷方式。默认按当前用户安装到 `%LocalAppData%\Programs\AstralParty.Toys`，默认添加开始菜单、不添加桌面快捷方式。自选目录需要当前用户具有写权限。自包含版本已自带 .NET 8，框架依赖版本需已安装 64 位 .NET 8 Desktop Runtime；两者均需要 WebView2 Runtime。

安装目录和快捷方式偏好写入 `HKCU\Software\AstralParty.Toys\Installer`，供后续 MSI 升级读取。卸载只清理安装程序管理的文件、快捷方式和安装注册信息，文档中的配置、回放以及游戏中的加载器不受影响。

## 本地构建

在仓库根目录运行（PowerShell 7）：

```powershell
dotnet tool install --global wix --version 5.0.2
wix extension add -g WixToolset.UI.wixext/5.0.2
dotnet publish src/AstralParty.Toys/AstralParty.Toys.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o .artifacts/msi-publish
./installer/Build-Msi.ps1 -PublishDirectory .artifacts/msi-publish -OutputPath .artifacts/AstralParty.Toys.msi -ProductVersion 1.0.1
```

`Build-Msi.ps1` 将发布目录全部文件编入 MSI，自动合成向导立绘与顶部横幅位图，并随附仓库的 AGPL 协议。构建输出目录中的 `msi-build` 是中间产物，不应作为发布附件。

无人值守安装可使用 MSI 公共属性（快捷方式 `1` 为开启，空值为关闭）：

```powershell
msiexec /i AstralParty.Toys.msi /qn INSTALLFOLDER="D:\Apps\AstralParty.Toys" STARTMENU_SHORTCUT=1 DESKTOP_SHORTCUT=""
```

MSI 数字版本来自项目的 `Version`，不使用 tag 中的 beta 后缀。`1.0.1-beta` 的程序与 MSI 均为 `1.0.1`。固定 UpgradeCode 并启用 `AllowSameVersionUpgrades`，用于识别旧版或同版本重复构建并原地覆盖升级，同时阻止降级。

## WinGet 源更新（`Hiynet.AstralParty.Toys`）

清单存档位于 `installer/winget/manifests/h/Hiynet/AstralParty/Toys/`。首次入库 PR 合并后，每次 GitHub Release 发布完成可执行以下命令同步更新 `microsoft/winget-pkgs`：

```powershell
wingetcreate update Hiynet.AstralParty.Toys --version X.Y.Z --urls https://github.com/higashitaniyume/AstralParty.Toys/releases/download/<tag>/AstralParty.Toys-<tag>-win-x64-self-contained-installer.msi --submit --token $(gh auth token)
```

