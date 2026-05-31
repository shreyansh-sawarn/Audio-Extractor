use std::io::{BufRead, BufReader};
use std::path::{Path, PathBuf};
use std::process::{Command, Stdio};
use std::thread;
use tauri::{Emitter, Window};

#[derive(serde::Serialize, Clone)]
struct ProgressPayload {
    id: String,
    progress: u32,
}

#[derive(serde::Serialize)]
struct FileDetails {
    #[serde(rename = "fileName")]
    file_name: String,
    #[serde(rename = "fileSize")]
    file_size: u64,
    #[serde(rename = "fileExtension")]
    file_extension: String,
    #[serde(rename = "totalDuration")]
    total_duration: String,
    #[serde(rename = "totalDurationSeconds")]
    total_duration_seconds: f64,
}

fn resolve_ffmpeg_path() -> PathBuf {
    // 1. Try global path check
    let test_global = Command::new("ffmpeg")
        .arg("-version")
        .stdout(Stdio::null())
        .stderr(Stdio::null())
        .status();

    if let Ok(status) = test_global {
        if status.success() {
            return PathBuf::from("ffmpeg");
        }
    }

    // 2. Try default winget package directory fallback
    if let Some(local_appdata) = std::env::var_os("LOCALAPPDATA") {
        let winget_packages = Path::new(&local_appdata).join("Microsoft").join("WinGet").join("Packages");
        if let Ok(entries) = std::fs::read_dir(&winget_packages) {
            for entry in entries.filter_map(Result::ok) {
                let path = entry.path();
                if path.is_dir() && path.file_name().map_or(false, |name| name.to_string_lossy().contains("Gyan.FFmpeg")) {
                    if let Ok(subentries) = std::fs::read_dir(&path) {
                        for subentry in subentries.filter_map(Result::ok) {
                            let bin_path = subentry.path().join("bin").join("ffmpeg.exe");
                            if bin_path.exists() {
                                return bin_path;
                            }
                        }
                    }
                }
            }
        }
    }

    PathBuf::from("ffmpeg")
}

#[tauri::command]
fn check_ffmpeg() -> bool {
    let ffmpeg_path = resolve_ffmpeg_path();
    Command::new(&ffmpeg_path)
        .arg("-version")
        .stdout(Stdio::null())
        .stderr(Stdio::null())
        .status()
        .map(|s| s.success())
        .unwrap_or(false)
}

#[tauri::command]
fn select_files() -> Vec<String> {
    rfd::FileDialog::new()
        .add_filter("Video Files", &["mp4", "mkv", "avi", "mov", "webm", "m4v"])
        .pick_files()
        .unwrap_or_default()
        .into_iter()
        .map(|p| p.to_string_lossy().into_owned())
        .collect()
}

#[tauri::command]
fn select_folder(default_path: Option<String>) -> Option<String> {
    let mut dialog = rfd::FileDialog::new();
    if let Some(ref path) = default_path {
        dialog = dialog.set_directory(path);
    }
    dialog.pick_folder().map(|p| p.to_string_lossy().into_owned())
}

#[tauri::command]
fn get_file_details(file_path: String) -> Result<FileDetails, String> {
    let path = Path::new(&file_path);
    if !path.exists() {
        return Err("File does not exist".into());
    }

    let file_name = path.file_stem()
        .map(|s| s.to_string_lossy().into_owned())
        .unwrap_or_default();
    
    let file_extension = path.extension()
        .map(|s| s.to_string_lossy().to_string().to_uppercase())
        .unwrap_or_default();

    let file_size = std::fs::metadata(path)
        .map(|m| m.len())
        .unwrap_or(0);

    let mut duration_str = "00:00:00".to_string();
    let mut duration_secs = 0.0;

    let ffmpeg_path = resolve_ffmpeg_path();
    let output = Command::new(&ffmpeg_path)
        .args(&["-hide_banner", "-i", &file_path])
        .output();

    if let Ok(out) = output {
        let stderr_str = String::from_utf8_lossy(&out.stderr);
        if let Some(idx) = stderr_str.find("Duration: ") {
            let start = idx + "Duration: ".len();
            if let Some(end) = stderr_str[start..].find(",") {
                let duration_part = stderr_str[start..start+end].trim();
                let parts: Vec<&str> = duration_part.split(':').collect();
                if parts.len() == 3 {
                    let hrs: f64 = parts[0].parse().unwrap_or(0.0);
                    let mins: f64 = parts[1].parse().unwrap_or(0.0);
                    let secs: f64 = parts[2].parse().unwrap_or(0.0);
                    duration_secs = hrs * 3600.0 + mins * 60.0 + secs;
                    duration_str = duration_part.split('.').next().unwrap_or(duration_part).to_string();
                }
            }
        }
    }

    Ok(FileDetails {
        file_name,
        file_size,
        file_extension,
        total_duration: duration_str,
        total_duration_seconds: duration_secs,
    })
}

fn resolve_output_dir(raw_path: &str) -> PathBuf {
    let p = Path::new(raw_path);
    if p.is_absolute() {
        return p.to_path_buf();
    }
    if let Some(mut path) = dirs::audio_dir() {
        path.push(raw_path);
        return path;
    }
    if let Some(mut path) = dirs::document_dir() {
        path.push(raw_path);
        return path;
    }
    p.to_path_buf()
}

