mod steam;

use serde_json::{json, Value};
use std::{
    fs::{self, File},
    io::Write,
    path::{Path, PathBuf},
    process::{Command, Stdio},
    sync::Mutex,
};
use tauri::{Manager, WebviewWindowBuilder};

const STEAM_URI: &str = "steam://rungameid/2622000";

type Events = Vec<Value>;

fn managed(request: &Value) -> Result<Events, String> {
    let executable = std::env::current_exe().map_err(|e| e.to_string())?;
    let sidecar = executable.with_file_name("managed").join("AstralParty.Toys.LinuxHost");
    let mut child = Command::new(&sidecar)
        .stdin(Stdio::piped()).stdout(Stdio::piped()).stderr(Stdio::piped())
        .spawn().map_err(|e| format!("{}: {e}", sidecar.display()))?;
    let sent = {
        let mut input = child.stdin.take().expect("piped stdin");
        serde_json::to_writer(&mut input, request).map_err(|e| e.to_string())
            .and_then(|()| input.write_all(b"\n").map_err(|e| e.to_string()))
    };
    let output = child.wait_with_output().map_err(|e| e.to_string())?;
    if !output.status.success() {
        let detail = String::from_utf8_lossy(&output.stderr);
        return Err(format!("Linux host ({}): {}", output.status, detail.trim()));
    }
    sent?;
    serde_json::from_slice(&output.stdout).map_err(|e| format!("Invalid Linux host response: {e}"))
}

fn write_lock(data: &Path) -> Result<File, String> {
    fs::create_dir_all(data).map_err(|e| e.to_string())?;
    let file = File::options().create(true).truncate(false).write(true).open(data.join("loader.lock"))
        .map_err(|e| e.to_string())?;
    file.try_lock().map_err(|e| format!("Another loader operation is active or the lock is unavailable: {e}"))?;
    Ok(file)
}

fn dispatch(app: &tauri::AppHandle, message: Value) -> Result<Events, String> {
    let kind = message["type"].as_str().ok_or("Missing message type")?;
    if !matches!(kind, "ready" | "gameProfileSetActive" | "launchGame" | "linuxInstallLoader" | "linuxUpdateLoader" | "linuxUninstallLoader") {
        return Err(format!("Unsupported native action: {kind}"));
    }
    let state = app.state::<Mutex<Option<PathBuf>>>();
    let mut selected = state.lock().map_err(|_| "Native session unavailable")?;
    let installations = steam::discover()?;
    let data = app.path().app_data_dir().map_err(|e| e.to_string())?;
    let game = match kind {
        "gameProfileSetActive" => {
            let id = message["id"].as_str().ok_or("Missing game profile")?;
            Some(steam::find_installation(&installations, Path::new(id))?)
        }
        _ => match selected.as_deref() {
            Some(path) => match steam::find_installation(&installations, path) {
                Ok(game) => Some(game),
                Err(_) if kind == "ready" => None,
                Err(error) => return Err(error),
            },
            None if kind == "ready" => installations.iter().find(|game| game.edition == "cn").or_else(|| installations.first()),
            None => return Err("Select a detected game first".into()),
        },
    };
    *selected = game.map(|game| game.game_dir.clone());
    let mut request = json!({
        "type": "ready", "gameDirectory": game.map(|g| g.game_dir.as_path()).unwrap_or(Path::new("")),
        "edition": game.map(|g| g.edition.as_str()).unwrap_or(""), "dataDirectory": data,
        "gameDirectories": installations.iter().map(|g| json!({"id":g.game_dir,"directory":g.game_dir,"edition":g.edition})).collect::<Vec<_>>(),
        "gameRunning": game.and_then(|g| g.running()),
    });
    if kind == "launchGame" {
        let game = game.ok_or("Game is not installed")?;
        if message.get("mode").and_then(Value::as_str).is_some_and(|mode| mode != "steam") || message["bypassSteam"] == true {
            return Err("Only the normal Steam launch mode is supported".into());
        }
        if game.running() == Some(true) { return Err("The game is already running".into()); }
        let status = Command::new("xdg-open").arg(STEAM_URI).status().map_err(|e| e.to_string())?;
        if !status.success() { return Err(format!("Steam URL handler exited with {status}")); }
        return Ok(vec![json!({"type":"toast", "message":"已交给 Steam 启动游戏。"})]);
    }
    let mut notices = Events::new();
    if matches!(kind, "linuxInstallLoader" | "linuxUpdateLoader" | "linuxUninstallLoader") {
        let game = game.ok_or("Game is not installed")?;
        if message["id"].as_str().map(Path::new) != Some(game.game_dir.as_path()) {
            return Err("The selected installation changed; refresh before confirming".into());
        }
        let _lock = write_lock(&data)?;
        game.require_stopped()?;
        request["type"] = kind.into();
        if kind == "linuxUninstallLoader" {
            notices.extend(managed(&request)?);
            match game.restore_override(&data) {
                Ok(None) => (),
                Ok(Some(warning)) | Err(warning) => notices.push(json!({
                    "type":"toast", "message":format!("加载器文件已卸载，DLL 覆盖需确认：{warning}")
                })),
            }
        } else {
            let change = game.ensure_override(&data)?;
            let result = game.require_stopped().and_then(|()| managed(&request));
            match result {
                Err(mut reason) => {
                    if change.added {
                        match game.restore_override(&data) {
                            Ok(None) => (),
                            Ok(Some(warning)) | Err(warning) => reason.push_str(&format!("; registry: {warning}")),
                        }
                    }
                    return Err(reason);
                }
                Ok(events) => notices.extend(events),
            }
            if let Some(warning) = change.warning {
                notices.push(json!({"type":"toast","message":warning}));
            }
        }
        request["type"] = "ready".into();
    }
    let mut events = managed(&request).map_err(|error| {
        if notices.is_empty() { error } else {
            let outcome = notices.iter().filter_map(|notice| notice["message"].as_str()).collect::<Vec<_>>().join(" ");
            format!("{outcome} Could not refresh the overview: {error}")
        }
    })?;
    if let Some(warning) = game.and_then(|game| game.loader_warning()) {
        for event in &mut events {
            if event["type"] == "linuxStatus" {
                let previous = event["payload"]["warning"].as_str().unwrap_or("");
                event["payload"]["warning"] = if previous.is_empty() { warning.clone() } else { format!("{previous}\n{warning}") }.into();
            }
        }
    }
    events.append(&mut notices);
    Ok(events)
}

#[tauri::command]
async fn host_message(app: tauri::AppHandle, message: Value) -> Result<Events, String> {
    tauri::async_runtime::spawn_blocking(move || dispatch(&app, message)).await.map_err(|e| e.to_string())?
}

fn main() {
    tauri::Builder::default()
        .manage(Mutex::new(None::<PathBuf>))
        .setup(|app| {
            WebviewWindowBuilder::from_config(app, &app.config().app.windows[0])?
                .on_navigation(|url| matches!((url.scheme(), url.host_str()), ("tauri", Some("localhost")) | ("http" | "https", Some("tauri.localhost"))))
                .on_new_window(|_, _| tauri::webview::NewWindowResponse::Deny)
                .build()?;
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![host_message])
        .run(tauri::generate_context!())
        .expect("cannot start the native Linux window");
}
