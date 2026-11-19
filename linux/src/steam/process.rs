use super::{APP_ID, EXECUTABLES};
use std::{
    ffi::OsStr,
    fs, io,
    os::unix::{ffi::OsStrExt, fs::MetadataExt},
    path::Path,
};

// Steam/Proton identify startup helpers before the Windows game process exists.
// Inspect command lines first and only read Wine/Steam launchers' environments.
pub(super) fn running(proc_dir: &Path, compat_dir: &Path) -> Option<bool> {
    let self_status = fs::read(proc_dir.join("self/status")).ok()?;
    let uid = effective_uid(&self_status)?;
    let mut known = true;
    for entry in fs::read_dir(proc_dir).ok()? {
        let Ok(entry) = entry else {
            known = false;
            continue;
        };
        let name = entry.file_name();
        let name = name.as_bytes();
        if name.is_empty() || !name.iter().all(u8::is_ascii_digit) {
            continue;
        }
        match process_is_game(&entry, uid, compat_dir) {
            Ok(true) => return Some(true),
            Ok(false) => (),
            Err(error) if disappeared(&error) && entry.path().try_exists().is_ok_and(|exists| !exists) => (),
            Err(_) => known = false,
        }
    }
    known.then_some(false)
}

fn process_is_game(entry: &fs::DirEntry, uid: u32, compat_dir: &Path) -> io::Result<bool> {
    let path = entry.path();
    // A process that disabled dumps can have a root-owned /proc directory
    // even when it runs as this user. Check its effective UID in that case.
    if entry.metadata()?.uid() != uid {
        match fs::read(path.join("status")) {
            Ok(status) if effective_uid(&status) == Some(uid) => (),
            Ok(_) => return Ok(false),
            Err(error) if error.kind() == io::ErrorKind::PermissionDenied => return Ok(false),
            Err(error) => return Err(error),
        }
    }
    let cmdline = fs::read(path.join("cmdline"))?;
    if cmdline.is_empty() {
        return Ok(false); // Zombie or kernel thread.
    }
    let mut args = cmdline.split(|&byte| byte == 0);
    let first = basename(args.next().unwrap_or_default());
    if game_exe(first) {
        return Ok(true);
    }
    // Wine can set the kernel comm to the game name while argv[0] shows its loader.
    let comm = fs::read(path.join("comm"))?;
    let comm = comm.strip_suffix(b"\n").unwrap_or(&comm);
    if EXECUTABLES.iter().any(|exe| {
        let name = exe.name.as_bytes();
        comm.eq_ignore_ascii_case(&name[..name.len().min(15)])
    }) {
        return Ok(true);
    }

    let launch_helper = is_launch_helper(first) || is_launch_helper(comm);
    let mut proton_script = false;
    for arg in args {
        if (launch_helper && game_exe(basename(arg)))
            || (launch_helper && arg.strip_prefix(b"AppId=") == Some(APP_ID.as_bytes()))
        {
            return Ok(true);
        }
        proton_script |= arg.starts_with(b"/") && basename(arg) == b"proton";
    }
    if launch_helper || proton_script || first.ends_with(b".exe") || comm.ends_with(b".exe") {
        return Ok(environment_is_game(&fs::read(path.join("environ"))?, compat_dir));
    }
    Ok(false)
}

fn game_exe(name: &[u8]) -> bool {
    EXECUTABLES.iter().any(|exe| name.eq_ignore_ascii_case(exe.name.as_bytes()))
}

fn is_launch_helper(name: &[u8]) -> bool {
    matches!(name, b"bwrap" | b"pv-bwrap" | b"proton" | b"reaper" | b"SteamLaunch")
        || name.starts_with(b"wine")
        || name.starts_with(b"proton-")
        || name.starts_with(b"pressure-vessel")
        || name.starts_with(b"steam-runtime-")
}

fn environment_is_game(env: &[u8], compat_dir: &Path) -> bool {
    env.split(|&byte| byte == 0).any(|entry| {
        for key in [b"SteamAppId=".as_slice(), b"SteamGameId=", b"STEAM_COMPAT_APP_ID=", b"STEAM_APPID="] {
            if entry.strip_prefix(key) == Some(APP_ID.as_bytes()) {
                return true;
            }
        }
        let path = if let Some(value) = entry.strip_prefix(b"STEAM_COMPAT_DATA_PATH=") {
            Path::new(OsStr::from_bytes(value))
        } else if let Some(value) = entry.strip_prefix(b"WINEPREFIX=") {
            let prefix = Path::new(OsStr::from_bytes(value));
            if prefix.file_name() != Some(OsStr::new("pfx")) {
                return false;
            }
            let Some(parent) = prefix.parent() else { return false };
            parent
        } else {
            return false;
        };
        path.is_absolute()
            && (path == compat_dir || fs::canonicalize(path).is_ok_and(|path| path == compat_dir))
    })
}

fn effective_uid(status: &[u8]) -> Option<u32> {
    let uids = status.split(|&byte| byte == b'\n').find_map(|line| line.strip_prefix(b"Uid:"))?;
    std::str::from_utf8(uids).ok()?.split_ascii_whitespace().nth(1)?.parse().ok()
}

fn basename(value: &[u8]) -> &[u8] {
    value.rsplit(|&byte| byte == b'/' || byte == b'\\').next().unwrap_or(value)
}

fn disappeared(error: &io::Error) -> bool {
    error.kind() == io::ErrorKind::NotFound || error.raw_os_error() == Some(3) // ESRCH
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::steam::test_support::Scratch;

    #[test]
    fn app_id_matches_only_a_whole_variable_and_prefix_is_exact() {
        let prefix = Path::new("/disk/compatdata/2622000");
        assert!(environment_is_game(b"SteamAppId=2622000\0", prefix));
        assert!(!environment_is_game(b"STEAM_COMPAT_APP_ID=26220000\0OTHER=SteamAppId=2622000\0", prefix));
        assert!(environment_is_game(b"WINEPREFIX=/disk/compatdata/2622000/pfx\0", prefix));
        assert!(!environment_is_game(b"WINEPREFIX=/disk/compatdata/2622000-other/pfx\0", prefix));
    }

    #[test]
    fn scans_wine_and_steam_helpers_without_treating_generic_arguments_as_launches() {
        let temp = Scratch::new();
        let proc_dir = &temp.0;
        let process = proc_dir.join("42");
        let uid = fs::metadata(proc_dir).unwrap().uid();
        temp.write("self/status", format!("Uid:\t{uid}\t{uid}\t{uid}\t{uid}\n"));
        temp.write("42/cmdline", b"/usr/bin/nvim\0AstralParty_CN.exe\0");
        temp.write("42/comm", b"nvim\n");
        assert_eq!(running(proc_dir, Path::new("/compat")), Some(false));
        fs::write(process.join("cmdline"), b"/tmp/proton/files/bin/wine64\0steam.exe\0").unwrap();
        fs::write(process.join("comm"), b"wine64\n").unwrap();
        assert_eq!(running(proc_dir, Path::new("/compat")), None);
        temp.write("42/environ", b"SteamGameId=2622000\0");
        assert_eq!(running(proc_dir, Path::new("/compat")), Some(true));
        fs::write(process.join("cmdline"), b"/tmp/proton/files/bin/wine64\0").unwrap();
        fs::write(process.join("comm"), b"AstralParty_INT\n").unwrap();
        fs::remove_file(process.join("environ")).unwrap();
        assert_eq!(running(proc_dir, Path::new("/compat")), Some(true));
        assert_eq!(running(&proc_dir.join("missing"), Path::new("/compat")), None);
    }
}
