use super::{APP_ID, Installation, canonicalize, compat_directory, optional_text, vdf};
use std::{
    env,
    fs::{self, DirBuilder, File},
    io::{self, Write},
    os::unix::{ffi::OsStrExt, fs::{DirBuilderExt, MetadataExt, OpenOptionsExt, PermissionsExt}},
    path::{Path, PathBuf},
    process::{Command, Stdio},
};

pub struct OverrideChange {
    pub added: bool,
    pub warning: Option<String>,
}

const OWNER_DIRECTORY: &str = "steam-overrides";
const OVERRIDE_VALUE: &[u8] = b"\"native,builtin\"";

// Protontricks resolves Steam's registered compatibility tool and prefix. Its own
// fallback/default selection is not sufficient: compare both identities before any
// Wine command. Paths and the allowlisted registry key are arguments via the environment,
// never interpolated into this shell command.
const REGISTRY_COMMAND: &str = r#"
[ "$STEAM_APPID" = 2622000 ] && [ "$STEAM_APP_PATH" -ef "$TOYS_STEAM_GAME" ] &&
    [ "$WINEPREFIX" -ef "$TOYS_STEAM_PREFIX" ] || exit 80
[ "$PROTON_PATH" -ef "$TOYS_STEAM_PROTON" ] &&
    [ "$PROTON_DIST_PATH" -ef "$TOYS_STEAM_DIST" ] || exit 81
case "$TOYS_STEAM_ACTION" in
    verify) exit 0 ;;
    add)
        "$WINE" 'C:\windows\system32\reg.exe' query "$TOYS_REG_KEY" /v version >/dev/null 2>&1
        [ "$?" -eq 1 ] || exit 82
        "$WINE" 'C:\windows\system32\reg.exe' add "$TOYS_REG_KEY" /v version /t REG_SZ /d native,builtin /f || exit 83
        ;;
    remove)
        value=$("$WINE" 'C:\windows\system32\reg.exe' query "$TOYS_REG_KEY" /v version) || exit 82
        printf '%s\n' "$value" | grep -Eq '^[[:space:]]+version[[:space:]]+REG_SZ[[:space:]]+native,builtin[[:space:]]*$' || exit 82
        "$WINE" 'C:\windows\system32\reg.exe' delete "$TOYS_REG_KEY" /v version /f || exit 83
        ;;
    *) exit 84 ;;
esac
# Wait for the normal prefix shutdown/registry flush, never terminate processes.
"$WINESERVER" -w || exit 85
"#;

struct Runtime {
    prefix: PathBuf,
    proton: PathBuf,
    dist: PathBuf,
    mapping: String,
    protontricks: PathBuf,
}

impl Runtime {
    fn resolve(game: &Installation, prefix: PathBuf) -> Result<Self, String> {
        let config = game.steam_root.join("config/config.vdf");
        let source = optional_text(&config)?.ok_or("Steam 的 config.vdf 不可用，无法确认 Proton 配置。")?;
        let mapping = mapped_tool(&source)?;
        let (proton, dist) = recorded_runtime(&game.steam_root, &prefix)?;
        let protontricks = native_protontricks().ok_or("需要原生 protontricks 才能配置此游戏的 Proton DLL 覆盖。")?;
        Ok(Self { prefix, proton, dist, mapping, protontricks })
    }

