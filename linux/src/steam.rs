mod process;
mod proton;
mod vdf;

use std::{
    collections::HashSet,
    env, fs, io,
    path::{Component, Path, PathBuf},
    sync::Arc,
};

pub use proton::OverrideChange;

const APP_ID: &str = "2622000";
const MANIFEST_NAME: &str = "appmanifest_2622000.acf";
const FULLY_INSTALLED: u32 = 4;
const APP_RUNNING: u32 = 64;

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct GameExe {
    name: &'static str,
    edition: &'static str,
}

const EXECUTABLES: [GameExe; 3] = [
    GameExe { name: "AstralParty_CN.exe", edition: "cn" },
    GameExe { name: "AstralParty_INT.exe", edition: "global" },
    GameExe { name: "AstralParty.exe", edition: "global" },
];

pub struct Installation {
    pub game_dir: PathBuf,
    pub edition: String,
    executable: GameExe,
    game_root: PathBuf,
    steam_root: PathBuf,
    steamapps: PathBuf,
    libraries: Arc<[PathBuf]>,
}

impl Installation {
    pub fn running(&self) -> Option<bool> {
        let process = self.process_status();
        if process == Some(true) {
            return process;
        }
        let manifest = read_manifest(&self.steamapps).ok()??;
        let flags = manifest.state_flags?;
        if flags & APP_RUNNING != 0 { Some(true) } else { process }
    }

    pub fn require_stopped(&self) -> Result<(), String> {
        let manifest = read_manifest(&self.steamapps)?.ok_or("Steam 游戏清单已不存在，请重新扫描。")?;
        if manifest.state_flags.is_some_and(|flags| flags & APP_RUNNING != 0) {
            return Err("请在 Steam 中彻底退出游戏后再管理加载器。".into());
        }
        if manifest.state_flags != Some(FULLY_INSTALLED) {
            return Err("Steam 尚未确认游戏已完整安装或正在处理游戏文件，请稍后重试。".into());
        }
        if install_root(&self.steamapps, &manifest.install_name)? != self.game_root
            || fs::canonicalize(&self.game_dir).ok().as_ref() != Some(&self.game_dir)
            || detect_executable(&self.game_dir)? != Some(self.executable)
        {
            return Err("游戏路径或版本已变动，请重新扫描后选择游戏。".into());
        }
        match self.process_status() {
            Some(false) => Ok(()),
            Some(true) => Err("请在 Steam 中彻底退出游戏后再管理加载器。".into()),
            None => Err("无法确认游戏已完全退出，未执行加载器操作。".into()),
        }
    }

    pub fn loader_warning(&self) -> Option<String> {
        proton::warning(self).err()
    }

    pub fn ensure_override(&self, data_dir: &Path) -> Result<OverrideChange, String> {
        proton::ensure_override(self, data_dir)
    }

    pub fn restore_override(&self, data_dir: &Path) -> Result<Option<String>, String> {
        proton::restore_override(self, data_dir)
    }

    fn process_status(&self) -> Option<bool> {
        let local = compat_directory(&self.steamapps);
        let compat_dir = fs::canonicalize(&local).unwrap_or(local);
        process::running(Path::new("/proc"), &compat_dir)
    }
}

// An unavailable Steam library is different from a loaded library without the game.
pub fn discover() -> Result<Vec<Installation>, String> {
    let home = env::var_os("HOME").map(PathBuf::from).filter(|path| path.is_absolute())
        .ok_or("无法确定当前用户的 Steam 安装位置。")?;
    let xdg = env::var_os("XDG_DATA_HOME").map(PathBuf::from).filter(|path| path.is_absolute());
    let roots = [home.join(".steam/steam"), home.join(".steam/root"), home.join(".steam/debian-installation"),
        xdg.unwrap_or_else(|| home.join(".local/share")).join("Steam"), home.join(".local/share/Steam")];
    discover_roots(&home, &roots)
}