fn create_unique_output_path(input_path: &str, output_dir: &Path, format: &str, template: &str, bitrate: &str) -> PathBuf {
    let input_p = Path::new(input_path);
    let stem = input_p.file_stem().map(|s| s.to_string_lossy().into_owned()).unwrap_or_default();
    let date = chrono::Local::now().format("%Y%m%d").to_string();

    let mut filename = template.replace("[Name]", &stem)
        .replace("[Format]", format)
        .replace("[Bitrate]", bitrate)
        .replace("[Date]", &date);

    if filename.is_empty() {
        filename = stem.clone();
    }

    let mut out_path = output_dir.join(format!("{}.{}", filename, format.to_lowercase()));
    let mut counter = 1;

    while out_path.exists() {
        out_path = output_dir.join(format!("{}_{}.{}", filename, counter, format.to_lowercase()));
        counter += 1;
    }

    out_path
}

#[tauri::command]
fn convert_file(
    window: Window,
    id: String,
    input_path: String,
    target_folder: String,
    format: String,
    bitrate: String,
    is_mono: bool,
    normalize: bool,
    start_time: String,
    end_time: String,
    name_template: String,
    total_duration_seconds: f64,
) -> Result<String, String> {
    let output_dir = resolve_output_dir(&target_folder);
    if let Err(e) = std::fs::create_dir_all(&output_dir) {
        return Err(format!("Failed to create output directory: {}", e));
    }

    let output_path = create_unique_output_path(&input_path, &output_dir, &format, &name_template, &bitrate);
    let output_path_str = output_path.to_string_lossy().into_owned();

    let mut args = vec!["-hide_banner".to_string(), "-y".to_string()];
    
    if !start_time.trim().is_empty() && start_time.trim() != "00:00:00" {
        args.push("-ss".to_string());
        args.push(start_time.trim().to_string());
    }

    if !end_time.trim().is_empty() && end_time.trim() != "00:00:00" {
        args.push("-to".to_string());
        args.push(end_time.trim().to_string());
    }

    args.push("-i".to_string());
    args.push(input_path.clone());
    args.push("-vn".to_string());

    let ext_lower = format.to_lowercase();
    match ext_lower.as_str() {
        "m4a" => {
            args.push("-c:a".to_string());
            args.push("aac".to_string());
            args.push("-b:a".to_string());
            args.push(bitrate.clone());
        }
        "wav" => {
            args.push("-c:a".to_string());
            args.push("pcm_s16le".to_string());
        }
        "flac" => {
            args.push("-c:a".to_string());
            args.push("flac".to_string());
        }
        _ => {
            args.push("-c:a".to_string());
            args.push("libmp3lame".to_string());
            args.push("-b:a".to_string());
            args.push(bitrate.clone());
        }
    }

    if is_mono {
        args.push("-ac".to_string());
        args.push("1".to_string());
    }

    if normalize {
        args.push("-filter:a".to_string());
        args.push("loudnorm".to_string());
    }

    args.push(output_path_str.clone());

    let ffmpeg_path = resolve_ffmpeg_path();
    let mut child = Command::new(&ffmpeg_path)
        .args(&args)
        .stdout(Stdio::null())
        .stderr(Stdio::piped())
        .spawn()
        .map_err(|e| format!("Failed to spawn ffmpeg: {}", e))?;

    let stderr = child.stderr.take().ok_or("Failed to open stderr")?;
    let id_clone = id.clone();
    
    // Spawn thread to read and parse stderr output for progress events
    thread::spawn(move || {
        let reader = BufReader::new(stderr);
        for line in reader.lines() {
            if let Ok(l) = line {
                if let Some(pos) = l.find("time=") {
                    let time_str = &l[pos + 5..];
                    if let Some(end_pos) = time_str.find(char::is_whitespace).or(Some(time_str.len())) {
                        let time_val = time_str[..end_pos].trim();
                        let parts: Vec<&str> = time_val.split(':').collect();
                        if parts.len() == 3 {
                            let hrs: f64 = parts[0].parse().unwrap_or(0.0);
                            let mins: f64 = parts[1].parse().unwrap_or(0.0);
                            let secs: f64 = parts[2].parse().unwrap_or(0.0);
                            let current_secs = hrs * 3600.0 + mins * 60.0 + secs;
                            if total_duration_seconds > 0.0 {
                                let progress = ((current_secs / total_duration_seconds) * 100.0) as u32;
                                let progress = std::cmp::min(progress, 100);
                                let _ = window.emit("progress", ProgressPayload { id: id_clone.clone(), progress });
                            }
                        }
                    }
                }
            }
        }
    });

    let status = child.wait().map_err(|e| e.to_string())?;
    if status.success() {
        Ok(output_path_str)
    } else {
        Err("ffmpeg conversion failed. Check settings/input.".into())
    }
}

#[tauri::command]
fn open_folder(path: String) {
    let p = resolve_output_dir(&path);
    let _ = open::that(p);
}

#[tauri::command]
fn play_file(path: String) {
    let _ = open::that(path);
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
  tauri::Builder::default()
    .setup(|app| {
      if cfg!(debug_assertions) {
        app.handle().plugin(
          tauri_plugin_log::Builder::default()
            .level(log::LevelFilter::Info)
            .build(),
        )?;
      }
      Ok(())
    })
    .invoke_handler(tauri::generate_handler![
        check_ffmpeg,
        select_files,
        select_folder,
        get_file_details,
        convert_file,
        open_folder,
        play_file
    ])
    .run(tauri::generate_context!())
    .expect("error while running tauri application");
}