    fn run(&self, game: &Installation, action: &'static str) -> Result<(), String> {
        let mut command = Command::new(&self.protontricks);
        // Do not let an inherited terminal configuration redirect the tool, prefix,
        // runtime or the Wine binary chosen by Protontricks.
        for (key, _) in env::vars_os() {
            let bytes = key.as_bytes();
            if [b"PROTON".as_slice(), b"WINE", b"STEAM_COMPAT_", b"STEAM_RUNTIME", b"PRESSURE_VESSEL_", b"PYTHON"].iter()
                .any(|prefix| bytes.starts_with(prefix))
                || matches!(bytes, b"BASH_ENV" | b"ENV" | b"LD_PRELOAD" | b"LD_LIBRARY_PATH")
            {
                command.env_remove(key);
            }
        }
        let output = command.args(["--no-background-wineserver", "-c", REGISTRY_COMMAND, APP_ID])
            .env("STEAM_DIR", &game.steam_root)
            .env("TOYS_STEAM_GAME", &game.game_root)
            .env("TOYS_STEAM_PREFIX", &self.prefix)
            .env("TOYS_STEAM_PROTON", &self.proton)
            .env("TOYS_STEAM_DIST", &self.dist)
            .env("TOYS_STEAM_ACTION", action)
            .env("TOYS_REG_KEY", format!(r"HKCU\Software\Wine\AppDefaults\{}\DllOverrides", game.executable.name))
            .current_dir(&game.steam_root)
            .stdin(Stdio::null())
            .output()
            .map_err(|error| format!("无法启动 protontricks：{error}"))?;
        match output.status.code() {
            Some(0) => Ok(()),
            Some(80) => Err("protontricks 识别的游戏目录或前缀与当前选择不一致，未修改 DLL 覆盖。".into()),
            Some(81) => Err(format!("Steam 当前选用的 Proton（{}）与前缀记录的版本不一致。请通过 Steam 正常启动并退出游戏一次。", self.mapping)),
            Some(82) => Err("游戏专用 version DLL 覆盖在操作期间发生变化，未覆盖现有设置。".into()),
            Some(85) => Err("Wine 注册表写入已返回成功，但无法确认前缀已正常退出并保存，请检查当前覆盖。".into()),
            _ => {
                let stderr = String::from_utf8_lossy(&output.stderr);
                let detail = stderr.lines().rev().find(|line| !line.trim().is_empty()).unwrap_or("");
                Err(format!("protontricks 操作失败（{}）：{}", output.status, detail.trim()))
            }
        }
    }
}

fn native_protontricks() -> Option<PathBuf> {
    env::split_paths(&env::var_os("PATH")?).map(|path| path.join("protontricks"))
        .find(|file| fs::metadata(file).is_ok_and(|meta| meta.is_file() && (meta.permissions().mode() & 0o111 != 0)))
        .and_then(|file| fs::canonicalize(file).ok())
}

fn select_prefix(game: &Installation) -> Result<PathBuf, String> {
    if let Some(prefix) = installed_prefix(&game.steamapps)? {
        return Ok(prefix);
    }
    // The prefix can be left on a different registered library after a Steam move.
    // Never select from several possible prefixes by timestamp.
    let mut alternate = None;
    for library in game.libraries.iter().filter(|path| **path != game.steamapps) {
        if let Some(prefix) = installed_prefix(library)? {
            if alternate.as_ref().is_some_and(|path| *path != prefix) {
                return Err("多个 Steam 库中存在此游戏的 Proton 前缀，无法确定应修改哪个前缀。".into());
            }
            alternate = Some(prefix);
        }
    }
    alternate.ok_or_else(|| "还没有此游戏的 Proton 前缀，请先通过 Steam 正常启动并退出游戏一次。".into())
}

fn installed_prefix(steamapps: &Path) -> Result<Option<PathBuf>, String> {
    let path = compat_directory(steamapps).join("pfx");
    let metadata = match fs::symlink_metadata(&path) {
        Ok(metadata) => metadata,
        Err(error) if error.kind() == io::ErrorKind::NotFound => return Ok(None),
        Err(error) => return Err(format!("无法读取 Proton 前缀 {}：{error}", path.display())),
    };
    if !metadata.is_dir() {
        return Err(format!("Proton pfx 不是普通目录：{}", path.display()));
    }
    canonicalize(&path).map(Some)
}

fn mapped_tool(source: &str) -> Result<String, String> {
    const UNKNOWN: &str = "无法从 Steam 的兼容性配置确认 Proton。请在游戏属性中选择 Proton，再正常启动并退出游戏一次。";
    let tree = vdf::parse(source)?;
    let mut mapping = &tree;
    for key in ["InstallConfigStore", "Software", "Valve", "Steam", "CompatToolMapping"] {
        mapping = mapping.object(key)?.ok_or(UNKNOWN)?;
    }
    for key in [APP_ID, "0"] {
        if let Some(app) = mapping.object(key)? {
            if let Some(name) = app.text("name")?.filter(|name| !name.trim().is_empty()) {
                return Ok(name.to_owned());
            }
        }
    }
    Err(UNKNOWN.into())
}