pub fn find_installation<'a>(installations: &'a [Installation], directory: &Path) -> Result<&'a Installation, String> {
    if !directory.is_absolute() {
        return Err("必须选择已检测到的 Steam 游戏目录。".into());
    }
    let canonical = fs::canonicalize(directory).map_err(|_| "所选 Steam 游戏目录已不可用。")?;
    let mut matches = installations.iter().filter(|game| game.game_dir == canonical);
    let installation = matches.next().ok_or("所选目录不在已检测到的 Steam 游戏列表中。")?;
    if matches.next().is_some() {
        return Err("所选游戏目录被多个 Steam 安装共用，无法安全确定游戏前缀。".into());
    }
    Ok(installation)
}

fn discover_roots(home: &Path, roots: &[PathBuf]) -> Result<Vec<Installation>, String> {
    let mut seen = HashSet::new();
    let mut available = false;
    let mut installations = Vec::new();
    let mut issue = None;
    let canonical_home = fs::canonicalize(home).unwrap_or_else(|_| home.to_owned());
    for path in roots {
        let root = match fs::canonicalize(path) {
            Ok(root) => root,
            Err(error) if error.kind() == io::ErrorKind::NotFound => continue,
            Err(error) => {
                issue.get_or_insert_with(|| format!("无法读取 Steam 位置 {}：{error}", path.display()));
                continue;
            }
        };
        if is_sandbox_steam(&canonical_home, &root) || !seen.insert(root.clone()) {
            continue;
        }
        match discover_root(&root, &mut installations, &mut issue) {
            Ok(found) => available |= found,
            Err(error) => { issue.get_or_insert(error); }
        }
    }
    if installations.is_empty() {
        if let Some(issue) = issue {
            return Err(issue);
        }
        if !available {
            return Err("未找到可读取的原生 Steam 安装；不支持 Flatpak 或 Snap Steam。".into());
        }
    }
    installations.sort_unstable_by(|a, b| a.game_dir.cmp(&b.game_dir));
    Ok(installations)
}

fn is_sandbox_steam(home: &Path, root: &Path) -> bool {
    [".var/app/com.valvesoftware.Steam", "snap/steam", ".snap/data/steam"]
        .iter().any(|path| root.starts_with(home.join(path)))
}

fn discover_root(root: &Path, installations: &mut Vec<Installation>, issue: &mut Option<String>) -> Result<bool, String> {
    let Some(default) = steamapps_dir(root)? else { return Ok(false) };
    let mut registered = vec![(default, false)];
    for library in library_entries(root, &registered[0].0)? {
        match steamapps_dir(&library.path) {
            Ok(Some(apps)) => {
                if let Some(existing) = registered.iter_mut().find(|(path, _)| *path == apps) {
                    existing.1 |= library.game_listed;
                } else {
                    registered.push((apps, library.game_listed));
                }
            }
            Ok(None) if library.game_listed => {
                issue.get_or_insert_with(|| format!("包含游戏的 Steam 库不可用：{}", library.path.display()));
            }
            Err(error) => { issue.get_or_insert(error); }
            Ok(None) => (),
        }
    }
    let libraries: Arc<[PathBuf]> = registered.iter().map(|(path, _)| path.clone()).collect();
    for (steamapps, game_listed) in registered {
        match discover_library(root, &steamapps, game_listed, &libraries, installations) {
            Ok(()) => (),
            Err(error) => { issue.get_or_insert(error); }
        }
    }
    Ok(true)
}

