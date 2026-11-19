use std::{fs, io, path::Path};

fn assemble_html(path: &Path) -> io::Result<String> {
    let html = fs::read_to_string(path)?;
    let mut out = String::with_capacity(html.len());
    for line in html.lines() {
        if let Some(name) = line.trim().strip_prefix("<!-- @include ").and_then(|s| s.strip_suffix(" -->")) {
            out.push_str(&assemble_html(&path.with_file_name(name))?);
        } else {
            out.push_str(line);
        }
        out.push('\n');
    }
    Ok(out)
}

fn copy_tree(src: &Path, dst: &Path) -> io::Result<()> {
    fs::create_dir_all(dst)?;
    for entry in fs::read_dir(src)? {
        let entry = entry?;
        let target = dst.join(entry.file_name());
        if entry.file_type()?.is_dir() {
            copy_tree(&entry.path(), &target)?;
        } else {
            fs::copy(entry.path(), target)?;
        }
    }
    Ok(())
}

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let source = Path::new("../src/AstralParty.Toys/WebUI");
    let packed = Path::new("../src/AstralParty.Toys/PackedAssets");
    println!("cargo:rerun-if-changed={}", source.display());
    println!("cargo:rerun-if-changed={}", packed.display());
    let dest = Path::new("generated/webui");
    if dest.exists() { fs::remove_dir_all(dest)?; }
    copy_tree(source, dest)?;
    copy_tree(packed, &dest.join("game-assets"))?;
    fs::write(dest.join("index.html"), assemble_html(&source.join("index.html"))?)?;
    tauri_build::try_build(tauri_build::Attributes::new().app_manifest(
        tauri_build::AppManifest::new().commands(&["host_message"]),
    ))?;
    Ok(())
}