fn recorded_runtime(steam: &Path, prefix: &Path) -> Result<(PathBuf, PathBuf), String> {
    const MISMATCH: &str = "Proton 前缀记录缺失或与已安装工具不一致，请通过 Steam 正常启动并退出游戏一次。";
    let compat = prefix.parent().ok_or(MISMATCH)?;
    let config = optional_text(&compat.join("config_info"))?.ok_or(MISMATCH)?;
    let version = optional_text(&compat.join("version"))?.ok_or(MISMATCH)?;
    let mut fields = config.lines();
    let recorded = fields.next().filter(|version| !version.is_empty()).ok_or(MISMATCH)?;
    if version.trim() != recorded {
        return Err(MISMATCH.into());
    }
    let fonts = Path::new(fields.next().ok_or(MISMATCH)?);
    let libs = Path::new(fields.next().ok_or(MISMATCH)?);
    let recorded_steam = Path::new(fields.next().ok_or(MISMATCH)?);
    if !fonts.is_absolute() || !libs.is_absolute() || !recorded_steam.is_absolute()
        || fs::canonicalize(recorded_steam).ok().as_deref() != Some(steam)
    {
        return Err(MISMATCH.into());
    }
    // config_info stores paths inside the exact Proton distribution, not a
    // display name. Proton's top-level `version` is a build ID; the prefix schema
    // version is CURRENT_PREFIX_VERSION in the registered launcher instead.
    let proton = fonts.ancestors().find(|path| path.join("proton").is_file()).ok_or(MISMATCH)?;
    let dist = fonts.strip_prefix(proton).ok()
        .and_then(|path| path.components().next()).map(|name| proton.join(name)).ok_or(MISMATCH)?;
    if !libs.starts_with(&dist) || !fonts.is_dir() || !libs.is_dir() {
        return Err(MISMATCH.into());
    }
    let launcher = optional_text(&proton.join("proton"))?.ok_or(MISMATCH)?;
    if prefix_schema_version(&launcher) != Some(recorded) {
        return Err(MISMATCH.into());
    }
    Ok((canonicalize(proton)?, canonicalize(&dist)?))
}