fn steamapps_dir(root: &Path) -> Result<Option<PathBuf>, String> {
    let conventional = root.join("steamapps");
    match fs::metadata(&conventional) {
        Ok(metadata) if metadata.is_dir() => return canonicalize(&conventional).map(Some),
        Ok(_) => return Err(format!("Steam 库路径不是目录：{}", conventional.display())),
        Err(error) if error.kind() == io::ErrorKind::NotFound => (),
        Err(error) => return Err(format!("无法读取 Steam 库 {}：{error}", conventional.display())),
    }
    // Steam also accepts case-insensitive SteamApps directory names on imported libraries.
    let entries = match fs::read_dir(root) {
        Ok(entries) => entries,
        Err(error) if error.kind() == io::ErrorKind::NotFound => return Ok(None),
        Err(error) => return Err(format!("无法读取 Steam 位置 {}：{error}", root.display())),
    };
    let mut found = None;
    for entry in entries {
        let entry = entry.map_err(|error| format!("无法读取 Steam 库 {}：{error}", root.display()))?;
        if entry.file_name().as_encoded_bytes().eq_ignore_ascii_case(b"steamapps") {
            if found.is_some() {
                return Err(format!("Steam 库包含多个 SteamApps 目录：{}", root.display()));
            }
            found = Some(canonicalize(&entry.path())?);
        }
    }
    Ok(found.filter(|path| path.is_dir()))
}

struct LibraryEntry {
    path: PathBuf,
    game_listed: bool,
}

fn library_entries(root: &Path, steamapps: &Path) -> Result<Vec<LibraryEntry>, String> {
    let current = steamapps.join("libraryfolders.vdf");
    let legacy = root.join("config/libraryfolders.vdf");
    let source = match optional_text(&current)? {
        Some(text) => text,
        None => match optional_text(&legacy)? {
            Some(text) => text,
            None => return Ok(Vec::new()),
        },
    };
    let tree = vdf::parse(&source)?;
    let entries = tree.object("libraryfolders")?.ok_or("Steam 库索引缺少 libraryfolders。")?;
    let mut indices = HashSet::new();
    let mut libraries = Vec::new();
    for (key, value) in &entries.0 {
        if key.is_empty() || !key.bytes().all(|byte| byte.is_ascii_digit()) {
            continue;
        }
        if !indices.insert(key.as_ref()) {
            return Err(format!("Steam 库索引重复：{key}"));
        }
        let (path, game_listed) = match value {
            vdf::Value::Text(path) => (path.as_ref(), false),
            vdf::Value::Object(library) => (
                library.text("path")?.ok_or("Steam 库索引缺少目录。")?,
                match library.object("apps")? {
                    Some(apps) => apps.contains(APP_ID)?,
                    None => false,
                },
            ),
        };
        let path = PathBuf::from(path);
        if !path.is_absolute() {
            return Err(format!("Steam 库索引包含非绝对路径：{}", path.display()));
        }
        libraries.push(LibraryEntry { path, game_listed });
    }
    Ok(libraries)
}

fn discover_library(
    steam_root: &Path, steamapps: &Path, game_listed: bool, libraries: &Arc<[PathBuf]>, installations: &mut Vec<Installation>,
) -> Result<(), String> {
    let Some(manifest) = read_manifest(steamapps)? else {
        if game_listed {
            return Err(format!("Steam 库索引已列出游戏，但游戏清单不可用：{}", steamapps.display()));
        }
        return Ok(());
    };
    let game_root = install_root(steamapps, &manifest.install_name)?;
    let mut candidates = Vec::new();
    candidates.push(game_root.clone());
    // The manifest names the depot root; each separately packaged edition is a direct child.
    for entry in fs::read_dir(&game_root).map_err(|error| format!("无法读取游戏安装目录：{error}"))? {
        let entry = entry.map_err(|error| format!("无法读取游戏安装目录：{error}"))?;
        if entry.file_type().map_err(|error| error.to_string())?.is_dir() {
            candidates.push(entry.path());
        }
    }
    let mut found = false;
    let mut ambiguous = None;
    for game_dir in candidates {
        let executable = match detect_executable(&game_dir) {
            Ok(Some(exe)) => exe,
            Ok(None) => continue,
            Err(error) => { ambiguous.get_or_insert(error); continue; }
        };
        if game_dir.to_str().is_none() {
            ambiguous.get_or_insert_with(|| format!("游戏目录不是 UTF-8 路径：{}", game_dir.display()));
            continue;
        }
        installations.push(Installation {
            game_dir,
            edition: executable.edition.to_owned(),
            executable,
            game_root: game_root.clone(),
            steam_root: steam_root.to_owned(),
            steamapps: steamapps.to_owned(),
            libraries: Arc::clone(libraries),
        });
        found = true;
    }
    if let Some(error) = ambiguous {
        return Err(error);
    }
    if !found {
        return Err(format!("Steam 游戏清单存在，但没有找到受支持的吉星派对主程序：{}", game_root.display()));
    }
    Ok(())
}

