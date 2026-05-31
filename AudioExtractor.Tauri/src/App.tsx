import React, { useState, useEffect, useRef } from 'react';
import type { QueueItem, AppConfig } from './types';
import './App.css';

// Tauri API imports
import { invoke } from '@tauri-apps/api/core';
import { getCurrentWindow } from '@tauri-apps/api/window';

// Components
import AppHeader from './components/AppHeader';
import AppSidebar from './components/AppSidebar';
import QueueItemRow from './components/QueueItemRow';
import AppFooter from './components/AppFooter';
import DragDropOverlay from './components/DragDropOverlay';

// Hooks & utils
import { useFfmpegWasm } from './hooks/useFfmpegWasm';
import { useTauriListeners, addLocalPathsToQueue } from './hooks/useTauriListeners';
import { formatSeconds, validateItemTime, isSupportedExtension, fetchWebVideoDuration } from './lib/timeUtils';
import { fetchFile } from '@ffmpeg/util';

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
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const dragCounter = useRef(0);
  const isReordering = useRef(false);

  const { getOrInit: initFfmpegWasm } = useFfmpegWasm();

  // Apply visual theme to document body
  useEffect(() => {
    if (config.isDarkMode) {
      document.body.classList.remove('light-theme');
    } else {
      document.body.classList.add('light-theme');
    }
  }, [config.isDarkMode]);

  // Register Tauri listeners (progress, drag-drop, FFmpeg check)
  useTauriListeners({
    isTauri,
    isConverting,
    items,
    setItems,
    setIsDragOver,
    setIsFfmpegAvailable,
    setStatusMessage,
  });

  // ─── Config helper ───────────────────────────────────────────────
  const patchConfig = (patch: Partial<AppConfig>) =>
    setConfig(prev => ({ ...prev, ...patch }));

  // ─── File addition (web/browser mode) ────────────────────────────
  const addFilesToList = async (files: FileList | File[]) => {
    const newItems: QueueItem[] = [];
    const duplicates: string[] = [];

    for (let i = 0; i < files.length; i++) {
      const file = files[i];
      const ext = file.name.split('.').pop()?.toLowerCase() || '';
      if (!isSupportedExtension(ext)) continue;

      if (items.some(item => item.filePath === file.name || item.fileName === file.name.replace(/\.[^/.]+$/, ''))) {
        duplicates.push(file.name);
        continue;
      }

      const id = Math.random().toString(36).substring(2, 9);
      const nameWithoutExt = file.name.replace(/\.[^/.]+$/, '');

      newItems.push({
        id,
        file,
        fileName: nameWithoutExt,
        fileSize: file.size,
        fileExtension: ext.toUpperCase(),
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
        outputPath: null,
      });
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
              return { ...p, totalDuration: durationStr, totalDurationSeconds: durationSec, endTime: durationStr, ...validation };
            }
            return p;
          }));
        }
      });
    } else if (duplicates.length > 0) {
      setStatusMessage('Files already present in list.');
    }
  };

  // ─── Drag/drop (file imports) ─────────────────────────────────────
  const handleDragEnter = (e: React.DragEvent) => {
    e.preventDefault();
    if (isConverting || isReordering.current) return;
    dragCounter.current++;
    if (!isTauri) setIsDragOver(true);
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
    if (isReordering.current) { e.preventDefault(); return; }
    e.preventDefault();
    dragCounter.current = 0;
    setIsDragOver(false);
    if (!isConverting && e.dataTransfer.files?.length > 0) {
      addFilesToList(e.dataTransfer.files);
    }
  };

  // ─── Queue item reordering ────────────────────────────────────────
  const handleItemDragStart = (e: React.DragEvent, id: string) => {
    const target = e.target as HTMLElement;
    if (target.tagName === 'INPUT' || target.tagName === 'BUTTON' || target.closest('.trim-fields')) {
      e.preventDefault();
      return;
    }
    dragStartId.current = id;
    isReordering.current = true;
    dragCounter.current = 0;
    setIsDragOver(false);
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('text/plain', id);
  };

  const handleItemDragEnd = () => {
    isReordering.current = false;
    dragStartId.current = null;
    dragCounter.current = 0;
    setIsDragOver(false);
  };

  const handleItemDragOver = (e: React.DragEvent, _id: string) => {
    e.preventDefault();
    e.stopPropagation();
    e.dataTransfer.dropEffect = 'move';
  };

  const handleItemDrop = (e: React.DragEvent, dropTargetId: string) => {
    e.preventDefault();
    e.stopPropagation();
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
        const [dragged] = list.splice(dragIdx, 1);
        list.splice(dropIdx, 0, dragged);
      }
      return list;
    });
  };

  // ─── Trim time updates ────────────────────────────────────────────
  const updateStartTime = (id: string, val: string) => {
    setItems(prev => prev.map(item => {
      if (item.id !== id) return item;
      return { ...item, startTime: val, ...validateItemTime(item, val, item.endTime, item.totalDurationSeconds) };
    }));
  };

  const updateEndTime = (id: string, val: string) => {
    setItems(prev => prev.map(item => {
      if (item.id !== id) return item;
      return { ...item, endTime: val, ...validateItemTime(item, item.startTime, val, item.totalDurationSeconds) };
    }));
  };

  // ─── File actions ─────────────────────────────────────────────────
  const removeQueueItem = (id: string) => {
    if (isConverting) return;
    if (playingItemId === id && audioRef.current) {
      audioRef.current.pause();
      setPlayingItemId(null);
    }
    setItems(prev => prev.filter(item => item.id !== id));
  };

  const handleBrowseFolder = async () => {
    if (isTauri) {
      const folder = await invoke<string | null>('select_folder', { defaultPath: config.targetFolderPath });
      if (folder) patchConfig({ targetFolderPath: folder });
    }
  };

  const handleAddFiles = async () => {
    if (isTauri) {
      const selected = await invoke<string[]>('select_files');
      if (selected?.length > 0) {
        await addLocalPathsToQueue(selected, items, setItems, setStatusMessage);
      }
    } else {
      fileInputRef.current?.click();
    }
  };

  const handlePlayAudio = (item: QueueItem) => {
    if (isTauri && item.outputPath) {
      invoke('play_file', { path: item.outputPath });
    } else if (item.outputPath) {
      if (playingItemId === item.id) {
        audioRef.current?.pause();
        setPlayingItemId(null);
      } else {
        audioRef.current?.pause();
        const audio = new Audio(item.outputPath);
        audioRef.current = audio;
        setPlayingItemId(item.id);
        audio.play().catch(err => { alert('Playback error: ' + err.message); setPlayingItemId(null); });
        audio.onended = () => setPlayingItemId(null);
      }
    }
  };

  const handleOpenFolder = (item: QueueItem) => {
    if (isTauri && item.outputPath) {
      invoke('open_folder', { path: config.targetFolderPath });
    }
  };

  // ─── Conversion ───────────────────────────────────────────────────
  const startConversions = async () => {
    setIsConverting(true);
    setStatusMessage('Converting queue...');

    const pending = items.map(item => ({
      ...item,
      status: item.status === 'Done' ? 'Done' : 'Queued' as QueueItem['status'],
      progress: item.status === 'Done' ? 100 : 0,
    }));
    setItems(pending);

    const validItems = pending.filter(i => i.isTimeRangeValid && i.status !== 'Done');
    if (validItems.length === 0) {
      setIsConverting(false);
      setStatusMessage('No valid files queued.');
      return;
    }

    let ffmpegWasm = null;
    if (!isTauri) {
      try {
        setStatusMessage('Loading FFmpeg WebAssembly compiler (approx. 30MB)...');
        ffmpegWasm = await initFfmpegWasm();
        setStatusMessage('FFmpeg WebAssembly loaded successfully.');
      } catch (err: any) {
        setIsConverting(false);
        setStatusMessage('Failed to initialize browser WebAssembly compiler.');
        alert('WebAssembly initialization failed: ' + err.message);
        return;
      }
    }

    const maxParallel = isTauri ? config.parallelTasks : 1;
    const itemsToProcess = [...validItems];
    const activeTasks: Promise<void>[] = [];

    const processItem = async (item: QueueItem): Promise<void> => {
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
            totalDurationSeconds: item.totalDurationSeconds,
          });
          setItems(prev => prev.map(p => p.id === item.id ? { ...p, status: 'Done', outputPath: outPath, progress: 100 } : p));
        } catch (err: any) {
          setItems(prev => prev.map(p => p.id === item.id ? { ...p, status: 'Failed', errorMessage: err.toString(), progress: 0 } : p));
        }
      } else {
        if (item.fileSize > 150 * 1024 * 1024) {
          setItems(prev => prev.map(p => p.id === item.id ? {
            ...p,
            status: 'Failed',
            errorMessage: 'File exceeds 150MB browser limit. Please download our Desktop App to extract heavy files!',
            progress: 0,
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
          const fileData = await fetchFile(item.file);
          await ffmpeg.writeFile(item.filePath, fileData);

          const args: string[] = [];
          if (item.startTime && item.startTime !== '00:00:00') args.push('-ss', item.startTime);
          if (item.endTime && item.endTime !== item.totalDuration) args.push('-to', item.endTime);
          args.push('-i', item.filePath, '-vn');

          const ext = config.format.toLowerCase();
          if (ext === 'm4a') args.push('-c:a', 'aac', '-b:a', config.bitrate);
          else if (ext === 'wav') args.push('-c:a', 'pcm_s16le');
          else if (ext === 'flac') args.push('-c:a', 'flac');
          else args.push('-c:a', 'libmp3lame', '-b:a', config.bitrate);

          if (config.isMono) args.push('-ac', '1');
          if (config.normalize) args.push('-filter:a', 'loudnorm');

          const outputName = `${item.fileName}.${ext}`;
          args.push(outputName);

          await ffmpeg.exec(args);

          const outputData = await ffmpeg.readFile(outputName);
          const blob = new Blob([new Uint8Array(outputData as any)], { type: `audio/${ext}` });
          const url = URL.createObjectURL(blob);

          setItems(prev => prev.map(p => p.id === item.id ? { ...p, status: 'Done', outputPath: url, progress: 100 } : p));

          await ffmpeg.deleteFile(item.filePath);
          await ffmpeg.deleteFile(outputName);
        } catch (err: any) {
          setItems(prev => prev.map(p => p.id === item.id ? { ...p, status: 'Failed', errorMessage: err.toString(), progress: 0 } : p));
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

  // ─── Render ───────────────────────────────────────────────────────
  return (
    <div className="app-container" onDragEnter={handleDragEnter}>

      <AppHeader
        isDarkMode={config.isDarkMode}
        onToggleDarkMode={() => patchConfig({ isDarkMode: !config.isDarkMode })}
      />

      {/* Download pitch banner (web mode only) */}
      {!isTauri && (
        <div className="pitch-banner">
          🚀 <b>Converting large files (over 150MB) or need parallel batch conversions?</b>{' '}
          <a href="https://github.com/shreyansh-sawarn/Audio-Extractor/releases" target="_blank" rel="noreferrer" className="pitch-link">
            Download our native Desktop Application for Windows, Mac &amp; Linux
          </a>.
        </div>
      )}

      {/* Warning banner for missing system FFmpeg in Tauri mode */}
      {isTauri && !isFfmpegAvailable && (
        <div className="warning-banner">
          ⚠️ <b>FFmpeg is missing:</b> Please install FFmpeg on your system PATH to enable native conversions.
        </div>
      )}

      <div className="app-workspace">
        <AppSidebar
          config={config}
          isConverting={isConverting}
          isTauri={isTauri}
          onConfigChange={patchConfig}
        />

        <main className="app-content">
          <div
            className="list-container-border"
            onDragEnter={handleDragEnter}
            onDragLeave={handleDragLeave}
            onDragOver={(e) => e.preventDefault()}
            onDrop={handleDrop}
          >
            <ul className="queue-list">
              {items.map(item => (
                <QueueItemRow
                  key={item.id}
                  item={item}
                  isConverting={isConverting}
                  isTauri={isTauri}
                  isPlaying={playingItemId === item.id}
                  config={config}
                  onRemove={removeQueueItem}
                  onUpdateStartTime={updateStartTime}
                  onUpdateEndTime={updateEndTime}
                  onPlay={handlePlayAudio}
                  onOpenFolder={handleOpenFolder}
                  onDragStart={handleItemDragStart}
                  onDragEnd={handleItemDragEnd}
                  onDragOver={handleItemDragOver}
                  onDrop={handleItemDrop}
                />
              ))}
              {/* Always-visible drop hint — sits below the last file, scrolls with the list */}
              <li className="watermark-hint" aria-hidden="true">Drop files here</li>
            </ul>
          </div>
        </main>
      </div>

      <AppFooter
        config={config}
        items={items}
        isConverting={isConverting}
        isTauri={isTauri}
        statusMessage={statusMessage}
        audioRef={audioRef}
        fileInputRef={fileInputRef}
        onSetItems={setItems}
        onSetPlayingItemId={setPlayingItemId}
        onBrowseFolder={handleBrowseFolder}
        onAddFiles={handleAddFiles}
        onStartConversions={startConversions}
        onStopConversion={stopConversion}
        onFilesSelected={addFilesToList}
      />

      {isDragOver && (
        <DragDropOverlay
          onDragEnter={handleDragEnter}
          onDragLeave={handleDragLeave}
          onDragOver={(e) => e.preventDefault()}
          onDrop={handleDrop}
        />
      )}
    </div>
  );
}