fn prefix_schema_version(source: &str) -> Option<&str> {
    let (_, value) = source.lines().filter_map(|line| line.split_once('='))
        .find(|(name, _)| name.trim() == "CURRENT_PREFIX_VERSION")?;
    let value = value.trim_start();
    let quote = value.chars().next().filter(|c| matches!(c, '\'' | '"'))?;
    value[1..].split_once(quote).map(|(version, _)| version)
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
enum Setting {
    Absent,
    Compatible { exact: bool, stamp: Option<u64> },
    Conflict,
}

impl Setting {
    fn owned(self, marker: Option<u64>) -> bool {
        matches!(self, Self::Compatible { exact: true, stamp: Some(stamp) } if Some(stamp) == marker)
    }
}

fn read_setting(prefix: &Path, executable: &str) -> Result<Setting, String> {
    let path = prefix.join("user.reg");
    if !fs::symlink_metadata(&path).is_ok_and(|meta| meta.is_file()) {
        return Err("Proton 前缀未初始化或 user.reg 不是普通文件。".into());
    }
    let contents = fs::read(&path).map_err(|error| format!("无法读取 Proton 游戏注册表：{error}"))?;
    parse_setting(&contents, executable)
}

fn parse_setting(contents: &[u8], executable: &str) -> Result<Setting, String> {
    let mut lines = contents.split(|&byte| byte == b'\n').map(|line| line.trim_ascii());
    if lines.next() != Some(b"WINE REGISTRY Version 2".as_slice()) {
        return Err("无法识别 Proton 前缀注册表，未修改 DLL 覆盖。".into());
    }
    let key = format!(r"Software\\Wine\\AppDefaults\\{executable}\\DllOverrides");
    let mut active = false;
    let mut stamp = None;
    let mut setting = Setting::Absent;
    for line in lines {
        if let Some(section) = line.strip_prefix(b"[") {
            active = section.split(|&byte| byte == b']').next()
                .is_some_and(|name| name.eq_ignore_ascii_case(key.as_bytes()));
            stamp = None;
        } else if active {
            if let Some(time) = line.strip_prefix(b"#time=") {
                stamp = std::str::from_utf8(time).ok().and_then(|time| u64::from_str_radix(time, 16).ok());
            } else if let Some(line) = line.strip_prefix(b"\"") {
                let Some(quote) = line.iter().position(|&byte| byte == b'"') else { continue };
                let (name, tail) = line.split_at(quote);
                let plain = name.eq_ignore_ascii_case(b"version");
                let alias = [b"*version".as_slice(), b"version.dll", b"*version.dll"].iter()
                    .any(|alias| name.eq_ignore_ascii_case(alias));
                if !plain && !alias { continue; }
                let value = tail[1..].trim_ascii_start().strip_prefix(b"=").unwrap_or_default().trim_ascii();
                // Any different value type or alias is user configuration, even if
                // we cannot determine its effective order from this file.
                if !plain || setting != Setting::Absent || !compatible_order(value) {
                    return Ok(Setting::Conflict);
                }
                setting = Setting::Compatible { exact: value == OVERRIDE_VALUE, stamp };
            }
        }
    }
    Ok(setting)
}

fn compatible_order(value: &[u8]) -> bool {
    let Some(value) = value.strip_prefix(b"\"").and_then(|value| value.strip_suffix(b"\"")) else { return false };
    let Some(comma) = value.iter().position(|&byte| byte == b',') else { return false };
    let right = value[comma + 1..].trim_ascii();
    let left = value[..comma].trim_ascii();
    (left.eq_ignore_ascii_case(b"native") || left.eq_ignore_ascii_case(b"n"))
        && (right.eq_ignore_ascii_case(b"builtin") || right.eq_ignore_ascii_case(b"b"))
}

struct OwnerRecord {
    path: PathBuf,
    binding: Vec<u8>,
}

impl OwnerRecord {
    fn new(dir: &Path, prefix: &Path, executable: &str) -> Result<Self, String> {
        let meta = fs::metadata(prefix).map_err(|error| format!("无法读取 Proton 前缀标识：{error}"))?;
        let mut binding = format!("v1/{}/{}/{executable}\0", meta.dev(), meta.ino()).into_bytes();
        binding.extend_from_slice(prefix.as_os_str().as_bytes());
        // Stable filename only. The complete binding is checked on every read.
        let key = prefix.as_os_str().as_bytes().iter().chain(executable.as_bytes())
            .fold(0xcbf29ce484222325u64, |hash, &byte| (hash ^ u64::from(byte)).wrapping_mul(0x100000001b3));
        Ok(Self { path: dir.join(format!("{key:016x}.owner")), binding })
    }

    fn stamp(&self) -> Result<Option<u64>, String> {
        match fs::symlink_metadata(&self.path) {
            Err(error) if error.kind() == io::ErrorKind::NotFound => return Ok(None),
            Ok(metadata) if metadata.is_file() => (),
            Ok(_) => return Err("DLL 覆盖所有权记录不是普通文件。".into()),
            Err(error) => return Err(format!("无法读取 DLL 覆盖所有权记录：{error}")),
        }
        let record = fs::read(&self.path).map_err(|error| format!("无法读取 DLL 覆盖所有权记录：{error}"))?;
        // A crash before committing ownership may leave an empty reservation.
        if record.is_empty() {
            return Ok(None);
        }
        let stamp = record.strip_prefix(self.binding.as_slice()).and_then(|stamp| stamp.try_into().ok())
            .map(u64::from_le_bytes).ok_or("DLL 覆盖所有权记录与当前 Proton 前缀不符，未修改覆盖。")?;
        Ok(Some(stamp))
    }

    fn reserve(&self) -> Result<File, String> {
        if fs::symlink_metadata(&self.path).is_ok_and(|metadata| metadata.is_file() && metadata.len() == 0) {
            self.remove()?;
        }
        File::options().write(true).create_new(true).mode(0o600).open(&self.path)
            .map_err(|error| format!("无法保存 DLL 覆盖所有权记录：{error}"))
    }

    fn commit(&self, file: &mut File, stamp: u64) -> Result<(), String> {
        file.write_all(&self.binding).and_then(|()| file.write_all(&stamp.to_le_bytes())).and_then(|()| file.sync_all())
            .map_err(|error| format!("无法保存 DLL 覆盖所有权记录：{error}"))
    }

    fn remove(&self) -> Result<(), String> {
        fs::remove_file(&self.path).map_err(|error| format!("无法清理 DLL 覆盖所有权记录：{error}"))
    }
}

fn owner_dir(data_dir: &Path, create: bool) -> Result<Option<PathBuf>, String> {
    let path = data_dir.join(OWNER_DIRECTORY);
    match fs::symlink_metadata(&path) {
        Ok(metadata) if metadata.is_dir() => Ok(Some(path)),
        Ok(_) => Err("DLL 覆盖所有权记录目录不是普通目录。".into()),
        Err(error) if error.kind() == io::ErrorKind::NotFound => {
            if !create { return Ok(None); }
            DirBuilder::new().mode(0o700).create(&path).map_err(|error| format!("无法创建 DLL 覆盖所有权记录目录：{error}"))?;
            Ok(Some(path))
        }
        Err(error) => Err(format!("无法读取 DLL 覆盖所有权记录目录：{error}")),
    }
}

pub(super) fn warning(game: &Installation) -> Result<(), String> {
    let prefix = select_prefix(game)?;
    let runtime = Runtime::resolve(game, prefix)?;
    match read_setting(&runtime.prefix, game.executable.name)? {
        Setting::Conflict => Err("此游戏已有不同的 version DLL 覆盖；工具箱不会修改现有设置。".into()),
        Setting::Absent if game.game_dir.join("version.dll").is_file() => {
            Err("此游戏版本尚未配置专用的 version DLL 覆盖；可在退出游戏后安装或更新加载器。".into())
        }
        _ => Ok(()),
    }
}

pub(super) fn ensure_override(game: &Installation, data_dir: &Path) -> Result<OverrideChange, String> {
    game.require_stopped()?;
    let prefix = select_prefix(game)?;
    let runtime = Runtime::resolve(game, prefix)?;
    let setting = read_setting(&runtime.prefix, game.executable.name)?;
    if setting == Setting::Conflict {
        return Err("此游戏已有不同的 version DLL 覆盖，未修改现有设置。".into());
    }
    let directory = owner_dir(data_dir, setting == Setting::Absent)?;
    let record = directory.as_ref().map(|dir| OwnerRecord::new(dir, &runtime.prefix, game.executable.name)).transpose()?;
    let owned_stamp = record.as_ref().map(OwnerRecord::stamp).transpose()?.flatten();
    if let Setting::Compatible { exact, stamp } = setting {
        runtime.run(game, "verify")?;
        let owned = Setting::Compatible { exact, stamp }.owned(owned_stamp);
        return Ok(OverrideChange {
            added: false,
            warning: (!owned).then(|| "已保留现有的游戏专用 version DLL 覆盖；卸载不会删除它。".to_owned()),
        });
    }
    let record = record.ok_or("无法创建 DLL 覆盖所有权记录。")?;
    if owned_stamp.is_some() {
        // The user already removed the previous value. It no longer belongs to us.
        record.remove()?;
    }
    game.require_stopped()?;
    let mut reservation = record.reserve()?;
    if let Err(error) = runtime.run(game, "add") {
        return Err(with_cleanup(error, record.remove()));
    }
    let stamp = match read_setting(&runtime.prefix, game.executable.name) {
        Ok(Setting::Compatible { exact: true, stamp: Some(stamp) }) => stamp,
        Ok(_) => return Err(with_cleanup("无法验证已写入的游戏专用 version DLL 覆盖。".into(), record.remove())),
        Err(error) => return Err(with_cleanup(error, record.remove())),
    };
    if let Err(error) = record.commit(&mut reservation, stamp) {
        let rollback = game.require_stopped().and_then(|()| {
            if !read_setting(&runtime.prefix, game.executable.name)?.owned(Some(stamp)) {
                return Err("DLL 覆盖已有变动，已保留当前设置。".into());
            }
            runtime.run(game, "remove")
        });
        return Err(with_cleanup(with_cleanup(error, rollback), record.remove()));
    }
    Ok(OverrideChange { added: true, warning: None })
}

pub(super) fn restore_override(game: &Installation, data_dir: &Path) -> Result<Option<String>, String> {
    let Some(dir) = owner_dir(data_dir, false)? else { return Ok(None) };
    let prefix = select_prefix(game)?;
    let record = OwnerRecord::new(&dir, &prefix, game.executable.name)?;
    let Some(stamp) = record.stamp()? else { return Ok(None) };
    game.require_stopped()?;
    match read_setting(&prefix, game.executable.name)? {
        Setting::Absent => { record.remove()?; Ok(None) }
        setting if setting.owned(Some(stamp)) => {
            let runtime = Runtime::resolve(game, prefix)?;
            game.require_stopped()?;
            runtime.run(game, "remove")?;
            record.remove()?;
            Ok(None)
        }
        _ => {
            record.remove()?;
            Ok(Some("游戏专用 version DLL 覆盖已变更，已保留当前设置。".into()))
        }
    }
}

fn with_cleanup(error: String, result: Result<(), String>) -> String {
    match result {
        Ok(()) => error,
        Err(cleanup) => format!("{error}\n清理 DLL 覆盖失败：{cleanup}"),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::steam::test_support::Scratch;

    #[test]
    fn uses_app_mapping_before_global_and_does_not_assume_experimental() {
        let source = r#"InstallConfigStore { Software { Valve { Steam { CompatToolMapping {
            0 { name proton_10 }
            2622000 { name GE-Custom }
        } } } } }"#;
        assert_eq!(mapped_tool(source).unwrap(), "GE-Custom");
        assert_eq!(mapped_tool(&source.replace("2622000", "1234")).unwrap(), "proton_10");
        assert!(mapped_tool("InstallConfigStore {}").is_err());
        assert!(mapped_tool(&source.replace("2622000", "0")).is_err());
    }

    #[test]
    fn verifies_recorded_tool_path_and_prefix_schema_not_build_id() {
        let temp = Scratch::new();
        let steam = canonicalize(&temp.0).unwrap();
        let tool = steam.join("Proton Custom");
        temp.write("Proton Custom/proton", b"CURRENT_PREFIX_VERSION = 'ge-10-2'\n");
        temp.write("Proton Custom/version", b"1780012345 GE-Custom-build\n");
        fs::create_dir_all(tool.join("files/share/fonts")).unwrap();
        fs::create_dir_all(tool.join("files/lib")).unwrap();
        temp.write("compat/config_info", format!("ge-10-2\n{tool}/files/share/fonts/\n{tool}/files/lib/\n{steam}\n",
            tool = tool.display(), steam = steam.display()));
        temp.write("compat/version", "ge-10-2\n");
        let prefix = steam.join("compat/pfx");
        assert_eq!(recorded_runtime(&steam, &prefix).unwrap(), (tool.clone(), tool.join("files")));
        temp.write("compat/version", "ge-10-1\n");
        assert!(recorded_runtime(&steam, &prefix).is_err());
        temp.write("compat/version", "ge-10-2\n");
        temp.write("Proton Custom/proton", b"CURRENT_PREFIX_VERSION = 'ge-10-3'\n");
        assert!(recorded_runtime(&steam, &prefix).is_err());
    }

    #[test]
    fn fixed_runner_refuses_a_different_steam_prefix_or_proton() {
        let temp = Scratch::new();
        let game = temp.0.join("game");
        let prefix = temp.0.join("prefix");
        let tool = temp.0.join("tool");
        let dist = tool.join("files");
        for path in [&game, &prefix, &dist] {
            fs::create_dir_all(path).unwrap();
        }
        let mut command = Command::new("/bin/sh");
        command.args(["-c", REGISTRY_COMMAND]).env_clear()
            .env("WINE", "/bin/false").env("WINESERVER", "/bin/false")
            .env("STEAM_APPID", APP_ID).env("STEAM_APP_PATH", &game).env("TOYS_STEAM_GAME", &game)
            .env("WINEPREFIX", &prefix).env("TOYS_STEAM_PREFIX", &prefix)
            .env("PROTON_PATH", &tool).env("TOYS_STEAM_PROTON", &tool)
            .env("PROTON_DIST_PATH", &dist).env("TOYS_STEAM_DIST", &dist)
            .env("TOYS_STEAM_ACTION", "verify");
        assert!(command.status().unwrap().success());
        command.env("TOYS_STEAM_ACTION", "add").env("WINEPREFIX", &game);
        assert_eq!(command.status().unwrap().code(), Some(80));
        command.env("WINEPREFIX", &prefix).env("PROTON_PATH", &game);
        assert_eq!(command.status().unwrap().code(), Some(81));
    }

    #[test]
    fn detects_exact_edition_override_and_preserves_user_values() {
        let registry = r#"WINE REGISTRY Version 2
[Software\\Wine\\DllOverrides] 100
"version"="builtin"
[Software\\Wine\\AppDefaults\\AstralParty_INT.exe\\DllOverrides] 101
#time=abc
"version"="native,builtin"
[Software\\Wine\\AppDefaults\\AstralParty_CN.exe\\DllOverrides] 102
"version"="builtin,native"
"#;
        assert_eq!(parse_setting(registry.as_bytes(), "AstralParty_INT.exe").unwrap(), Setting::Compatible { exact: true, stamp: Some(0xabc) });
        assert_eq!(parse_setting(registry.as_bytes(), "AstralParty_CN.exe").unwrap(), Setting::Conflict);
        assert_eq!(parse_setting(registry.as_bytes(), "AstralParty.exe").unwrap(), Setting::Absent);
        let alias = registry.replace("\"version\"", "\"*VERSION\"");
        assert_eq!(parse_setting(alias.as_bytes(), "AstralParty_INT.exe").unwrap(), Setting::Conflict);
        assert!(parse_setting(b"not a registry", "AstralParty_INT.exe").is_err());
    }

    #[test]
    fn ownership_is_bound_to_the_prefix_and_executable() {
        let temp = Scratch::new();
        let prefix = temp.0.join("pfx");
        fs::create_dir(&prefix).unwrap();
        let record = OwnerRecord::new(&temp.0, &prefix, "AstralParty_INT.exe").unwrap();
        let mut file = record.reserve().unwrap();
        record.commit(&mut file, 0xabc).unwrap();
        assert_eq!(record.stamp().unwrap(), Some(0xabc));
        // This is also the ownership gate for install rollback. An identical user
        // value with no record, or a later reassignment, is never eligible.
        let owned = Setting::Compatible { exact: true, stamp: Some(0xabc) };
        assert!(owned.owned(record.stamp().unwrap()));
        assert!(!owned.owned(None));
        assert!(!owned.owned(Some(0xdef)));
        assert!(!Setting::Conflict.owned(record.stamp().unwrap()));
        assert_eq!(OwnerRecord::new(&temp.0, &prefix, "AstralParty_CN.exe").unwrap().stamp().unwrap(), None);
        let other = temp.0.join("other");
        fs::create_dir(&other).unwrap();
        let foreign = OwnerRecord::new(&temp.0, &other, "AstralParty_INT.exe").unwrap();
        fs::copy(record.path, &foreign.path).unwrap();
        assert!(foreign.stamp().is_err());
    }

    #[test]
    #[ignore = "requires a separately initialized native Steam/Proton fixture under the temporary directory"]
    fn disposable_proton_registry_round_trip() {
        let fixture = env::var_os("TOYS_TEST_STEAM_ROOT").expect("select a disposable Steam fixture");
        let root = canonicalize(Path::new(&fixture)).unwrap();
        let temp = canonicalize(&env::temp_dir()).unwrap();
        assert!(root.starts_with(&temp) && root != temp);
        let games = super::super::discover_roots(&root, std::slice::from_ref(&root)).unwrap();
        assert_eq!(games.len(), 1, "fixture must contain only one game edition");
        let game = &games[0];
        let prefix = select_prefix(game).unwrap();
        let runtime = Runtime::resolve(game, prefix.clone()).unwrap();
        for path in [&game.game_dir, &prefix, &runtime.proton, &runtime.dist] {
            assert!(path.starts_with(&root), "fixture must not reuse live game, prefix, or Proton paths");
        }
        assert_eq!(read_setting(&prefix, game.executable.name).unwrap(), Setting::Absent);

        let owner = Scratch::new();
        let unowned = Scratch::new();
        assert!(ensure_override(game, &owner.0).unwrap().added);
        let setting = read_setting(&prefix, game.executable.name).unwrap();
        assert!(matches!(setting, Setting::Compatible { exact: true, stamp: Some(_) }));
        let retained = ensure_override(game, &unowned.0).unwrap();
        assert!(!retained.added && retained.warning.is_some());
        assert_eq!(restore_override(game, &unowned.0).unwrap(), None);
        assert_eq!(read_setting(&prefix, game.executable.name).unwrap(), setting);

        // This is the same compensation used when managed file installation fails.
        assert_eq!(restore_override(game, &owner.0).unwrap(), None);
        assert_eq!(read_setting(&prefix, game.executable.name).unwrap(), Setting::Absent);
    }
}