struct Manifest {
    install_name: String,
    state_flags: Option<u32>,
}

fn read_manifest(steamapps: &Path) -> Result<Option<Manifest>, String> {
    let path = steamapps.join(MANIFEST_NAME);
    let Some(source) = optional_text(&path)? else { return Ok(None) };
    let tree = vdf::parse(&source)?;
    let app = tree.object("AppState")?.ok_or("Steam 游戏清单缺少 AppState。")?;
    if app.text("appid")? != Some(APP_ID) {
        return Err(format!("Steam 游戏清单的 AppID 不匹配：{}", path.display()));
    }
    let install = app.text("installdir")?.ok_or("Steam 游戏清单缺少安装目录。")?;
    let mut components = Path::new(install).components();
    if !matches!(components.next(), Some(Component::Normal(name)) if name == install)
        || components.next().is_some() || install.contains('\\') || install.chars().any(char::is_control)
    {
        return Err("Steam 游戏清单中的安装目录无效。".into());
    }
    let state_flags = app.text("StateFlags")?.map(str::parse).transpose()
        .map_err(|_| "Steam 游戏清单中的 StateFlags 无效。")?;
    Ok(Some(Manifest { install_name: install.to_owned(), state_flags }))
}

fn install_root(steamapps: &Path, install_name: &str) -> Result<PathBuf, String> {
    let common = canonicalize(&steamapps.join("common"))?;
    let directory = canonicalize(&common.join(install_name))?;
    if directory.parent() != Some(common.as_path()) || !directory.is_dir() {
        return Err(format!("游戏安装目录不在所属的 Steam 库中：{}", directory.display()));
    }
    Ok(directory)
}

fn detect_executable(directory: &Path) -> Result<Option<GameExe>, String> {
    let entries = fs::read_dir(directory).map_err(|error| format!("无法读取游戏目录 {}：{error}", directory.display()))?;
    let mut count = 0;
    let mut known = None;
    for entry in entries {
        let entry = entry.map_err(|error| error.to_string())?;
        let name = entry.file_name();
        let Some(name) = name.to_str() else { continue };
        if name.starts_with("AstralParty") && name.ends_with(".exe") {
            count += 1;
            if let Some(executable) = EXECUTABLES.iter().find(|exe| exe.name == name) {
                if !entry.file_type().map_err(|error| error.to_string())?.is_file() {
                    return Err(format!("游戏主程序不是普通文件：{}", entry.path().display()));
                }
                known = Some(*executable);
            }
        }
    }
    if known.is_some() && count > 1 {
        return Err(format!("同一目录包含多个吉星派对主程序，无法安全区分游戏版本：{}", directory.display()));
    }
    Ok(known)
}

fn compat_directory(steamapps: &Path) -> PathBuf {
    steamapps.join("compatdata").join(APP_ID)
}

fn canonicalize(path: &Path) -> Result<PathBuf, String> {
    fs::canonicalize(path).map_err(|error| format!("无法解析路径 {}：{error}", path.display()))
}

fn optional_text(path: &Path) -> Result<Option<String>, String> {
    match fs::read_to_string(path) {
        Ok(text) => Ok(Some(text)),
        Err(error) if error.kind() == io::ErrorKind::NotFound => Ok(None),
        Err(error) => Err(format!("无法读取 {}：{error}", path.display())),
    }
}

#[cfg(test)]
mod test_support {
    use super::*;
    use std::sync::atomic::{AtomicU64, Ordering};

    pub(crate) struct Scratch(pub PathBuf);

