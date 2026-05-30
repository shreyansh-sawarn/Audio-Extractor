import React, { useState, useEffect, useRef } from 'react';
import type { QueueItem, AppConfig } from './types';
import './App.css';

// Tauri API imports
import { invoke } from '@tauri-apps/api/core';
import { listen } from '@tauri-apps/api/event';
import { getCurrentWindow } from '@tauri-apps/api/window';

// FFmpeg WASM imports
import { FFmpeg } from '@ffmpeg/ffmpeg';
import { fetchFile, toBlobURL } from '@ffmpeg/util';

// Check if running inside Tauri desktop shell
const isTauri = typeof window !== 'undefined' && '__TAURI__' in window;

export default function App() {
  const [config, setConfig] = useState<AppConfig>({
    format: 'MP3',
    bitrate: '256k',
    isMono: false,
    normalize: false,
    parallelTasks: 2,
    filenameTemplate: '[Name]',
    postAction: 'None',
    targetFolderPath: 'Music\\AudioExtractor',
    isDarkMode: true,
  });

  const [items, setItems] = useState<QueueItem[]>([]);
  const [isConverting, setIsConverting] = useState(false);
  const [statusMessage, setStatusMessage] = useState('Drop video files to begin.');
  const [isDragOver, setIsDragOver] = useState(false);
  const [isFfmpegAvailable, setIsFfmpegAvailable] = useState(true);
  const [playingItemId, setPlayingItemId] = useState<string | null>(null);

  const fileInputRef = useRef<HTMLInputElement>(null);
  const dragStartId = useRef<string | null>(null);
  const ffmpegWasmRef = useRef<FFmpeg | null>(null);
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const dragCounter = useRef(0);
  const isReordering = useRef(false); // true while a queue-item reorder drag is active

  // Apply visual theme to document body
  useEffect(() => {
    if (config.isDarkMode) {
      document.body.classList.remove('light-theme');
    } else {
      document.body.classList.add('light-theme');
    }
  }, [config.isDarkMode]);

  // Check FFmpeg availability & register Tauri listeners
  useEffect(() => {
    if (isTauri) {
      // Check FFmpeg
      invoke<boolean>('check_ffmpeg').then(avail => {
        setIsFfmpegAvailable(avail);
        if (!avail) {
          setStatusMessage('Warning: FFmpeg is missing from your system PATH.');
        }
      });

      // 1. Progress updates
      const unlistenProgress = listen<{ id: string; progress: number }>('progress', (event) => {
        const { id, progress } = event.payload;
        setItems(prev => prev.map(item => item.id === id ? { ...item, progress } : item));
      });

      // 2. Native File drops and drag alerts
      const unlistenDrop = getCurrentWindow().onDragDropEvent((event) => {
        if (event.payload.type === 'enter') {
          setIsDragOver(true);
        } else if (event.payload.type === 'leave') {
          setIsDragOver(false);
        } else if (event.payload.type === 'drop') {
          setIsDragOver(false);
          addLocalPathsToList(event.payload.paths);
        }
      });

      return () => {
        unlistenProgress.then(un => un());
        unlistenDrop.then(un => un());
      };
    }
  }, []);

  // Format seconds to hh:mm:ss
  const formatSeconds = (totalSeconds: number): string => {
    const hrs = Math.floor(totalSeconds / 3600);
    const mins = Math.floor((totalSeconds % 3600) / 60);
    const secs = Math.floor(totalSeconds % 60);
    return `${hrs.toString().padStart(2, '0')}:${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
  };

  // Helper: Parse hh:mm:ss or mm:ss or seconds into double
  const tryParseTime = (input: string): number | null => {
    const trimmed = input.trim();
    if (!trimmed) return null;

    if (!isNaN(Number(trimmed))) {
      const val = Number(trimmed);
      return val >= 0 ? val : null;
    }

    const parts = trimmed.split(':');
    if (parts.length === 2) {
      const min = parseInt(parts[0], 10);
      const sec = parseFloat(parts[1]);
      if (min >= 0 && sec >= 0 && sec < 60) {
        return min * 60 + sec;
      }
    } else if (parts.length === 3) {
      const hr = parseInt(parts[0], 10);
      const min = parseInt(parts[1], 10);
      const sec = parseFloat(parts[2]);
      if (hr >= 0 && min >= 0 && min < 60 && sec >= 0 && sec < 60) {
        return hr * 3600 + min * 60 + sec;
      }
    }

    return null;
  };

  // Live validation logic for Start and End Trim Inputs
  const validateItemTime = (item: QueueItem, start: string, end: string, totalSec: number): Partial<QueueItem> => {
    const startSec = tryParseTime(start);
    if (start && startSec === null) {
      return {
        isTimeRangeValid: false,
        timeValidationError: 'Start time format invalid (use hh:mm:ss, mm:ss, or seconds).',
      };
    }

    const endSec = tryParseTime(end);
    if (end && endSec === null) {
      return {
        isTimeRangeValid: false,
        timeValidationError: 'End time format invalid (use hh:mm:ss, mm:ss, or seconds).',
      };
    }

    const finalStart = startSec || 0;
    const finalEnd = endSec !== null ? endSec : totalSec >= 0 ? totalSec : Infinity;

    if (finalStart > finalEnd) {
      return {
        isTimeRangeValid: false,
        timeValidationError: 'Start time cannot be after End time.',
      };
    }

    if (totalSec >= 0) {
      if (finalStart > totalSec) {
        return {
          isTimeRangeValid: false,
          timeValidationError: `Start time exceeds media length (${item.totalDuration}).`,
        };
      }
      if (finalEnd > totalSec && endSec !== null) {
        return {
          isTimeRangeValid: false,
          timeValidationError: `End time exceeds media length (${item.totalDuration}).`,
        };
      }
    }

    return {
      isTimeRangeValid: true,
      timeValidationError: '',
    };
  };

  // Retrieve duration of a file in browser using HTML5 Video Element
  const fetchWebVideoDuration = (file: File): Promise<number> => {
    return new Promise((resolve) => {
      const video = document.createElement('video');
      video.preload = 'metadata';
      video.onloadedmetadata = () => {
        resolve(video.duration);
      };
      video.onerror = () => {
        resolve(0);
      };
      video.src = URL.createObjectURL(file);
    });
  };

  // Add native file paths to list (Tauri mode)
  const addLocalPathsToList = async (paths: string[]) => {
    const newItems: QueueItem[] = [];
    const duplicates: string[] = [];

    for (const path of paths) {
      const ext = path.split('.').pop()?.toLowerCase() || '';
      const supported = ['webm', 'mp4', 'mkv', 'mov', 'avi', 'm4v'];
      if (!supported.includes(ext)) continue;

      if (items.some(item => item.filePath === path)) {
        duplicates.push(path);
        continue;
      }

      try {
        const details = await invoke<{
          fileName: string;
          fileSize: number;
          fileExtension: string;
          totalDuration: string;
          totalDurationSeconds: number;
        }>('get_file_details', { filePath: path });

        const id = Math.random().toString(36).substring(2, 9);
        newItems.push({
          id,
          fileName: details.fileName,
          fileSize: details.fileSize,
          fileExtension: details.fileExtension,
          containerDirectory: path.substring(0, path.lastIndexOf('\\')) || 'Local',
          filePath: path,
          startTime: '00:00:00',
          endTime: details.totalDuration,
          totalDuration: details.totalDuration,
          totalDurationSeconds: details.totalDurationSeconds,
          isTimeRangeValid: true,
          timeValidationError: '',
          status: 'Queued',
          errorMessage: null,
          progress: 0,
          outputPath: null
        });
      } catch (err) {
        console.error("Failed to query file details natively:", err);
      }
    }

    if (newItems.length > 0) {
      setItems(prev => [...prev, ...newItems]);
      setStatusMessage(`Added ${newItems.length} file(s).`);
    } else if (duplicates.length > 0) {
      setStatusMessage('Files already present in list.');
    }
  };

  // Add standard web files to list (Web Simulator mode)
  const addFilesToList = async (files: FileList | File[]) => {
    const newItems: QueueItem[] = [];
    const duplicates: string[] = [];

    for (let i = 0; i < files.length; i++) {
      const file = files[i];
      const ext = file.name.split('.').pop()?.toLowerCase() || '';
      const supported = ['webm', 'mp4', 'mkv', 'mov', 'avi', 'm4v'];
      if (!supported.includes(ext)) continue;

      if (items.some(item => item.filePath === file.name || item.fileName === file.name.replace(/\.[^/.]+$/, ""))) {
        duplicates.push(file.name);
        continue;
      }

      const id = Math.random().toString(36).substring(2, 9);
      const nameWithoutExt = file.name.replace(/\.[^/.]+$/, "");
      const extUpper = ext.toUpperCase();

      const item: QueueItem = {
        id,
        file,
        fileName: nameWithoutExt,
        fileSize: file.size,
        fileExtension: extUpper,
        containerDirectory: 'Browser Upload',
        filePath: file.name,
        startTime: '00:00:00',
        endTime: '00:00:00',
        totalDuration: '00:00:00',
        totalDurationSeconds: -1,
        isTimeRangeValid: true,
        timeValidationError: '',
        status: 'Queued',
        errorMessage: null,
        progress: 0,
        outputPath: null
      };

      newItems.push(item);
    }

    if (newItems.length > 0) {
      setItems(prev => [...prev, ...newItems]);
      setStatusMessage(`Added ${newItems.length} file(s).`);

      newItems.forEach(async (item) => {
        if (item.file) {
          const durationSec = await fetchWebVideoDuration(item.file);
          const durationStr = formatSeconds(durationSec);
          setItems(prev => prev.map(p => {
            if (p.id === item.id) {
              const validation = validateItemTime(p, p.startTime, durationStr, durationSec);
              return {
                ...p,
                totalDuration: durationStr,
                totalDurationSeconds: durationSec,
                endTime: durationStr,
                ...validation
              };
            }
            return p;
          }));
        }
      });
    } else if (duplicates.length > 0) {
      setStatusMessage('Files already present in list.');
    }
  };

  // Safe HTML5 Drag/Drop Counter listeners
  const handleDragEnter = (e: React.DragEvent) => {
    e.preventDefault();
    // Don't activate the file-drop overlay when reordering queue items
    if (isConverting || isReordering.current) return;
    dragCounter.current++;
    if (!isTauri) {
      setIsDragOver(true);
    }
  };

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    if (isReordering.current) return;
    dragCounter.current--;
    if (dragCounter.current <= 0 && !isTauri) {
      setIsDragOver(false);
      dragCounter.current = 0;
    }
  };

  const handleDrop = (e: React.DragEvent) => {
    // If a queue item reorder is in progress, ignore this as a file drop
    if (isReordering.current) {
      e.preventDefault();
      return;
    }
    e.preventDefault();
    dragCounter.current = 0;
    setIsDragOver(false);
    if (!isConverting && e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      addFilesToList(e.dataTransfer.files);
    }
  };

  const removeQueueItem = (id: string) => {
    if (isConverting) return;
    if (playingItemId === id && audioRef.current) {
      audioRef.current.pause();
      setPlayingItemId(null);
    }
    setItems(prev => prev.filter(item => item.id !== id));
  };

  const updateStartTime = (id: string, val: string) => {
    setItems(prev => prev.map(item => {
      if (item.id === id) {
        const validation = validateItemTime(item, val, item.endTime, item.totalDurationSeconds);
        return { ...item, startTime: val, ...validation };
      }
      return item;
    }));
  };

  const updateEndTime = (id: string, val: string) => {
    setItems(prev => prev.map(item => {
      if (item.id === id) {
        const validation = validateItemTime(item, item.startTime, val, item.totalDurationSeconds);
        return { ...item, endTime: val, ...validation };
      }
      return item;
    }));
  };

  // Reordering handlers
  const handleItemDragStart = (e: React.DragEvent, id: string) => {
    const target = e.target as HTMLElement;
    if (target.tagName === 'INPUT' || target.tagName === 'BUTTON' || target.closest('.trim-fields')) {
      e.preventDefault();
      return;
    }
    dragStartId.current = id;
    isReordering.current = true;
    // Reset the file-drop overlay counter so it won't flicker on
    dragCounter.current = 0;
    setIsDragOver(false);
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('text/plain', id); // Explicitly set data to trigger HTML5 drop sequence on all runtimes!
  };

  const handleItemDragEnd = () => {
    // Always clean up reorder state, even if drop was cancelled
    isReordering.current = false;
    dragStartId.current = null;
    dragCounter.current = 0;
    setIsDragOver(false);
  };

  const handleItemDragOver = (e: React.DragEvent, _id: string) => {
    e.preventDefault();
    e.stopPropagation(); // prevent parent file-drop zone from reacting
    e.dataTransfer.dropEffect = 'move';
  };

  const handleItemDrop = (e: React.DragEvent, dropTargetId: string) => {
    e.preventDefault();
    e.stopPropagation(); // prevent parent onDrop (file import) from firing
    const draggedId = dragStartId.current;
    isReordering.current = false;
    dragStartId.current = null;
    dragCounter.current = 0;
    setIsDragOver(false);
    if (!draggedId || draggedId === dropTargetId) return;

    setItems(prev => {
      const list = [...prev];
      const dragIdx = list.findIndex(item => item.id === draggedId);
      const dropIdx = list.findIndex(item => item.id === dropTargetId);
      if (dragIdx >= 0 && dropIdx >= 0) {
        const [draggedItem] = list.splice(dragIdx, 1);
        list.splice(dropIdx, 0, draggedItem);
      }
      return list;
    });
  };

  // Directory picker (Tauri native)
  const handleBrowseFolder = async () => {
    if (isTauri) {
      const folder = await invoke<string | null>('select_folder', { defaultPath: config.targetFolderPath });
      if (folder) {
        setConfig(prev => ({ ...prev, targetFolderPath: folder }));
      }
    }
  };

  // File adder (handles both Tauri native picker and Web File API)
  const handleAddFilesTrigger = async () => {
    if (isTauri) {
      const selected = await invoke<string[]>('select_files');
      if (selected && selected.length > 0) {
        addLocalPathsToList(selected);
      }
    } else {
      fileInputRef.current?.click();
    }
  };

  // Play audio file
  const handlePlayAudio = (item: QueueItem) => {
    if (isTauri && item.outputPath) {
      invoke('play_file', { path: item.outputPath });
    } else if (item.outputPath) {
      // Toggle play/pause on web
      if (playingItemId === item.id) {
        if (audioRef.current) {
          audioRef.current.pause();
        }
        setPlayingItemId(null);
      } else {
        // Stop currently playing
        if (audioRef.current) {
          audioRef.current.pause();
        }
        
        const audio = new Audio(item.outputPath);
        audioRef.current = audio;
        setPlayingItemId(item.id);

        audio.play().catch(err => {
          alert("Playback error: " + err.message);
          setPlayingItemId(null);
        });

        audio.onended = () => {
          setPlayingItemId(null);
        };
      }
    }
  };

  // Open output directory in Explorer (Tauri)
  const handleOpenFolder = (item: QueueItem) => {
    if (isTauri && item.outputPath) {
      invoke('open_folder', { path: config.targetFolderPath });
    }
  };

  // Helper to initialize and load ffmpeg.wasm for browser
  const initFfmpegWasm = async (): Promise<FFmpeg> => {
    if (ffmpegWasmRef.current) return ffmpegWasmRef.current;
    
    setStatusMessage('Loading FFmpeg WebAssembly compiler (approx. 30MB)...');
    const ffmpeg = new FFmpeg();
    const baseURL = 'https://unpkg.com/@ffmpeg/core@0.12.6/dist/esm';
    
    await ffmpeg.load({
      coreURL: await toBlobURL(`${baseURL}/ffmpeg-core.js`, 'text/javascript'),
      wasmURL: await toBlobURL(`${baseURL}/ffmpeg-core.wasm`, 'application/wasm'),
    });

    ffmpegWasmRef.current = ffmpeg;
    setStatusMessage('FFmpeg WebAssembly loaded successfully.');
    return ffmpeg;
  };

  // Main controller queue runner (handles WebAssembly ffmpeg and real Tauri spawns)
  const startConversions = async () => {
    setIsConverting(true);
    setStatusMessage('Converting queue...');
    
    const pending: QueueItem[] = items.map(item => ({
      ...item,
      status: item.status === 'Done' ? 'Done' : 'Queued',
      progress: item.status === 'Done' ? 100 : 0
    }));

    setItems(pending);

    const validItems = pending.filter(i => i.isTimeRangeValid && i.status !== 'Done');
    if (validItems.length === 0) {
      setIsConverting(false);
      setStatusMessage('No valid files queued.');
      return;
    }

    // Initialize WASM toolchain on web
    let ffmpegWasm: FFmpeg | null = null;
    if (!isTauri) {
      try {
        ffmpegWasm = await initFfmpegWasm();
      } catch (err: any) {
        setIsConverting(false);
        setStatusMessage('Failed to initialize browser WebAssembly compiler.');
        alert('WebAssembly initialization failed: ' + err.message);
        return;
      }
    }

    const maxParallel = isTauri ? config.parallelTasks : 1; // ffmpeg.wasm executes sequentially to prevent WebAssembly stack crashes
    const itemsToProcess = [...validItems];
    const activeTasks: Promise<void>[] = [];

    const processItem = async (item: QueueItem): Promise<void> => {
      // Set to converting
      setItems(prev => prev.map(p => p.id === item.id ? { ...p, status: 'Converting', progress: 0 } : p));
      
      if (isTauri) {
        try {
          const outPath = await invoke<string>('convert_file', {
            window: getCurrentWindow(),
            id: item.id,
            inputPath: item.filePath,
            targetFolder: config.targetFolderPath,
            format: config.format,
            bitrate: config.bitrate,
            isMono: config.isMono,
            normalize: config.normalize,
            startTime: item.startTime,
            endTime: item.endTime,
            nameTemplate: config.filenameTemplate,
            totalDurationSeconds: item.totalDurationSeconds
          });

          setItems(prev => prev.map(p => p.id === item.id ? {
            ...p,
            status: 'Done',
            outputPath: outPath,
            progress: 100
          } : p));
        } catch (err: any) {
          setItems(prev => prev.map(p => p.id === item.id ? {
            ...p,
            status: 'Failed',
            errorMessage: err.toString(),
            progress: 0
          } : p));
        }
      } else {
        // Run conversion natively in browser via ffmpeg.wasm!
        if (item.fileSize > 150 * 1024 * 1024) {
          setItems(prev => prev.map(p => p.id === item.id ? {
            ...p,
            status: 'Failed',
            errorMessage: 'File exceeds 150MB browser limit. Please download our Desktop App to extract heavy files!',
            progress: 0
          } : p));
          return;
        }

        const ffmpeg = ffmpegWasm;
        if (!ffmpeg || !item.file) return;

        const progressHandler = ({ progress }: { progress: number }) => {
          setItems(prev => prev.map(p => p.id === item.id ? { ...p, progress: Math.floor(progress * 100) } : p));
        };
        ffmpeg.on('progress', progressHandler);

        try {
          // Write input file to in-memory virtual filesystem
          const fileData = await fetchFile(item.file);
          await ffmpeg.writeFile(item.filePath, fileData);

          const args = [];
          if (item.startTime && item.startTime !== '00:00:00') {
            args.push('-ss', item.startTime);
          }
          if (item.endTime && item.endTime !== item.totalDuration) {
            args.push('-to', item.endTime);
          }
          args.push('-i', item.filePath);
          args.push('-vn');

          const ext = config.format.toLowerCase();
          if (ext === 'm4a') {
            args.push('-c:a', 'aac', '-b:a', config.bitrate);
          } else if (ext === 'wav') {
            args.push('-c:a', 'pcm_s16le');
          } else if (ext === 'flac') {
            args.push('-c:a', 'flac');
          } else {
            args.push('-c:a', 'libmp3lame', '-b:a', config.bitrate);
          }

          if (config.isMono) {
            args.push('-ac', '1');
          }
          if (config.normalize) {
            args.push('-filter:a', 'loudnorm');
          }

          const outputName = `${item.fileName}.${ext}`;
          args.push(outputName);

          await ffmpeg.exec(args);

          // Read generated output binary file
          const outputData = await ffmpeg.readFile(outputName);
          const blob = new Blob([new Uint8Array(outputData as any)], { type: `audio/${ext}` });
          const url = URL.createObjectURL(blob);

          setItems(prev => prev.map(p => p.id === item.id ? {
            ...p,
            status: 'Done',
            outputPath: url,
            progress: 100
          } : p));

          // Clear references
          await ffmpeg.deleteFile(item.filePath);
          await ffmpeg.deleteFile(outputName);
        } catch (err: any) {
          setItems(prev => prev.map(p => p.id === item.id ? {
            ...p,
            status: 'Failed',
            errorMessage: err.toString(),
            progress: 0
          } : p));
        } finally {
          ffmpeg.off('progress', progressHandler);
        }
      }
    };

    const runQueue = async () => {
      while (itemsToProcess.length > 0) {
        if (activeTasks.length < maxParallel) {
          const nextItem = itemsToProcess.shift();
          if (nextItem) {
            const task = processItem(nextItem).then(() => {
              activeTasks.splice(activeTasks.indexOf(task), 1);
            });
            activeTasks.push(task);
          }
        } else {
          await Promise.race(activeTasks);
        }
      }
      await Promise.all(activeTasks);
    };

    await runQueue();

    setIsConverting(false);
    setStatusMessage('Conversion batch complete.');
  };

  const stopConversion = () => {
    setIsConverting(false);
    setStatusMessage('Conversion cancelled.');
    setItems(prev => prev.map(item => item.status === 'Converting' ? { ...item, status: 'Cancelled' } : item));
  };

  return (
    <div className="app-container" onDragEnter={handleDragEnter}>
      
      {/* Header */}
      <header className="app-header">
        <div className="logo-section">
          <span className="logo-text">Audio Extractor & Converter</span>
          <span className="version-badge">v2.0</span>
        </div>
        <div className="theme-toggle-section">
          <span>Dark Mode</span>
          <label className="theme-switch">
            <input 
              type="checkbox" 
              checked={config.isDarkMode} 
              onChange={() => setConfig(prev => ({ ...prev, isDarkMode: !prev.isDarkMode }))}
            />
            <span className="slider"></span>
          </label>
        </div>
      </header>

      {/* Cross-Platform Heavy Weight App Download Pitch (Web Mode only) */}
      {!isTauri && (
        <div className="pitch-banner">
          🚀 <b>Converting large files (over 150MB) or need parallel batch conversions?</b>{' '}
          <a href="https://github.com/shreyansh-sawarn/Audio-Extractor/releases" target="_blank" rel="noreferrer" className="pitch-link">
            Download our native Desktop Application for Windows, Mac & Linux
          </a>.
        </div>
      )}

      {/* Warning banner for missing system FFmpeg in Tauri Mode */}
      {isTauri && !isFfmpegAvailable && (
        <div className="warning-banner">
          ⚠️ <b>FFmpeg is missing:</b> Please install FFmpeg on your system PATH to enable native conversions.
        </div>
      )}

      {/* Main Workspace */}
      <div className="app-workspace">
        
        {/* Sidebar Configuration */}
        <aside className="app-sidebar">
          <div className="sidebar-title">Audio Config</div>
          
          <div className="form-group">
            <label>Format</label>
            <select 
              disabled={isConverting}
              value={config.format}
              onChange={(e) => setConfig(prev => ({ ...prev, format: e.target.value }))}
            >
              <option value="MP3">MP3</option>
              <option value="M4A">M4A</option>
              <option value="WAV">WAV</option>
              <option value="FLAC">FLAC</option>
            </select>
          </div>

          <div className="form-group">
            <label>Bitrate</label>
            <select 
              disabled={isConverting}
              value={config.bitrate}
              onChange={(e) => setConfig(prev => ({ ...prev, bitrate: e.target.value }))}
            >
              <option value="128k">128k</option>
              <option value="256k">256k</option>
              <option value="320k">320k</option>
            </select>
          </div>

          <label className="checkbox-group">
            <input 
              type="checkbox"
              disabled={isConverting}
              checked={config.isMono}
              onChange={(e) => setConfig(prev => ({ ...prev, isMono: e.target.checked }))}
            />
            Force Mono
          </label>

          <label className="checkbox-group">
            <input 
              type="checkbox"
              disabled={isConverting}
              checked={config.normalize}
              onChange={(e) => setConfig(prev => ({ ...prev, normalize: e.target.checked }))}
            />
            Normalize Volume
          </label>

          <div className="form-group">
            <label>Parallel Tasks</label>
            <select 
              disabled={isConverting || !isTauri}
              value={isTauri ? config.parallelTasks : 1}
              onChange={(e) => setConfig(prev => ({ ...prev, parallelTasks: parseInt(e.target.value, 10) }))}
            >
              <option value="1">1</option>
              <option value="2">2</option>
              <option value="3">3</option>
              <option value="4">4</option>
            </select>
            {!isTauri && <span className="input-info-hint">Parallelism is limited to 1 in Web browsers</span>}
          </div>

          <div style={{ height: '1px', backgroundColor: 'var(--border-color)', margin: '4px 0' }} />

          <div className="sidebar-title">Filename Template</div>
          <div className="form-group">
            <input 
              type="text"
              disabled={isConverting}
              value={config.filenameTemplate}
              onChange={(e) => setConfig(prev => ({ ...prev, filenameTemplate: e.target.value }))}
            />
            <span className="template-help">Tags: [Name] [Format] [Bitrate] [Date]</span>
          </div>

          <div style={{ height: '1px', backgroundColor: 'var(--border-color)', margin: '4px 0' }} />

          <div className="sidebar-title">Post Operation</div>
          <div className="form-group">
            <label>When complete</label>
            <select 
              disabled={isConverting}
              value={config.postAction}
              onChange={(e) => setConfig(prev => ({ ...prev, postAction: e.target.value }))}
            >
              <option value="None">None</option>
              <option value="Play Sound">Play Sound</option>
              {isTauri && <option value="Open Folder">Open Folder</option>}
            </select>
          </div>
        </aside>

        {/* Main Content Area */}
        <main className="app-content">
          <div className="list-container-border" onDragEnter={handleDragEnter} onDragLeave={handleDragLeave} onDragOver={(e) => e.preventDefault()} onDrop={handleDrop}>
            <ul className="queue-list">
              {items.map(item => (
                <li 
                  key={item.id} 
                  className="queue-item"
                  draggable={!isConverting ? "true" : "false"}
                  onDragStart={(e) => handleItemDragStart(e, item.id)}
                  onDragEnd={handleItemDragEnd}
                  onDragOver={(e) => handleItemDragOver(e, item.id)}
                  onDrop={(e) => handleItemDrop(e, item.id)}
                >
                  <div className="ext-badge">{item.fileExtension}</div>
                  
                  <div className="item-details">
                    <span className="item-name">{item.fileName}</span>
                    <span className="item-meta">
                      Size: {(item.fileSize / (1024 * 1024)).toFixed(2)} MB | Duration: {item.totalDuration}
                    </span>
                    <div className="trim-fields" draggable={false} onDragStart={e => e.preventDefault()}>
                      <span>Trim Start:</span>
                      <div className="tooltip-container">
                        <input 
                          type="text" 
                          disabled={isConverting}
                          className={item.isTimeRangeValid ? '' : 'input-invalid'}
                          value={item.startTime}
                          onChange={(e) => updateStartTime(item.id, e.target.value)}
                        />
                        {!item.isTimeRangeValid && (
                          <span className="tooltip-text">{item.timeValidationError}</span>
                        )}
                      </div>
                      <span>End:</span>
                      <div className="tooltip-container">
                        <input 
                          type="text"
                          disabled={isConverting}
                          className={item.isTimeRangeValid ? '' : 'input-invalid'}
                          value={item.endTime}
                          onChange={(e) => updateEndTime(item.id, e.target.value)}
                        />
                        {!item.isTimeRangeValid && (
                          <span className="tooltip-text">{item.timeValidationError}</span>
                        )}
                      </div>
                    </div>
                  </div>

                  <div className="item-status-panel">
                    <span className="status-text" style={{ 
                      color: item.status === 'Done' ? 'var(--success)' : 
                             item.status === 'Failed' ? 'var(--error)' : 
                             item.status === 'Converting' ? 'var(--accent)' : 'var(--text-primary)'
                    }}>{item.status}</span>
                    
                    {item.status === 'Converting' && (
                      <div className="item-progress-bar">
                        <div className="progress-fill" style={{ width: `${item.progress}%` }}></div>
                        <span className="progress-percent">{item.progress}%</span>
                      </div>
                    )}

                    {item.status === 'Done' && (
                      <div className="row-buttons">
                        <button className="row-btn" onClick={() => handlePlayAudio(item)}>
                          {!isTauri && playingItemId === item.id ? 'Pause' : 'Play'}
                        </button>
                        {isTauri ? (
                          <button className="row-btn row-btn-secondary" onClick={() => handleOpenFolder(item)}>Open Folder</button>
                        ) : (
                          <a 
                            className="row-btn row-btn-secondary download-link" 
                            href={item.outputPath || '#'} 
                            download={`${item.fileName}.${config.format.toLowerCase()}`}
                            style={{ display: 'inline-flex', alignItems: 'center', justifyContent: 'center', textDecoration: 'none' }}
                          >
                            Download
                          </a>
                        )}
                      </div>
                    )}

                    {item.status === 'Failed' && item.errorMessage && (
                      <span className="error-hint" title={item.errorMessage}>
                        ⚠️ Info
                      </span>
                    )}
                  </div>

                  <button 
                    disabled={isConverting}
                    className="remove-btn" 
                    onClick={() => removeQueueItem(item.id)}
                  >
                    ✕
                  </button>
                </li>
              ))}
              {/* Always-visible drop hint — sits below the last file, scrolls with the list */}
              <li className="watermark-hint" aria-hidden="true">Drop files here</li>
            </ul>
          </div>
        </main>
      </div>

      {/* Bottom Controls */}
      <footer className="app-controls">
        <div className="info-section">
          {isTauri ? (
            <>
              <span>Target folder:</span>
              <span className="target-folder-text">{config.targetFolderPath}</span>
            </>
          ) : (
            <span>Target folder: <b>Browser Downloads</b> (Files download directly client-side)</span>
          )}
          <span style={{ fontSize: '11px', marginTop: '2px' }}>{statusMessage}</span>
        </div>

        <div className="button-section">
          {isTauri && (
            <button className="btn" onClick={handleBrowseFolder}>Browse</button>
          )}
          
          <input 
            type="file" 
            multiple 
            ref={fileInputRef} 
            style={{ display: 'none' }} 
            onChange={(e) => e.target.files && addFilesToList(e.target.files)}
          />
          <button 
            disabled={isConverting}
            className="btn" 
            onClick={handleAddFilesTrigger}
          >
            Add Files
          </button>
          
          <button 
            disabled={isConverting || items.length === 0}
            className="btn" 
            onClick={() => {
              if (audioRef.current) {
                audioRef.current.pause();
              }
              setPlayingItemId(null);
              setItems([]);
            }}
          >
            Clear
          </button>
          
          <button 
            disabled={isConverting || !items.some(i => i.status === 'Done')}
            className="btn" 
            onClick={() => setItems(prev => prev.filter(i => i.status !== 'Done'))}
          >
            Clear Completed
          </button>
          
          {isConverting ? (
            <button className="btn btn-primary btn-danger" onClick={stopConversion}>Stop</button>
          ) : (
            <button className="btn btn-primary" disabled={items.length === 0} onClick={startConversions}>Start</button>
          )}
        </div>
      </footer>

      {/* Visual Drag and Drop Viewport Overlay */}
      {isDragOver && (
        <div 
          className="drag-overlay fade-in" 
          onDragEnter={handleDragEnter}
          onDragLeave={handleDragLeave} 
          onDragOver={(e) => e.preventDefault()}
          onDrop={handleDrop}
        >
          <div className="drag-border">
            <span className="drag-icon">📂</span>
            <span className="drag-title">Drop Video Files to Import</span>
            <span className="drag-subtitle">Supports webm, mp4, mkv, mov, avi, m4v</span>
          </div>
        </div>
      )}
    </div>
  );
}