    impl Scratch {
        pub fn new() -> Self {
            static NEXT: AtomicU64 = AtomicU64::new(0);
            let path = env::temp_dir().join(format!("toys-steam-{}-{}", std::process::id(), NEXT.fetch_add(1, Ordering::Relaxed)));
            fs::create_dir(&path).unwrap();
            Self(path)
        }

        pub fn write(&self, path: impl AsRef<Path>, contents: impl AsRef<[u8]>) {
            let path = self.0.join(path);
            fs::create_dir_all(path.parent().unwrap()).unwrap();
            fs::write(path, contents).unwrap();
        }
    }

    impl Drop for Scratch {
        fn drop(&mut self) { let _ = fs::remove_dir_all(&self.0); }
    }
}

#[cfg(test)]
mod tests {
    use super::{test_support::Scratch, *};
    use std::os::unix::fs::symlink;

    fn manifest(name: &str) -> String {
        format!(r#"AppState {{ appid 2622000 installdir "{name}" StateFlags 4 }}"#)
    }

    #[test]
    fn discovers_external_library_and_exposes_both_depot_editions() {
        let temp = Scratch::new();
        let root = temp.0.join("steam");
        let external = temp.0.join("external space");
        temp.write("steam/steamapps/libraryfolders.vdf", format!(r#"
            // Current library list with a mount containing the target app.
            libraryfolders {{
                0 "{root}"
                1 {{ path "{external}" apps {{ 2622000 12345 }} }}
            }}
        "#, root = root.display(), external = external.display()));
        temp.write("external space/SteamApps/appmanifest_2622000.acf", manifest("Astral Party"));
        temp.write("external space/SteamApps/common/Astral Party/subCN/AstralParty_CN.exe", b"");
        temp.write("external space/SteamApps/common/Astral Party/subINT/AstralParty_INT.exe", b"");
        temp.write("external space/SteamApps/appmanifest_999.acf", manifest("Other"));
        temp.write("external space/SteamApps/common/Other/AstralParty_CN.exe", b"");
        let alias = temp.0.join("steam-link");
        symlink(&root, &alias).unwrap();
        let found = discover_roots(&temp.0, &[alias, root]).unwrap();
        assert_eq!(found.len(), 2);
        assert_eq!(found[0].edition, "cn");
        assert_eq!(found[1].edition, "global");
        let cn_alias = temp.0.join("cn-link");
        symlink(&found[0].game_dir, &cn_alias).unwrap();
        assert_eq!(find_installation(&found, &cn_alias).unwrap().game_dir, found[0].game_dir);
        assert!(find_installation(&found, &external).is_err());
    }

    #[test]
    fn distinguishes_unavailable_steam_from_no_installed_game() {
        let temp = Scratch::new();
        let root = temp.0.join("steam");
        assert!(discover_roots(&temp.0, std::slice::from_ref(&root)).is_err());
        fs::create_dir_all(root.join("steamapps")).unwrap();
        assert!(discover_roots(&temp.0, std::slice::from_ref(&root)).unwrap().is_empty());
        let offline = temp.0.join("unmounted");
        temp.write("steam/steamapps/libraryfolders.vdf", format!(r#"libraryfolders {{ 1 {{ path "{}" apps {{ 2622000 1 }} }} }}"#, offline.display()));
        assert!(discover_roots(&temp.0, &[root]).is_err());
    }

    #[test]
    fn rejects_same_directory_ambiguity_and_install_directory_escape() {
        let temp = Scratch::new();
        let root = temp.0.join("steam");
        let path = "steam/steamapps/appmanifest_2622000.acf";
        temp.write(path, manifest("Game"));
        temp.write("steam/steamapps/common/Game/AstralParty_CN.exe", b"");
        temp.write("steam/steamapps/common/Game/AstralParty_INT.exe", b"");
        assert!(discover_roots(&temp.0, std::slice::from_ref(&root)).err().unwrap().contains("多个"));
        temp.write(path, manifest("../foreign"));
        assert!(discover_roots(&temp.0, &[root]).err().unwrap().contains("无效"));
    }
}
